using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
namespace NekoLib.Pipes
{
    internal static class PipeFraming
    {
        private const int MaxSize = 1024 * 1024;
        
#if NET9
    public static async Task WriteAsync(Stream stream, PipeMessage msg, CancellationToken ct)
    {
        byte[] json = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(msg);
        await WriteCore(stream, json, ct);
    }

    public static async Task<PipeMessage> ReadAsync(Stream stream, CancellationToken ct)
    {
        byte[] payload = await ReadFrame(stream, ct);
        return System.Text.Json.JsonSerializer.Deserialize<PipeMessage>(payload)!;
    }

#else

        public static Task WriteAsync(Stream stream, PipeMessage msg)
        {
            return Task.Run(() =>
            {
                byte[] json = System.Text.Encoding.UTF8.GetBytes(
                    Newtonsoft.Json.JsonConvert.SerializeObject(msg));

                WriteCore(stream, json);
            });
        }

        public static Task<PipeMessage> ReadAsync(Stream stream, CancellationToken ct = default)
        {
            return Task.Run(() =>
            {
                byte[] payload = ReadFrame(stream);
                return Newtonsoft.Json.JsonConvert
                    .DeserializeObject<PipeMessage>(
                        System.Text.Encoding.UTF8.GetString(payload))!;
            });
        }
 
#endif

        // --- Shared Core Methods ---

#if NET9
    private static async Task WriteCore(Stream stream, byte[] json, CancellationToken ct)
    {
        if (json.Length > MaxSize)
            throw new InvalidOperationException("Message too large");

        byte[] length = BitConverter.GetBytes(json.Length);

        await stream.WriteAsync(length, ct);
        await stream.WriteAsync(json, ct);
        await stream.FlushAsync(ct);
    }

    private static async Task<byte[]> ReadFrame(Stream stream, CancellationToken ct)
    {
        byte[] lengthBytes = await ReadExact(stream, 4, ct);
        int size = BitConverter.ToInt32(lengthBytes, 0);

        if (size <= 0 || size > MaxSize)
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

        private static void WriteCore(Stream stream, byte[] json)
        {
            if (json.Length > MaxSize)
                throw new InvalidOperationException("Message too large");

            byte[] length = BitConverter.GetBytes(json.Length);

            stream.Write(length, 0, 4);
            stream.Write(json, 0, json.Length);
            stream.Flush();
        }

        private static byte[] ReadFrame(Stream stream)
        {
            byte[] lengthBytes = ReadExact(stream, 4);
            int size = BitConverter.ToInt32(lengthBytes, 0);

            if (size <= 0 || size > MaxSize)
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


