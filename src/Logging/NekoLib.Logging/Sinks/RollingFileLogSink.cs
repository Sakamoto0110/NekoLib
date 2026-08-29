using NekoLib.Core.Logging;
using System;
using System.Collections.Generic;
using System.IO;

namespace NekoLib.Logging.Sinks
{
    /// <summary>
    /// Thread-safe rolling file sink. Instances targeting the same normalized
    /// path serialize within this process. Cross-process writers are rejected by
    /// the file share mode and surface as sink failures to the owning logger.
    /// </summary>
    public sealed class RollingFileLogSink : IFlushableLogSink
    {
        private static readonly object PathRegistryGate = new object();
        private static readonly Dictionary<string, object> PathGates =
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        private readonly string _filePath;
        private readonly long _maximumFileBytes;
        private readonly int _retainedFileCount;
        private readonly System.Text.Encoding _encoding;
        private readonly object _pathGate;

        /// <summary>
        /// Creates a sink from a captured options snapshot and joins the
        /// process-wide serialization gate for the normalized path.
        /// </summary>
        /// <param name="options">Required rolling-file options.</param>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> or its encoding is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">The configured file path is blank.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A size or retention bound is below its supported minimum.</exception>
        public RollingFileLogSink(RollingFileLogSinkOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(options.FilePath))
                throw new ArgumentException("FilePath is required.", nameof(options));
            if (options.MaximumFileBytes < 1024)
                throw new ArgumentOutOfRangeException(nameof(options.MaximumFileBytes));
            if (options.RetainedFileCount < 1)
                throw new ArgumentOutOfRangeException(nameof(options.RetainedFileCount));

            _filePath = Path.GetFullPath(options.FilePath);
            _maximumFileBytes = options.MaximumFileBytes;
            _retainedFileCount = options.RetainedFileCount;
            _encoding = options.Encoding ?? throw new ArgumentNullException(nameof(options.Encoding));

            lock (PathRegistryGate)
            {
                object? pathGate;
                if (!PathGates.TryGetValue(_filePath, out pathGate))
                {
                    pathGate = new object();
                    PathGates.Add(_filePath, pathGate);
                }

                _pathGate = pathGate!;
            }
        }

        /// <summary>Gets the absolute file path captured at construction.</summary>
        public string FilePath => _filePath;

        /// <summary>
        /// Appends one formatted line, rotating first when the encoded line would
        /// exceed the configured bound. Error and Fatal entries force a disk flush.
        /// <para/>
        /// The target directory is created when it is missing. The constructor
        /// normalizes the path but never touches the filesystem, so an unusable
        /// location first fails here, as a sink failure the owning pipeline
        /// absorbs.
        /// </summary>
        /// <param name="entry">Entry to persist.</param>
        /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <c>null</c>.</exception>
        public void Write(LogEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            var line = entry + Environment.NewLine;
            var bytes = _encoding.GetByteCount(line);

            lock (_pathGate)
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                if (File.Exists(_filePath))
                {
                    var length = new FileInfo(_filePath).Length;
                    if (length > 0 && length + bytes > _maximumFileBytes)
                        Rotate();
                }

                using (var stream = new FileStream(
                    _filePath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.Read))
                using (var writer = new StreamWriter(stream, _encoding))
                {
                    writer.Write(line);
                    writer.Flush();
                    if (entry.Level >= LogLevel.Error)
                        stream.Flush(true);
                }
            }
        }

        /// <summary>Waits for any in-progress write to this normalized path to finish.</summary>
        public void Flush()
        {
            lock (_pathGate) { }
        }

        private void Rotate()
        {
            var oldest = ArchivePath(_retainedFileCount);
            if (File.Exists(oldest))
                File.Delete(oldest);

            for (int index = _retainedFileCount - 1; index >= 1; index--)
            {
                var source = ArchivePath(index);
                if (File.Exists(source))
                    File.Move(source, ArchivePath(index + 1));
            }

            File.Move(_filePath, ArchivePath(1));
        }

        private string ArchivePath(int index) => _filePath + "." + index;
    }
}
