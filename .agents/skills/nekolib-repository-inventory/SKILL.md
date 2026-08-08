---
name: repo-file-inventory
description: Inventory meaningful files in the NekoLib C#/.NET repository. Use when asked to list, inventory, enumerate, count, inspect, or summarize repository files. Count physical files by category, aggregate identical basenames regardless of directory, omit paths and folder names, and evaluate relevant Git-ignored files.
---

# Repository File Inventory

## Purpose

Produce a compact inventory of meaningful files in the current C# framework repository.

The repository contains its own solution and project files.

The inventory is based on **physical files**, but duplicate detection uses **filename only**, ignoring directory location.

Do not produce a directory tree.

---

## Repository Scope

Operate on the entire Git repository.

Resolve the repository root using Git when available.

Do not limit the scan to the current working directory if invoked from a nested directory.

---

## Primary File Policy

Use a **whitelist**.

Only files matching one of the following extensions or exact filenames are eligible.

### C# source

```text
.cs
```

### .NET project and build files

```text
.sln
.slnx
.csproj
.props
.targets
```

### Documentation

```text
.md
.txt
```

### Configuration

```text
.json
.yml
.yaml
.toml
.config
.editorconfig
```

### Scripts and automation

```text
.ps1
.cmd
.bat
.sh
```

### Repository metadata

Include these exact filenames when present:

```text
.gitignore
.gitattributes
.gitmodules
.editorconfig
global.json
NuGet.Config
Directory.Build.props
Directory.Build.targets
Directory.Packages.props
```

Matching is case-insensitive.

Files outside this whitelist are excluded from the inventory.

---

## Git-Ignored Files

Git ignored status is a secondary rule.

Ignored files must not automatically be included or excluded.

An ignored file is eligible only if:

1. it passes the primary whitelist;
2. it appears to be meaningful, human-maintained repository content;
3. it is not generated, transient, machine-specific, or secret-bearing.

### Always exclude generated or transient content

Exclude files originating from typical generated/runtime locations such as:

```text
bin/
obj/
.vs/
TestResults/
coverage/
artifacts/
packages/
BenchmarkDotNet.Artifacts/
```

Also exclude:

* compiler-generated files;
* generated NuGet artifacts;
* generated assembly metadata;
* test execution output;
* coverage output;
* temporary files;
* IDE state;
* local caches;
* package caches.

Directory names are used internally for classification only.

Never show directory names in the final response.

### Ignored files that may still be included

Include an ignored file when it is clearly manually maintained and relevant to understanding the repository, for example:

* local documentation;
* source code intentionally excluded from Git;
* repository scripts;
* templates;
* manually maintained configuration;
* manually maintained test assets.

### Secrets

Do not list secret-bearing or credential-specific filenames if doing so would expose sensitive repository information.

Examples include files clearly representing:

* credentials;
* API keys;
* tokens;
* private keys;
* local user secrets.

---

## Generated C# Files

Do not inventory generated C# source merely because it has a `.cs` extension.

Exclude files that are clearly generated, including common patterns such as:

```text
*.g.cs
*.g.i.cs
*.designer.cs
*.generated.cs
AssemblyInfo.cs
```

Exception:

Include such files when repository evidence indicates they are intentionally hand-maintained source files rather than generated outputs.

Do not assume every `Designer.cs` file is disposable if it is committed source required by WinForms or another designer-based framework.

Tracked, manually maintained designer files should therefore remain included.

---

## Filename Aggregation

Ignore directories when determining duplicate filenames.

Example:

```text
/src/Core/README.md
/src/Navigation/README.md
/tests/README.md
```

is represented as:

```text
README.md: 3x
```

Do not expose any of the source paths.

Filename comparison is case-insensitive.

Therefore:

```text
README.md
readme.md
ReadMe.md
```

are one filename group.

For output casing:

1. prefer the most common casing;
2. on a tie, use the first discovered casing.

---

## File Categories

Classify every eligible physical file into exactly one category.

### Code files

```text
.cs
```

### Project/build files

```text
.sln
.slnx
.csproj
.props
.targets
```

Also include:

```text
global.json
Directory.Build.props
Directory.Build.targets
Directory.Packages.props
NuGet.Config
```

### Documentation files

```text
.md
.txt
```

### Configuration files

```text
.json
.yml
.yaml
.toml
.config
.editorconfig
.gitignore
.gitattributes
.gitmodules
```

### Script files

```text
.ps1
.cmd
.bat
.sh
```

Do not print categories containing zero files.

Category totals count physical files, not unique basenames.

---

## Output Format

Return a compact inventory.

First print category totals.

Example:

Code files: 124
Project/build files: 18
Documentation files: 11
Configuration files: 7
Script files: 3

Do not print categories containing zero files.

After the category totals, print a blank line.

Then print duplicated filenames only.

A duplicated filename is any basename that occurs more than once anywhere in the repository, regardless of directory.

Example:

README.md: 4x
AssemblyInfo.cs: 3x
TODO.md: 2x

Do not list filenames that occur exactly once.

Do not append `1x` to any filename.

For duplicated filenames:

1. sort by occurrence count descending;
2. when counts are equal, sort alphabetically by filename;
3. compare filenames case-insensitively.

Directory location must not affect duplicate detection.

Do not print:

- directories;
- directory names;
- relative paths;
- absolute paths;
- directory trees;
- unique filenames;
- one entry per physical file;
- explanatory commentary;
- headings between category totals and duplicated filenames.

If no duplicated filenames exist, return only the category totals.

The normal response must therefore follow this shape:

Code files: 124
Project/build files: 18
Documentation files: 11
Configuration files: 7
Script files: 3

README.md: 4x
AssemblyInfo.cs: 3x
TODO.md: 2x


## Ordering

Output order:

1. category totals;
2. blank line;
3. duplicated filenames.

Sort duplicated filenames by:

1. occurrence count descending;
2. filename alphabetically.

All sorting is case-insensitive.

---

## Counting Semantics

Category totals represent physical files.

Filename counts represent basename occurrences.

Example:

```text
Core/NavigationService.cs
WinForms/NavigationService.cs
Tests/NavigationServiceTests.cs
```

produces:

```text
Code files: 3

NavigationService.cs: 2x
```

The repository path has no effect on duplicate counting.

---

## Git Enumeration

When Git is available, enumerate all relevant repository content using Git-aware mechanisms.

Consider:

* tracked files;
* untracked files;
* ignored files.

Do not rely only on:

```text
git ls-files
```

because this omits untracked files.

Do not rely only on raw recursive filesystem enumeration because this can flood the inventory with ignored generated content.

Use Git metadata to inform classification.

---

## Execution Order

Perform the inventory in this order:

1. resolve repository root;
2. enumerate tracked, untracked, and relevant ignored files;
3. apply the primary whitelist;
4. exclude generated/transient content;
5. exclude unsafe secret-bearing content;
6. classify remaining physical files;
7. count category totals;
8. group by basename;
9. sort according to the output rules;
10. emit the compact inventory.

---

## Safety

This skill is strictly read-only.

Do not:

* modify repository files;
* create files;
* delete files;
* rename files;
* alter `.gitignore`;
* stage changes;
* commit changes;
* run formatters;
* restore packages;
* build the solution.

Repository inspection commands must not mutate repository state.

---

## Response Contract

Normally return only the inventory.

Example:

```text
Code files: 124
Project/build files: 18
Documentation files: 11
Configuration files: 7
Script files: 3

README.md: 4x
AssemblyInfo.cs: 3x
TODO.md: 2x
```

Do not include paths, directory names, repository trees, or commentary in the normal result.
