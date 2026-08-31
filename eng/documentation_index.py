#!/usr/bin/env python3
"""Build and query NekoLib's rebuildable local documentation index.

The SQLite database is local preparation. Repository sources, compiled API
baselines, and versioned documentation retain their documented authority.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import sqlite3
import subprocess
import sys
import tempfile
import textwrap
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path, PurePosixPath
from typing import Any, Iterable, Sequence
from urllib.parse import unquote, urlsplit


SCHEMA_NAME = "nekolib-documentation-index"
LEGACY_SCHEMA_NAME = "nekolib-documentation-migration-index"
SCHEMA_VERSION = "2"
SCANNER_VERSION = "nekolib-documentation-index/2"
DOCUMENT_SUFFIXES = {".md", ".txt"}
METADATA_PATTERN = re.compile(r"^\*\*([^*]+):\*\*\s*(.*)$")
HEADING_PATTERN = re.compile(r"^(#{1,6})\s+(.+?)\s*$")
FENCE_PATTERN = re.compile(r"^\s*(```+|~~~+)")
LINK_PATTERN = re.compile(
    r"(?P<image>!)?\[(?P<label>[^\]]*)\]\("
    r"(?P<target><[^>]+>|[^)\s]+(?:\s+[\"'][^\"']*[\"'])?)\)"
)
TOKEN_PATTERN = re.compile(r"[\w.:-]+", re.UNICODE)


BASE_SCHEMA = r"""
PRAGMA foreign_keys = ON;
PRAGMA user_version = 2;

CREATE TABLE IF NOT EXISTS schema_meta (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
) WITHOUT ROWID;

CREATE TABLE IF NOT EXISTS inventory_runs (
    run_id INTEGER PRIMARY KEY,
    started_at_utc TEXT NOT NULL,
    completed_at_utc TEXT,
    repository_root TEXT NOT NULL,
    branch TEXT,
    head_commit TEXT,
    worktree_state TEXT NOT NULL CHECK (
        worktree_state IN ('clean', 'dirty', 'unknown')
    ),
    scanner_version TEXT,
    notes TEXT,
    tree_fingerprint TEXT
);

CREATE TABLE IF NOT EXISTS files (
    file_id INTEGER PRIMARY KEY,
    run_id INTEGER NOT NULL REFERENCES inventory_runs(run_id) ON DELETE CASCADE,
    path TEXT NOT NULL,
    git_state TEXT NOT NULL CHECK (
        git_state IN ('tracked', 'untracked', 'ignored', 'missing', 'unknown')
    ),
    content_sha256 TEXT,
    size_bytes INTEGER CHECK (size_bytes IS NULL OR size_bytes >= 0),
    modified_at_utc TEXT,
    media_type TEXT,
    title TEXT,
    module_candidate TEXT,
    boundary_candidate TEXT,
    document_kind TEXT,
    lifecycle TEXT,
    subject TEXT,
    authority_role TEXT,
    reference_date TEXT,
    reference_commit TEXT,
    has_mixed_responsibilities INTEGER NOT NULL DEFAULT 0 CHECK (
        has_mixed_responsibilities IN (0, 1)
    ),
    UNIQUE (run_id, path)
);

CREATE INDEX IF NOT EXISTS ix_files_run_module
    ON files (run_id, module_candidate, path);
CREATE INDEX IF NOT EXISTS ix_files_run_classification
    ON files (run_id, document_kind, lifecycle);

CREATE TABLE IF NOT EXISTS file_metadata (
    file_id INTEGER NOT NULL REFERENCES files(file_id) ON DELETE CASCADE,
    key TEXT NOT NULL,
    value TEXT NOT NULL,
    source_line INTEGER CHECK (source_line IS NULL OR source_line > 0),
    PRIMARY KEY (file_id, key)
) WITHOUT ROWID;

