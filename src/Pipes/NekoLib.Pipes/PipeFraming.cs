using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
namespace NekoLib.Pipes
{
    /// <summary>Thrown when a message exceeds the maximum pipe frame size. Derives from
    /// InvalidOperationException so existing catch sites still match.</summary>
    internal sealed class PipeFrameTooLargeException : InvalidOperationException
    {
        public PipeFrameTooLargeException(int size, int maxSize)
            : base("Message too large: " + size + " > " + maxSize + " bytes.")
        {
        }
    }

    internal static class PipeFraming
    {
        /// <summary>Default maximum framed-message size (1 MiB). Overridable per call
        /// via the <c>maxBytes</c> parameter, surfaced as <c>MaxMessageBytes</c> on the
        /// server/client options.</summary>
        public const int DefaultMaxBytes = 1024 * 1024;

#if NET9
    public static async Task WriteAsync(Stream stream, PipeMessage msg, CancellationToken ct, int maxBytes = DefaultMaxBytes)
    {
        byte[] json = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(msg);
        await WriteCore(stream, json, maxBytes, ct);
    }

    public static async Task<PipeMessage> ReadAsync(Stream stream, CancellationToken ct, int maxBytes = DefaultMaxBytes)
    {
        byte[] payload = await ReadFrame(stream, maxBytes, ct);
        return System.Text.Json.JsonSerializer.Deserialize<PipeMessage>(payload)!;
    }

#else

        public static async Task WriteAsync(Stream stream, PipeMessage msg, CancellationToken ct = default, int maxBytes = DefaultMaxBytes)
        {
            var work = Task.Run(() =>
            {
                byte[] json = System.Text.Encoding.UTF8.GetBytes(
                    Newtonsoft.Json.JsonConvert.SerializeObject(msg));

                WriteCore(stream, json, maxBytes);
            });

            await WithCancellation(work, ct).ConfigureAwait(false);
        }

        public static async Task<PipeMessage> ReadAsync(Stream stream, CancellationToken ct = default, int maxBytes = DefaultMaxBytes)
        {
            var work = Task.Run(() =>
            {
                byte[] payload = ReadFrame(stream, maxBytes);
                return Newtonsoft.Json.JsonConvert
                    .DeserializeObject<PipeMessage>(
                        System.Text.Encoding.UTF8.GetString(payload))!;
            });

            return await WithCancellation(work, ct).ConfigureAwait(false);
        }

        // net481 only: the blocking pipe I/O above runs on a pool thread and cannot
        // observe a CancellationToken directly. These make the *await* honor the
        // token — when it fires the caller gets OperationCanceledException promptly
        // (so PipeClient.RequestTimeout / PipeServer.ClientIdleTimeout finally take
        // effect on net481), and the still-blocked work task is unblocked when the
        // caller disposes the pipe in its finally. net9 uses native ct-aware I/O and
        // never reaches this code.
        private static async Task WithCancellation(Task work, CancellationToken ct)
        {
            if (!ct.CanBeCanceled)
            {
                await work.ConfigureAwait(false);
                return;
            }

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (ct.Register(s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs))
            {
                if (await Task.WhenAny(work, tcs.Task).ConfigureAwait(false) != work)
                {
                    Observe(work);
                    throw new OperationCanceledException(ct);
                }
            }

            await work.ConfigureAwait(false);
        }

        private static async Task<T> WithCancellation<T>(Task<T> work, CancellationToken ct)
        {
            if (!ct.CanBeCanceled)
                return await work.ConfigureAwait(false);

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (ct.Register(s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs))
            {
                if (await Task.WhenAny(work, tcs.Task).ConfigureAwait(false) != work)
                {
                    Observe(work);
                    throw new OperationCanceledException(ct);
                }
            }

            return await work.ConfigureAwait(false);
        }

        // Swallow the eventual fault of an abandoned work task (it throws once the
        // caller disposes the pipe) so it never raises UnobservedTaskException.
        private static void Observe(Task t)
        {
            t.ContinueWith(
                x => { var _ = x.Exception; },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }

#endif

        // --- Shared Core Methods ---

#if NET9
    private static async Task WriteCore(Stream stream, byte[] json, int maxBytes, CancellationToken ct)
    {
        if (json.Length > maxBytes)
            throw new PipeFrameTooLargeException(json.Length, maxBytes);

        byte[] length = BitConverter.GetBytes(json.Length);

        await stream.WriteAsync(length, ct);
        await stream.WriteAsync(json, ct);
        await stream.FlushAsync(ct);
    }

    private static async Task<byte[]> ReadFrame(Stream stream, int maxBytes, CancellationToken ct)
    {
        byte[] lengthBytes = await ReadExact(stream, 4, ct);
        int size = BitConverter.ToInt32(lengthBytes, 0);

        if (size <= 0 || size > maxBytes)
            throw new InvalidDataException();

        return await ReadExact(stream, size, ct);
    }

    private static async Task<byte[]> ReadExact(Stream stream, int size, CancellationToken ct)
    {
        byte[] buffer = new byte[size];
        int read = 0;

        while (read < size)
        {
            int r = await stream.ReadAsync(buffer.AsMemory(read, size - read), ct);
            if (r == 0)
                throw new EndOfStreamException();
            read += r;
        }

        return buffer;
    }

#else

        private static void WriteCore(Stream stream, byte[] json, int maxBytes)
        {
            if (json.Length > maxBytes)
                throw new PipeFrameTooLargeException(json.Length, maxBytes);

            byte[] length = BitConverter.GetBytes(json.Length);

            stream.Write(length, 0, 4);
            stream.Write(json, 0, json.Length);
            stream.Flush();
        }

        private static byte[] ReadFrame(Stream stream, int maxBytes)
        {
            byte[] lengthBytes = ReadExact(stream, 4);
            int size = BitConverter.ToInt32(lengthBytes, 0);

            if (size <= 0 || size > maxBytes)
                throw new InvalidDataException();

            return ReadExact(stream, size);
        }

        private static byte[] ReadExact(Stream stream, int size)
        {
            byte[] buffer = new byte[size];
            int read = 0;

            while (read < size)
            {
                int r = stream.Read(buffer, read, size - read);
                if (r == 0)
                    throw new EndOfStreamException();
                read += r;
            }

            return buffer;
        }

#endif
    }



}