CREATE TABLE IF NOT EXISTS document_links (
    link_id INTEGER PRIMARY KEY,
    source_file_id INTEGER NOT NULL REFERENCES files(file_id) ON DELETE CASCADE,
    source_line INTEGER CHECK (source_line IS NULL OR source_line > 0),
    link_kind TEXT NOT NULL,
    raw_target TEXT NOT NULL,
    resolved_path TEXT,
    fragment TEXT,
    target_file_id INTEGER REFERENCES files(file_id) ON DELETE SET NULL,
    resolution_state TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_document_links_source
    ON document_links (source_file_id, source_line);
CREATE INDEX IF NOT EXISTS ix_document_links_target
    ON document_links (target_file_id);

CREATE TABLE IF NOT EXISTS document_chunks (
    chunk_id INTEGER PRIMARY KEY,
    file_id INTEGER NOT NULL REFERENCES files(file_id) ON DELETE CASCADE,
    ordinal INTEGER NOT NULL CHECK (ordinal >= 0),
    heading_path TEXT,
    start_line INTEGER NOT NULL CHECK (start_line > 0),
    end_line INTEGER NOT NULL CHECK (end_line >= start_line),
    content TEXT NOT NULL,
    content_sha256 TEXT NOT NULL,
    UNIQUE (file_id, ordinal)
);

CREATE VIRTUAL TABLE IF NOT EXISTS document_chunks_fts USING fts5(
    heading_path,
    content,
    content = 'document_chunks',
    content_rowid = 'chunk_id',
    tokenize = 'unicode61 remove_diacritics 2'
);

CREATE TRIGGER IF NOT EXISTS document_chunks_after_insert
AFTER INSERT ON document_chunks BEGIN
    INSERT INTO document_chunks_fts(rowid, heading_path, content)
    VALUES (new.chunk_id, new.heading_path, new.content);
END;
CREATE TRIGGER IF NOT EXISTS document_chunks_after_delete
AFTER DELETE ON document_chunks BEGIN
    INSERT INTO document_chunks_fts(document_chunks_fts, rowid, heading_path, content)
    VALUES ('delete', old.chunk_id, old.heading_path, old.content);
END;
CREATE TRIGGER IF NOT EXISTS document_chunks_after_update
AFTER UPDATE ON document_chunks BEGIN
    INSERT INTO document_chunks_fts(document_chunks_fts, rowid, heading_path, content)
    VALUES ('delete', old.chunk_id, old.heading_path, old.content);
    INSERT INTO document_chunks_fts(rowid, heading_path, content)
    VALUES (new.chunk_id, new.heading_path, new.content);
END;

CREATE TABLE IF NOT EXISTS accepted_api_baselines (
    baseline_id INTEGER PRIMARY KEY,
    run_id INTEGER NOT NULL REFERENCES inventory_runs(run_id) ON DELETE CASCADE,
    package_id TEXT NOT NULL,
    target_framework TEXT NOT NULL,
    path TEXT NOT NULL,
    content_sha256 TEXT NOT NULL,
    content TEXT NOT NULL,
    UNIQUE (run_id, package_id, target_framework)
);

CREATE TABLE IF NOT EXISTS api_documents (
    api_document_id INTEGER PRIMARY KEY,
    run_id INTEGER NOT NULL REFERENCES inventory_runs(run_id) ON DELETE CASCADE,
    baseline_id INTEGER NOT NULL REFERENCES accepted_api_baselines(baseline_id),
    package_id TEXT NOT NULL,
    assembly_name TEXT NOT NULL,
    target_framework TEXT NOT NULL,
    boundary_key TEXT NOT NULL,
    project_path TEXT NOT NULL,
    xml_path TEXT NOT NULL,
    content_sha256 TEXT NOT NULL,
    member_count INTEGER NOT NULL CHECK (member_count >= 0),
    UNIQUE (run_id, package_id, target_framework)
);

CREATE INDEX IF NOT EXISTS ix_api_documents_run_boundary
    ON api_documents (run_id, boundary_key, package_id, target_framework);

CREATE TABLE IF NOT EXISTS api_members (
    api_member_id INTEGER PRIMARY KEY,
    api_document_id INTEGER NOT NULL REFERENCES api_documents(api_document_id) ON DELETE CASCADE,
    member_id TEXT NOT NULL,
    member_kind TEXT NOT NULL,
    summary TEXT,
    remarks TEXT,
    returns_text TEXT,
    parameters_json TEXT NOT NULL,
    exceptions_json TEXT NOT NULL,
    content TEXT NOT NULL,
    content_sha256 TEXT NOT NULL,
    UNIQUE (api_document_id, member_id)
);

CREATE VIRTUAL TABLE IF NOT EXISTS api_members_fts USING fts5(
    member_id,
    summary,
    content,
    content = 'api_members',
    content_rowid = 'api_member_id',
    tokenize = 'unicode61 remove_diacritics 2'
);

CREATE TRIGGER IF NOT EXISTS api_members_after_insert
AFTER INSERT ON api_members BEGIN
    INSERT INTO api_members_fts(rowid, member_id, summary, content)
    VALUES (new.api_member_id, new.member_id, new.summary, new.content);
END;
CREATE TRIGGER IF NOT EXISTS api_members_after_delete
AFTER DELETE ON api_members BEGIN
    INSERT INTO api_members_fts(api_members_fts, rowid, member_id, summary, content)
    VALUES ('delete', old.api_member_id, old.member_id, old.summary, old.content);
END;
CREATE TRIGGER IF NOT EXISTS api_members_after_update
AFTER UPDATE ON api_members BEGIN
    INSERT INTO api_members_fts(api_members_fts, rowid, member_id, summary, content)
    VALUES ('delete', old.api_member_id, old.member_id, old.summary, old.content);
    INSERT INTO api_members_fts(rowid, member_id, summary, content)
    VALUES (new.api_member_id, new.member_id, new.summary, new.content);
END;

CREATE VIEW IF NOT EXISTS current_inventory AS
SELECT f.* FROM files AS f
JOIN (SELECT MAX(run_id) AS run_id FROM inventory_runs) AS latest
    ON latest.run_id = f.run_id;

CREATE VIEW IF NOT EXISTS current_api_documents AS
SELECT d.* FROM api_documents AS d
JOIN (SELECT MAX(run_id) AS run_id FROM inventory_runs) AS latest
    ON latest.run_id = d.run_id;
"""


@dataclass
class Document:
    path: str
    git_state: str
    absolute_path: Path
    text: str | None
    raw_bytes: bytes | None
    metadata: dict[str, tuple[str, int]]
    title: str | None
    decode_replaced: bool
    file_id: int | None = None


@dataclass(frozen=True)
class ProjectInfo:
    package_id: str
    assembly_name: str
    project_path: str
    project_directory: Path
    target_frameworks: tuple[str, ...]


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def sha256_bytes(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest()


def normalize_space(value: str | None) -> str:
    return " ".join((value or "").split())


def run_process(
    arguments: Sequence[str], cwd: Path, *, check: bool = True
) -> subprocess.CompletedProcess[bytes]:
    result = subprocess.run(
        list(arguments),
        cwd=cwd,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )
    if check and result.returncode != 0:
        stderr = result.stderr.decode("utf-8", errors="replace").strip()
        raise RuntimeError(
            f"{' '.join(arguments)} failed with exit code {result.returncode}: {stderr}"
        )
    return result


def run_git(root: Path, *arguments: str) -> bytes:
    return run_process(["git", *arguments], root).stdout


def decode_git_paths(payload: bytes) -> list[str]:
    return [
        item.decode("utf-8", errors="surrogateescape").replace("\\", "/")
        for item in payload.split(b"\0")
        if item
    ]


def repository_state(root: Path) -> dict[str, str]:
    branch = run_git(root, "branch", "--show-current").decode("utf-8").strip()
    head = run_git(root, "rev-parse", "HEAD").decode("ascii").strip()
    status = run_git(root, "status", "--porcelain=v1", "-z", "--untracked-files=normal")
    digest = hashlib.sha256()
    digest.update(status)
    changed_paths = decode_git_paths(run_git(root, "diff", "--name-only", "-z", "HEAD", "--"))
    changed_paths.extend(
        decode_git_paths(run_git(root, "ls-files", "--others", "--exclude-standard", "-z"))
    )
    for relative in sorted(set(changed_paths), key=str.casefold):
        absolute = root / PurePosixPath(relative)
        digest.update(relative.encode("utf-8", errors="surrogateescape"))
        digest.update(b"\0")
        if absolute.is_file():
            digest.update(absolute.read_bytes())
        else:
            digest.update(b"<missing>")
        digest.update(b"\0")
    return {
        "branch": branch,
        "head_commit": head,
        "worktree_state": "dirty" if status else "clean",
        "tree_fingerprint": digest.hexdigest(),
    }


def get_document_paths(root: Path) -> tuple[list[str], list[str]]:
    pathspecs = ["*.md", "*.txt"]
    tracked = decode_git_paths(run_git(root, "ls-files", "-z", "--", *pathspecs))
    untracked = decode_git_paths(
        run_git(root, "ls-files", "--others", "--exclude-standard", "-z", "--", *pathspecs)
    )
    return sorted(set(tracked), key=str.casefold), sorted(set(untracked), key=str.casefold)


def read_document(root: Path, path: str, git_state: str) -> Document:
    absolute = root / PurePosixPath(path)
    if git_state == "missing":
        return Document(path, git_state, absolute, None, None, {}, None, False)
    raw = absolute.read_bytes()
    replaced = False
    try:
        text = raw.decode("utf-8-sig")
    except UnicodeDecodeError:
        text = raw.decode("utf-8", errors="replace")
        replaced = True
    metadata: dict[str, tuple[str, int]] = {}
    title: str | None = None
    in_fence = False
    fence_marker: str | None = None
    for line_number, line in enumerate(text.splitlines(), start=1):
        fence = FENCE_PATTERN.match(line)
        if fence:
            marker = fence.group(1)[0]
            if not in_fence:
                in_fence = True
                fence_marker = marker
            elif marker == fence_marker:
                in_fence = False
                fence_marker = None
            continue
        if in_fence:
            continue
        heading = HEADING_PATTERN.match(line)
        if title is None and heading and len(heading.group(1)) == 1:
            title = heading.group(2).strip()
        match = METADATA_PATTERN.match(line)
        if match:
            key = match.group(1).strip()
            metadata.setdefault(key, (match.group(2).strip(), line_number))
    return Document(path, git_state, absolute, text, raw, metadata, title, replaced)


def metadata_value(document: Document, key: str) -> str | None:
    value = document.metadata.get(key)
    return value[0] if value else None


def effective_indexing(document: Document) -> tuple[str, int | None]:
    declared = document.metadata.get("Indexing")
    if declared is not None:
        return declared
    return ("exclude", None) if document.text is None else ("include", None)


def infer_boundary(document: Document) -> str | None:
    declared = metadata_value(document, "Boundary")
    if declared:
        return declared.lower()
    parts = PurePosixPath(document.path).parts
    lowered = [part.lower() for part in parts]
    if not parts:
        return None
    if lowered[0] in {".agents", ".claude"}:
        return "global"
    if lowered[0] == "docs":
        if len(parts) >= 3 and lowered[1] == "modules":
            return lowered[2]
        return "global"
    if lowered[0] == "src" and len(parts) >= 2:
        return lowered[1]
    return "global"


def iter_chunks(text: str) -> Iterable[tuple[int, str | None, int, int, str, str]]:
    lines = text.splitlines()
    if not lines:
        return
    headings: list[str | None] = [None] * 6
    chunks: list[tuple[str | None, int, int, str]] = []
    start = 1
    current_heading: str | None = None
    in_fence = False
    fence_marker: str | None = None
    for index, line in enumerate(lines, start=1):
        fence = FENCE_PATTERN.match(line)
        if fence:
            marker = fence.group(1)[0]
            if not in_fence:
                in_fence = True
                fence_marker = marker
            elif marker == fence_marker:
                in_fence = False
                fence_marker = None
            continue
        heading = None if in_fence else HEADING_PATTERN.match(line)
        if heading:
            if index > start:
                content = "\n".join(lines[start - 1 : index - 1]).strip()
                if content:
                    chunks.append((current_heading, start, index - 1, content))
            level = len(heading.group(1))
            headings[level - 1] = heading.group(2).strip()
            for offset in range(level, 6):
                headings[offset] = None
            current_heading = " > ".join(item for item in headings if item)
            start = index
    content = "\n".join(lines[start - 1 :]).strip()
    if content:
        chunks.append((current_heading, start, len(lines), content))
    for ordinal, (heading, start_line, end_line, content) in enumerate(chunks):
        yield ordinal, heading, start_line, end_line, content, sha256_bytes(content.encode("utf-8"))


def iter_links(text: str) -> Iterable[tuple[int, str, str]]:
    in_fence = False
    fence_marker: str | None = None
    for line_number, line in enumerate(text.splitlines(), start=1):
        fence = FENCE_PATTERN.match(line)
        if fence:
            marker = fence.group(1)[0]
            if not in_fence:
                in_fence = True
                fence_marker = marker
            elif marker == fence_marker:
                in_fence = False
                fence_marker = None
            continue
        if in_fence:
            continue
        for match in LINK_PATTERN.finditer(line):
            raw = match.group("target").strip()
            if raw.startswith("<") and raw.endswith(">"):
                raw = raw[1:-1]
            elif " " in raw:
                raw = raw.split(" ", 1)[0]
            yield line_number, "image" if match.group("image") else "markdown", raw


def resolve_link(
    source_path: str, raw_target: str, document_ids: dict[str, int]
) -> tuple[str | None, str | None, int | None, str]:
    parsed = urlsplit(raw_target)
    if parsed.scheme or raw_target.startswith("//"):
        return None, parsed.fragment or None, None, "external"
    if not parsed.path:
        return source_path, parsed.fragment or None, document_ids.get(source_path.casefold()), "anchor-unverified"
    decoded = unquote(parsed.path).replace("\\", "/")
    source_parent = PurePosixPath(source_path).parent
    combined: list[str] = []
    for part in (source_parent / PurePosixPath(decoded)).parts:
        if part in {"", "."}:
            continue
        if part == "..":
            if combined:
                combined.pop()
            continue
        combined.append(part)
    relative = PurePosixPath(*combined).as_posix()
    target_id = document_ids.get(relative.casefold())
    if target_id is None:
        return relative, parsed.fragment or None, None, "missing"
    return relative, parsed.fragment or None, target_id, "resolved"


def xml_local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def project_property(root: ET.Element, name: str) -> str | None:
    for element in root.iter():
        if xml_local_name(element.tag) == name and normalize_space(element.text):
            return normalize_space(element.text)
    return None


def discover_packable_projects(root: Path) -> dict[str, ProjectInfo]:
    projects: dict[str, ProjectInfo] = {}
    for project_path in sorted((root / "src").rglob("*.csproj")):
        project_root = ET.parse(project_path).getroot()
        if (project_property(project_root, "IsPackable") or "").lower() != "true":
            continue
        package_id = project_property(project_root, "PackageId")
        frameworks = project_property(project_root, "TargetFrameworks") or project_property(
            project_root, "TargetFramework"
        )
        if not package_id or not frameworks:
            raise RuntimeError(f"Packable project lacks PackageId/target frameworks: {project_path}")
        assembly_name = project_property(project_root, "AssemblyName") or project_path.stem
        relative = project_path.relative_to(root).as_posix()
        info = ProjectInfo(
            package_id=package_id,
            assembly_name=assembly_name,
            project_path=relative,
            project_directory=project_path.parent,
            target_frameworks=tuple(item.strip() for item in frameworks.split(";") if item.strip()),
        )
        if package_id in projects:
            raise RuntimeError(f"Duplicate packable PackageId: {package_id}")
        projects[package_id] = info
    return projects


def package_boundary(package_id: str) -> str:
    value = package_id.removeprefix("NekoLib.").lower()
    return value if value else "global"


def flatten_xml(element: ET.Element | None) -> str:
    if element is None:
        return ""
    parts: list[str] = []

    root_tag = xml_local_name(element.tag)
    if root_tag == "inheritdoc":
        marker = element.get("cref")
        parts.append(f"inheritdoc {marker}" if marker else "inheritdoc")
    elif root_tag == "see":
        marker = element.get("cref") or element.get("href") or element.get("langword")
        if marker:
            parts.append(marker)
    elif root_tag in {"paramref", "typeparamref"}:
        marker = element.get("name")
        if marker:
            parts.append(marker)

    def visit(node: ET.Element) -> None:
        if node.text:
            parts.append(node.text)
        for child in node:
            tag = xml_local_name(child.tag)
            if tag == "see":
                marker = child.get("cref") or child.get("href") or child.get("langword")
                if marker:
                    parts.append(marker)
            elif tag in {"paramref", "typeparamref"}:
                marker = child.get("name")
                if marker:
                    parts.append(marker)
            elif tag == "inheritdoc":
                marker = child.get("cref")
                parts.append(f"inheritdoc {marker}" if marker else "inheritdoc")
            visit(child)
            if child.tail:
                parts.append(child.tail)

    visit(element)
    return normalize_space(" ".join(parts))


def member_kind(member_id: str) -> str:
    kinds = {"T": "type", "M": "method", "P": "property", "F": "field", "E": "event", "N": "namespace"}
    return kinds.get(member_id[:1], "other")


def open_database(database_path: Path) -> sqlite3.Connection:
    database_path.parent.mkdir(parents=True, exist_ok=True)
    connection = sqlite3.connect(database_path)
    connection.row_factory = sqlite3.Row
    connection.execute("PRAGMA foreign_keys=ON")
    existing = connection.execute(
        "SELECT name FROM sqlite_master WHERE type='table' AND name='schema_meta'"
    ).fetchone()
    if existing:
        metadata = dict(connection.execute("SELECT key, value FROM schema_meta"))
        schema_name = metadata.get("schema_name")
        if schema_name not in {SCHEMA_NAME, LEGACY_SCHEMA_NAME}:
            raise RuntimeError(f"Unexpected documentation index schema: {schema_name}")
        columns = {row[1] for row in connection.execute("PRAGMA table_info(inventory_runs)")}
        if "tree_fingerprint" not in columns:
            connection.execute("ALTER TABLE inventory_runs ADD COLUMN tree_fingerprint TEXT")
    connection.executescript(BASE_SCHEMA)
    connection.execute(
        "INSERT OR REPLACE INTO schema_meta(key, value) VALUES ('schema_name', ?)",
        (SCHEMA_NAME,),
    )
    connection.execute(
        "INSERT OR REPLACE INTO schema_meta(key, value) VALUES ('schema_version', ?)",
        (SCHEMA_VERSION,),
    )
    connection.execute(
        "INSERT OR REPLACE INTO schema_meta(key, value) VALUES ('authority', 'rebuildable-local-index-not-repository-evidence')"
    )
    connection.execute("PRAGMA user_version=2")
    connection.commit()
    return connection


def collect_documents(root: Path) -> list[Document]:
    tracked, untracked = get_document_paths(root)
    tracked_set = set(tracked)
    documents: list[Document] = []
    for path in tracked:
        absolute = root / PurePosixPath(path)
        documents.append(read_document(root, path, "tracked" if absolute.is_file() else "missing"))
    for path in untracked:
        if path not in tracked_set:
            documents.append(read_document(root, path, "untracked"))
    return sorted(documents, key=lambda item: item.path.casefold())


def insert_markdown_snapshot(
    connection: sqlite3.Connection, root: Path, run_id: int, documents: list[Document]
) -> dict[str, int]:
    for document in documents:
        stat = document.absolute_path.stat() if document.absolute_path.is_file() else None
        digest = sha256_bytes(document.raw_bytes) if document.raw_bytes is not None else None
        media_type = "text/markdown" if document.absolute_path.suffix.lower() == ".md" else "text/plain"
        modified = datetime.fromtimestamp(stat.st_mtime, timezone.utc).isoformat() if stat else None
        cursor = connection.execute(
            """
            INSERT INTO files (
                run_id, path, git_state, content_sha256, size_bytes,
                modified_at_utc, media_type, title, module_candidate,
                boundary_candidate, document_kind, lifecycle, subject,
                authority_role, reference_date, reference_commit,
                has_mixed_responsibilities
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 0)
            """,
            (
                run_id,
                document.path,
                document.git_state,
                digest,
                stat.st_size if stat else None,
                modified,
                media_type,
                document.title,
                infer_boundary(document),
                infer_boundary(document),
                metadata_value(document, "Kind"),
                metadata_value(document, "Lifecycle"),
                metadata_value(document, "Subject"),
                metadata_value(document, "Authority role"),
                metadata_value(document, "Reference date"),
                metadata_value(document, "Reference commit"),
            ),
        )
        document.file_id = int(cursor.lastrowid)
        for key, (value, source_line) in document.metadata.items():
            connection.execute(
                "INSERT INTO file_metadata(file_id, key, value, source_line) VALUES (?, ?, ?, ?)",
                (document.file_id, key, value, source_line),
            )
        if "Indexing" not in document.metadata:
            indexing, source_line = effective_indexing(document)
            connection.execute(
                "INSERT INTO file_metadata(file_id, key, value, source_line) VALUES (?, 'Indexing', ?, ?)",
                (document.file_id, indexing, source_line),
            )
    document_ids = {
        document.path.casefold(): int(document.file_id)
        for document in documents
        if document.file_id is not None
    }
    for document in documents:
        if document.text is None or document.file_id is None:
            continue
        if effective_indexing(document)[0].lower() == "exclude":
            continue
        for ordinal, heading, start_line, end_line, content, digest in iter_chunks(document.text):
            connection.execute(
                """
                INSERT INTO document_chunks(
                    file_id, ordinal, heading_path, start_line, end_line, content, content_sha256
                ) VALUES (?, ?, ?, ?, ?, ?, ?)
                """,
                (document.file_id, ordinal, heading, start_line, end_line, content, digest),
            )
        for source_line, link_kind, raw_target in iter_links(document.text):
            resolved_path, fragment, target_id, state = resolve_link(
                document.path, raw_target, document_ids
            )
            connection.execute(
                """
                INSERT INTO document_links(
                    source_file_id, source_line, link_kind, raw_target,
                    resolved_path, fragment, target_file_id, resolution_state
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?)
                """,
                (
                    document.file_id,
                    source_line,
                    link_kind,
                    raw_target,
                    resolved_path,
                    fragment,
                    target_id,
                    state,
                ),
            )
    return {
        "files": len(documents),
        "tracked": sum(item.git_state == "tracked" for item in documents),
        "untracked": sum(item.git_state == "untracked" for item in documents),
        "missing": sum(item.git_state == "missing" for item in documents),
        "decode_replaced": sum(item.decode_replaced for item in documents),
    }


def insert_api_snapshot(
    connection: sqlite3.Connection,
    root: Path,
    run_id: int,
    configuration: str,
) -> dict[str, int]:
    projects = discover_packable_projects(root)
    baseline_root = root / "eng" / "public-api"
    baseline_paths = sorted(baseline_root.glob("*/*.approved.txt"))
    if not baseline_paths:
        raise RuntimeError(f"No accepted API baselines found under {baseline_root}")
    documents = 0
    members = 0
    seen_pairs: set[tuple[str, str]] = set()
    for baseline_path in baseline_paths:
        package_id = baseline_path.parent.name
        suffix = ".approved.txt"
        if not baseline_path.name.endswith(suffix):
            continue
        target_framework = baseline_path.name[: -len(suffix)]
        project = projects.get(package_id)
        if project is None:
            raise RuntimeError(f"Accepted API baseline has no packable project: {baseline_path}")
        if target_framework not in project.target_frameworks:
            raise RuntimeError(
                f"Accepted API target {target_framework} is not declared by {project.project_path}"
            )
        pair = (package_id, target_framework)
        if pair in seen_pairs:
            raise RuntimeError(f"Duplicate accepted API baseline: {package_id}/{target_framework}")
        seen_pairs.add(pair)
        xml_path = (
            project.project_directory
            / "bin"
            / configuration
            / target_framework
            / f"{project.assembly_name}.xml"
        )
        if not xml_path.is_file():
            raise RuntimeError(f"Generated XML documentation is missing: {xml_path}")
        baseline_bytes = baseline_path.read_bytes()
        baseline_text = baseline_bytes.decode("utf-8-sig")
        baseline_cursor = connection.execute(
            """
            INSERT INTO accepted_api_baselines(
                run_id, package_id, target_framework, path, content_sha256, content
            ) VALUES (?, ?, ?, ?, ?, ?)
            """,
            (
                run_id,
                package_id,
                target_framework,
                baseline_path.relative_to(root).as_posix(),
                sha256_bytes(baseline_bytes),
                baseline_text,
            ),
        )
        xml_bytes = xml_path.read_bytes()
        xml_root = ET.fromstring(xml_bytes)
        assembly_element = xml_root.find("./assembly/name")
        assembly_in_xml = normalize_space(assembly_element.text if assembly_element is not None else None)
        if assembly_in_xml != project.assembly_name:
            raise RuntimeError(
                f"XML assembly '{assembly_in_xml}' does not match '{project.assembly_name}': {xml_path}"
            )
        member_elements = list(xml_root.findall("./members/member"))
        document_cursor = connection.execute(
            """
            INSERT INTO api_documents(
                run_id, baseline_id, package_id, assembly_name, target_framework,
                boundary_key, project_path, xml_path, content_sha256, member_count
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            (
                run_id,
                int(baseline_cursor.lastrowid),
                package_id,
                project.assembly_name,
                target_framework,
                package_boundary(package_id),
                project.project_path,
                xml_path.relative_to(root).as_posix(),
                sha256_bytes(xml_bytes),
                len(member_elements),
            ),
        )
        document_id = int(document_cursor.lastrowid)
        seen_members: set[str] = set()
        for member in member_elements:
            member_id = (member.get("name") or "").strip()
            if not member_id:
                raise RuntimeError(f"XML member without a name: {xml_path}")
            if member_id in seen_members:
                raise RuntimeError(f"Duplicate XML member '{member_id}': {xml_path}")
            seen_members.add(member_id)
            summary = flatten_xml(member.find("summary"))
            remarks = flatten_xml(member.find("remarks"))
            returns_text = flatten_xml(member.find("returns")) or flatten_xml(member.find("value"))
            parameters = [
                {"name": item.get("name") or "", "text": flatten_xml(item)}
                for item in member.findall("param")
            ]
            exceptions = [
                {"cref": item.get("cref") or "", "text": flatten_xml(item)}
                for item in member.findall("exception")
            ]
            other_parts = [
                flatten_xml(item)
                for item in member
                if xml_local_name(item.tag)
                not in {"summary", "remarks", "returns", "value", "param", "exception"}
            ]
            content = normalize_space(
                " ".join(
                    [
                        member_id,
                        summary,
                        remarks,
                        returns_text,
                        *(item["name"] + " " + item["text"] for item in parameters),
                        *(item["cref"] + " " + item["text"] for item in exceptions),
                        *other_parts,
                    ]
                )
            )
            if not content or content == member_id:
                raise RuntimeError(f"XML member has no effective documentation: {member_id} in {xml_path}")
            connection.execute(
                """
                INSERT INTO api_members(
                    api_document_id, member_id, member_kind, summary, remarks,
                    returns_text, parameters_json, exceptions_json, content, content_sha256
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                """,
                (
                    document_id,
                    member_id,
                    member_kind(member_id),
                    summary or None,
                    remarks or None,
                    returns_text or None,
                    json.dumps(parameters, ensure_ascii=False, sort_keys=True),
                    json.dumps(exceptions, ensure_ascii=False, sort_keys=True),
                    content,
                    sha256_bytes(content.encode("utf-8")),
                ),
            )
        documents += 1
        members += len(member_elements)
    expected_pairs = {
        (package_id, target)
        for package_id, project in projects.items()
        if package_id != "NekoLib.Watchdog.Host"
        for target in project.target_frameworks
    }
    if seen_pairs != expected_pairs:
        missing = sorted(expected_pairs - seen_pairs)
        extra = sorted(seen_pairs - expected_pairs)
        raise RuntimeError(f"Accepted API/XML topology mismatch. Missing={missing}; Extra={extra}")
    return {"api_documents": documents, "api_members": members, "api_baselines": len(seen_pairs)}


def latest_run(connection: sqlite3.Connection) -> sqlite3.Row:
    row = connection.execute("SELECT * FROM inventory_runs ORDER BY run_id DESC LIMIT 1").fetchone()
    if row is None:
        raise RuntimeError("Documentation index contains no inventory run.")
    return row


def status_payload(root: Path, database_path: Path) -> dict[str, Any]:
    if not database_path.is_file():
        return {
            "exists": False,
            "database": str(database_path),
            "fresh": False,
            "clean_current": False,
            "reasons": ["index database is missing"],
        }
    connection = sqlite3.connect(database_path)
    connection.row_factory = sqlite3.Row
    connection.execute("PRAGMA foreign_keys=ON")
    metadata = dict(connection.execute("SELECT key, value FROM schema_meta"))
    if metadata.get("schema_name") != SCHEMA_NAME or metadata.get("schema_version") != SCHEMA_VERSION:
        connection.close()
        return {
            "exists": True,
            "database": str(database_path),
            "fresh": False,
            "clean_current": False,
            "reasons": [
                f"unsupported schema {metadata.get('schema_name')}/{metadata.get('schema_version')}"
            ],
        }
    run = latest_run(connection)
    current = repository_state(root)
    reasons: list[str] = []
    if Path(run["repository_root"]).resolve() != root.resolve():
        reasons.append("repository root differs")
    if (run["branch"] or "") != current["branch"]:
        reasons.append("branch differs")
    if (run["head_commit"] or "") != current["head_commit"]:
        reasons.append("HEAD differs")
    if (run["tree_fingerprint"] or "") != current["tree_fingerprint"]:
        reasons.append("working-tree fingerprint differs")
    xml_drift = 0
    baseline_drift = 0
    for row in connection.execute(
        "SELECT xml_path, content_sha256 FROM api_documents WHERE run_id = ?", (run["run_id"],)
    ):
        path = root / PurePosixPath(row["xml_path"])
        if not path.is_file() or sha256_bytes(path.read_bytes()) != row["content_sha256"]:
            xml_drift += 1
    for row in connection.execute(
        "SELECT path, content_sha256 FROM accepted_api_baselines WHERE run_id = ?", (run["run_id"],)
    ):
        path = root / PurePosixPath(row["path"])
        if not path.is_file() or sha256_bytes(path.read_bytes()) != row["content_sha256"]:
            baseline_drift += 1
    if xml_drift:
        reasons.append(f"{xml_drift} generated XML document(s) differ or are missing")
    if baseline_drift:
        reasons.append(f"{baseline_drift} accepted API baseline(s) differ or are missing")
    integrity = connection.execute("PRAGMA integrity_check").fetchone()[0]
    foreign_keys = len(connection.execute("PRAGMA foreign_key_check").fetchall())
    if integrity != "ok":
        reasons.append(f"SQLite integrity_check={integrity}")
    if foreign_keys:
        reasons.append(f"SQLite foreign-key violations={foreign_keys}")
    counts = {
        "files": connection.execute("SELECT COUNT(*) FROM files WHERE run_id = ?", (run["run_id"],)).fetchone()[0],
        "chunks": connection.execute(
            "SELECT COUNT(*) FROM document_chunks c JOIN files f ON f.file_id=c.file_id WHERE f.run_id = ?",
            (run["run_id"],),
        ).fetchone()[0],
        "links": connection.execute(
            "SELECT COUNT(*) FROM document_links l JOIN files f ON f.file_id=l.source_file_id WHERE f.run_id = ?",
            (run["run_id"],),
        ).fetchone()[0],
        "api_documents": connection.execute(
            "SELECT COUNT(*) FROM api_documents WHERE run_id = ?", (run["run_id"],)
        ).fetchone()[0],
        "api_members": connection.execute(
            "SELECT COUNT(*) FROM api_members m JOIN api_documents d ON d.api_document_id=m.api_document_id WHERE d.run_id = ?",
            (run["run_id"],),
        ).fetchone()[0],
        "api_baselines": connection.execute(
            "SELECT COUNT(*) FROM accepted_api_baselines WHERE run_id = ?", (run["run_id"],)
        ).fetchone()[0],
    }
    fresh = not reasons
    payload = {
        "exists": True,
        "database": str(database_path),
        "schema_version": int(SCHEMA_VERSION),
        "authority": metadata.get("authority"),
        "run_id": run["run_id"],
        "indexed_at_utc": run["completed_at_utc"],
        "indexed_branch": run["branch"],
        "indexed_head": run["head_commit"],
        "indexed_tree_state": run["worktree_state"],
        "current_branch": current["branch"],
        "current_head": current["head_commit"],
        "current_tree_state": current["worktree_state"],
        "fresh": fresh,
        "clean_current": fresh and run["worktree_state"] == "clean" and current["worktree_state"] == "clean",
        "reasons": reasons,
        "counts": counts,
        "integrity_check": integrity,
        "foreign_key_violations": foreign_keys,
    }
    connection.close()
    return payload


def build_index(root: Path, database_path: Path, configuration: str) -> dict[str, Any]:
    root = root.resolve()
    database_path = database_path.resolve()
    state_before = repository_state(root)
    documents = collect_documents(root)
    connection = open_database(database_path)
    started = utc_now()
    try:
        with connection:
            cursor = connection.execute(
                """
                INSERT INTO inventory_runs(
                    started_at_utc, repository_root, branch, head_commit,
                    worktree_state, scanner_version, notes, tree_fingerprint
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?)
                """,
                (
                    started,
                    str(root),
                    state_before["branch"],
                    state_before["head_commit"],
                    state_before["worktree_state"],
                    SCANNER_VERSION,
                    "Tracked plus non-ignored untracked Markdown/Text and generated managed XML documentation linked to accepted API baselines. Rebuildable local preparation; not repository evidence.",
                    state_before["tree_fingerprint"],
                ),
            )
            run_id = int(cursor.lastrowid)
            markdown_counts = insert_markdown_snapshot(connection, root, run_id, documents)
            api_counts = insert_api_snapshot(connection, root, run_id, configuration)
            state_after = repository_state(root)
            if state_after != state_before:
                raise RuntimeError("Repository state changed while the documentation index was being built.")
            connection.execute(
                "UPDATE inventory_runs SET completed_at_utc = ? WHERE run_id = ?",
                (utc_now(), run_id),
            )
    finally:
        connection.close()
    payload = status_payload(root, database_path)
    if not payload.get("fresh"):
        raise RuntimeError(f"New documentation index is not fresh: {payload.get('reasons')}")
    payload["build"] = {**markdown_counts, **api_counts, "configuration": configuration}
    return payload


def fts_query(value: str) -> str:
    tokens = TOKEN_PATTERN.findall(value)
    if not tokens:
        raise ValueError("Search query contains no searchable token.")
    return " AND ".join(f'"{token.replace(chr(34), chr(34) * 2)}"*' for token in tokens)


def search_index(
    root: Path,
    database_path: Path,
    query: str,
    boundary: str | None,
    kind: str | None,
    source: str,
    limit: int,
    allow_stale: bool,
) -> tuple[dict[str, Any], list[dict[str, Any]]]:
    status = status_payload(root, database_path)
    if not status.get("exists"):
        raise RuntimeError("Documentation index is missing. Run eng/refresh-documentation-index.ps1.")
    if not status.get("fresh") and not allow_stale:
        raise RuntimeError(
            "Documentation index is stale: "
            + "; ".join(status.get("reasons", []))
            + ". Refresh it or pass -AllowStale deliberately."
        )
    match = fts_query(query)
    run_id = int(status["run_id"])
    connection = sqlite3.connect(database_path)
    connection.row_factory = sqlite3.Row
    results: list[dict[str, Any]] = []
    per_source_limit = max(limit * 3, limit)
    if source in {"all", "markdown"}:
        sql = """
            SELECT bm25(document_chunks_fts) AS score,
                   snippet(document_chunks_fts, 1, '[', ']', ' … ', 28) AS snippet,
                   f.path, f.boundary_candidate, f.document_kind, f.lifecycle,
                   f.authority_role, c.heading_path, c.start_line, c.end_line
            FROM document_chunks_fts
            JOIN document_chunks c ON c.chunk_id=document_chunks_fts.rowid
            JOIN files f ON f.file_id=c.file_id
            WHERE document_chunks_fts MATCH ? AND f.run_id=?
        """
        parameters: list[Any] = [match, run_id]
        if boundary:
            sql += " AND lower(coalesce(f.boundary_candidate, '')) = lower(?)"
            parameters.append(boundary)
        if kind:
            sql += " AND lower(coalesce(f.document_kind, '')) = lower(?)"
            parameters.append(kind)
        sql += " ORDER BY score LIMIT ?"
        parameters.append(per_source_limit)
        for row in connection.execute(sql, parameters):
            results.append(
                {
                    "source": "markdown",
                    "score": row["score"],
                    "boundary": row["boundary_candidate"],
                    "authority_role": row["authority_role"],
                    "kind": row["document_kind"],
                    "lifecycle": row["lifecycle"],
                    "path": row["path"],
                    "heading": row["heading_path"],
                    "start_line": row["start_line"],
                    "end_line": row["end_line"],
                    "snippet": row["snippet"],
                }
            )
    if source in {"all", "xml"} and not kind:
        sql = """
            SELECT bm25(api_members_fts) AS score,
                   snippet(api_members_fts, 2, '[', ']', ' … ', 28) AS snippet,
                   d.package_id, d.assembly_name, d.target_framework,
                   d.boundary_key, d.xml_path, b.path AS baseline_path,
                   m.member_id, m.member_kind
            FROM api_members_fts
            JOIN api_members m ON m.api_member_id=api_members_fts.rowid
            JOIN api_documents d ON d.api_document_id=m.api_document_id
            JOIN accepted_api_baselines b ON b.baseline_id=d.baseline_id
            WHERE api_members_fts MATCH ? AND d.run_id=?
        """
        parameters = [match, run_id]
        if boundary:
            sql += " AND lower(d.boundary_key) = lower(?)"
            parameters.append(boundary)
        sql += " ORDER BY score LIMIT ?"
        parameters.append(per_source_limit)
        for row in connection.execute(sql, parameters):
            results.append(
                {
                    "source": "xml-api",
                    "score": row["score"],
                    "boundary": row["boundary_key"],
                    "authority_role": "generated-api-guidance",
                    "package_id": row["package_id"],
                    "assembly_name": row["assembly_name"],
                    "target_framework": row["target_framework"],
                    "member_id": row["member_id"],
                    "member_kind": row["member_kind"],
                    "xml_path": row["xml_path"],
                    "baseline_path": row["baseline_path"],
                    "snippet": row["snippet"],
                }
            )
    connection.close()
    results.sort(key=lambda item: float(item.get("score") or 0.0))
    return status, results[:limit]


def print_status(payload: dict[str, Any]) -> None:
    print(f"Database:     {payload.get('database')}")
    print(f"Authority:    {payload.get('authority', 'local preparation only')}")
    print(f"Fresh:        {payload.get('fresh', False)}")
    print(f"Clean current: {payload.get('clean_current', False)}")
    if payload.get("indexed_head"):
        print(f"Indexed HEAD: {payload.get('indexed_head')} ({payload.get('indexed_tree_state')})")
        print(f"Current HEAD: {payload.get('current_head')} ({payload.get('current_tree_state')})")
    for reason in payload.get("reasons", []):
        print(f"STALE:        {reason}")
    if payload.get("counts"):
        print("Counts:       " + json.dumps(payload["counts"], sort_keys=True))
    if payload.get("integrity_check"):
        print(
            f"SQLite:       integrity={payload['integrity_check']}; "
            f"foreign_keys={payload['foreign_key_violations']}"
        )


def print_results(status: dict[str, Any], results: list[dict[str, Any]]) -> None:
    state = "current-clean" if status.get("clean_current") else (
        "current-dirty" if status.get("fresh") else "stale"
    )
    print(f"Index: {state}; HEAD={status.get('indexed_head')}; authority=local-preparation")
    if status.get("reasons"):
        print("Warnings: " + "; ".join(status["reasons"]))
    if not results:
        print("No matches.")
        return
    for index, result in enumerate(results, start=1):
        if result["source"] == "markdown":
            location = f"{result['path']}:{result['start_line']}"
            heading = f" | {result['heading']}" if result.get("heading") else ""
            print(
                f"{index}. [markdown] boundary={result.get('boundary')} "
                f"authority={result.get('authority_role')} {location}{heading}"
            )
        else:
            print(
                f"{index}. [xml-api] boundary={result.get('boundary')} "
                f"package={result.get('package_id')} target={result.get('target_framework')} "
                f"member={result.get('member_id')}"
            )
            print(f"   xml={result.get('xml_path')} baseline={result.get('baseline_path')}")
        print("   " + normalize_space(result.get("snippet")))


def write_fixture_repository(root: Path) -> None:
    (root / "docs").mkdir(parents=True)
    (root / "eng" / "public-api" / "NekoLib.Sample").mkdir(parents=True)
    project_dir = root / "src" / "Sample" / "NekoLib.Sample"
    (project_dir / "bin" / "Release" / "net9.0").mkdir(parents=True)
    (root / ".gitignore").write_text("**/bin/\n.local/\n", encoding="utf-8")
    (root / "README.md").write_text(
        textwrap.dedent(
            """\
            # Sample repository

            **Kind:** reference

            **Boundary:** global

            **Authority role:** non-normative

            **Indexing:** include

            See the [sample reference](docs/sample.md).
            """
        ),
        encoding="utf-8",
    )
    (root / "docs" / "sample.md").write_text(
        textwrap.dedent(
            """\
            # Sample contract

            **Kind:** reference

            **Boundary:** sample

            **Authority role:** normative

            **Indexing:** include

            ## Translator extension

            Consumers implement the sample translator contract.
            """
        ),
        encoding="utf-8",
    )
    (project_dir / "NekoLib.Sample.csproj").write_text(
        textwrap.dedent(
            """\
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net9.0</TargetFrameworks>
                <IsPackable>true</IsPackable>
                <PackageId>NekoLib.Sample</PackageId>
              </PropertyGroup>
            </Project>
            """
        ),
        encoding="utf-8",
    )
    (root / "eng" / "public-api" / "NekoLib.Sample" / "net9.0.approved.txt").write_text(
        "namespace NekoLib.Sample { public sealed class SampleTranslator { } }\n",
        encoding="utf-8",
    )
    (project_dir / "bin" / "Release" / "net9.0" / "NekoLib.Sample.xml").write_text(
        textwrap.dedent(
            """\
            <?xml version="1.0"?>
            <doc>
              <assembly><name>NekoLib.Sample</name></assembly>
              <members>
                <member name="T:NekoLib.Sample.SampleTranslator">
                  <summary>Provides the sample translator extension.</summary>
                </member>
              </members>
            </doc>
            """
        ),
        encoding="utf-8",
    )
    run_process(["git", "init", "-q"], root)
    run_process(["git", "config", "user.email", "self-test@example.invalid"], root)
    run_process(["git", "config", "user.name", "Documentation Index Self Test"], root)
    run_process(["git", "add", "."], root)
    run_process(["git", "commit", "-q", "-m", "fixture"], root)


def self_test() -> None:
    with tempfile.TemporaryDirectory(prefix="nekolib-doc-index-") as temporary:
        root = Path(temporary)
        write_fixture_repository(root)
        database = root / ".local" / "documentation-migration" / "documentation-index.sqlite3"
        build = build_index(root, database, "Release")
        assert build["fresh"] and build["clean_current"]
        assert build["counts"]["api_documents"] == 1
        assert build["counts"]["api_members"] == 1
        status, results = search_index(
            root, database, "translator extension", None, None, "all", 10, False
        )
        assert status["fresh"]
        assert {item["source"] for item in results} == {"markdown", "xml-api"}
        with (root / "docs" / "sample.md").open("a", encoding="utf-8") as stream:
            stream.write("\nChanged after indexing.\n")
        stale = status_payload(root, database)
        assert not stale["fresh"]
        assert "working-tree fingerprint differs" in stale["reasons"]
    print("Documentation index self-test passed.")


def default_root() -> Path:
    return Path(__file__).resolve().parents[1]


def default_database(root: Path) -> Path:
    return root / ".local" / "documentation-migration" / "documentation-index.sqlite3"


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=default_root())
    parser.add_argument("--database", type=Path)
    subparsers = parser.add_subparsers(dest="command", required=True)
    build = subparsers.add_parser("build", help="Append a current Markdown/XML snapshot.")
    build.add_argument("--configuration", choices=["Debug", "Release"], default="Release")
    status = subparsers.add_parser("status", help="Report freshness, provenance, and integrity.")
    status.add_argument("--json", action="store_true")
    status.add_argument("--require-current", action="store_true")
    search = subparsers.add_parser("search", help="Search current Markdown and XML API entries.")
    search.add_argument("--query", required=True)
    search.add_argument("--boundary")
    search.add_argument("--kind")
    search.add_argument("--source", choices=["all", "markdown", "xml"], default="all")
    search.add_argument("--limit", type=int, default=20)
    search.add_argument("--allow-stale", action="store_true")
    search.add_argument("--json", action="store_true")
    subparsers.add_parser("self-test", help="Exercise schema, freshness, and combined search fixtures.")
    return parser


def main() -> int:
    parser = build_parser()
    arguments = parser.parse_args()
    root = arguments.root.resolve()
    database = (arguments.database or default_database(root)).resolve()
    try:
        if arguments.command == "self-test":
            self_test()
            return 0
        if arguments.command == "build":
            payload = build_index(root, database, arguments.configuration)
            print(json.dumps(payload, indent=2, ensure_ascii=False, sort_keys=True))
            return 0
        if arguments.command == "status":
            payload = status_payload(root, database)
            if arguments.json:
                print(json.dumps(payload, indent=2, ensure_ascii=False, sort_keys=True))
            else:
                print_status(payload)
            if arguments.require_current and not payload.get("fresh"):
                return 3
            return 0
        if arguments.command == "search":
            if arguments.limit < 1 or arguments.limit > 200:
                raise ValueError("--limit must be between 1 and 200.")
            status, results = search_index(
                root,
                database,
                arguments.query,
                arguments.boundary,
                arguments.kind,
                arguments.source,
                arguments.limit,
                arguments.allow_stale,
            )
            if arguments.json:
                print(json.dumps({"status": status, "results": results}, indent=2, ensure_ascii=False))
            else:
                print_results(status, results)
            return 0
        parser.error(f"Unknown command: {arguments.command}")
    except (OSError, RuntimeError, ValueError, sqlite3.Error, ET.ParseError) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
