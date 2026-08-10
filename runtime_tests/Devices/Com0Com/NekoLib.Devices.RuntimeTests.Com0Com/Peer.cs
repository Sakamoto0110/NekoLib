#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Threading;

namespace NekoLib.Devices.RuntimeTests.Com0Com
{
    /// <summary>
    /// The PCB-A wire protocol, written out here rather than shared with the
    /// emulator.
    /// <para/>
    /// The oracle path deliberately reconstructs both protocols instead of
    /// importing the emulator's code, because two implementations agreeing is
    /// the whole value of an independent oracle. The same constants serve the
    /// scenario-owned peer, and that is exactly why a run using the owned peer
    /// is <b>not</b> independent-oracle evidence and says so in its claim
    /// boundaries.
    /// </summary>
    internal static class PcbA
    {
        /// <summary>Latin-1: single-byte, and what the emulator speaks.</summary>
        public static readonly Encoding Latin1 = Encoding.GetEncoding(28591);

        public const string NewLine = "\r\n";

        public const string Ping = "SYS PING;";
        public const string Identify = "SYS ID;";

        public const string Pong = "OK PONG;" + NewLine;
        public const string Identity = "OK PCB-A OWNED-PEER;" + NewLine;

        /// <summary>
        /// An echo request carrying a caller-chosen token.
        /// <para/>
        /// The token is what makes "the next operation received its own
        /// response" a decidable question. Without one, a stale response and a
        /// fresh one are the same bytes and the check cannot tell them apart.
        /// </summary>
        public static string EchoRequest(string token) => "SYS ECHO " + token + ";";

        public static string EchoResponse(string token) => "OK " + token + ";" + NewLine;

        public const string Unknown = "ERR UNKNOWN;" + NewLine;
    }

    /// <summary>
    /// The PCB-B binary frame: <c>A5 5A len seq cmd crcHi crcLo</c>, with
    /// CRC-16/CCITT-FALSE over <c>len</c>, <c>seq</c> and <c>cmd</c>.
    /// </summary>
    internal static class PcbB
    {
        public const byte SyncHigh = 0xA5;
        public const byte SyncLow = 0x5A;
        public const byte BodyLength = 0x02;
        public const byte CommandPing = 0x10;
        public const byte ResponsePong = 0x90;

        /// <summary>Total frame size for the two-byte body both sides use.</summary>
        public const int FrameLength = 7;

        public static byte[] EncodeRequest(byte sequence, byte command) =>
            Encode(sequence, command);

        public static byte[] EncodeResponse(byte sequence, byte opcode) =>
            Encode(sequence, opcode);

        private static byte[] Encode(byte sequence, byte code)
        {
            byte[] frame = new byte[] { SyncHigh, SyncLow, BodyLength, sequence, code, 0x00, 0x00 };
            ushort crc = Crc(frame, 2, 3);
            frame[5] = (byte)(crc >> 8);
            frame[6] = (byte)crc;
            return frame;
        }

        /// <summary>
        /// A frame that is structurally a frame and arithmetically wrong: the
        /// sync bytes and the length are right, so a reader will accept it as a
        /// candidate, and the CRC is deliberately not the one the body implies.
        /// </summary>
        public static byte[] EncodeCorrupt(byte sequence)
        {
            byte[] frame = Encode(sequence, ResponsePong);
            frame[5] ^= 0xFF;
            return frame;
        }

        public static ushort Crc(byte[] data, int offset, int count)
        {
            ushort crc = 0xFFFF;
            for (int i = offset; i < offset + count; i++)
            {
                crc ^= (ushort)(data[i] << 8);
                for (int bit = 0; bit < 8; bit++)
                    crc = (ushort)((crc & 0x8000) != 0 ? (crc << 1) ^ 0x1021 : crc << 1);
            }

            return crc;
        }

        /// <summary>
        /// Validates a response frame the way the oracle path does, returning
        /// the first thing that was wrong rather than throwing, so a check can
        /// record it.
        /// </summary>
        public static string? Validate(byte[]? frame, byte expectedSequence)
        {
            if (frame == null) return "no frame arrived";
            if (frame.Length != FrameLength)
                return "frame length was " + frame.Length.ToString(CultureInfo.InvariantCulture) +
                       ", expected " + FrameLength.ToString(CultureInfo.InvariantCulture);

            if (frame[0] != SyncHigh || frame[1] != SyncLow) return "sync bytes were invalid";
            if (frame[2] != BodyLength) return "body length was invalid";
            if (frame[3] != expectedSequence)
                return "sequence was 0x" + frame[3].ToString("X2", CultureInfo.InvariantCulture) +
                       ", expected 0x" + expectedSequence.ToString("X2", CultureInfo.InvariantCulture);

            if (frame[4] != ResponsePong)
                return "opcode was 0x" + frame[4].ToString("X2", CultureInfo.InvariantCulture) + ", expected PONG";

            ushort expected = Crc(frame, 2, 3);
            ushort actual = (ushort)((frame[5] << 8) | frame[6]);
            if (actual != expected)
                return "CRC-16/CCITT-FALSE was 0x" + actual.ToString("X4", CultureInfo.InvariantCulture) +
                       ", expected 0x" + expected.ToString("X4", CultureInfo.InvariantCulture);

            return null;
        }

        public static string Hex(byte[]? data) =>
            data == null ? "<null>" : BitConverter.ToString(data).Replace("-", " ");
    }

    internal enum PeerKind
    {
        TextPcbA = 0,
        BinaryPcbB = 1
    }

    /// <summary>
    /// The other end of a com0com pair, owned by this scenario.
    /// <para/>
    /// This is E3-PIPE's raw peer applied to serial, and it exists for the same
    /// reason. The suite requires faults of "delay, silence, malformed frame,
    /// disconnect and restart", and the independent emulator cannot be asked to
    /// produce any of them: it lives in another repository with no reference to
    /// NekoLib, and giving it a control channel would make it an accomplice
    /// rather than an oracle. Adding such a channel to <c>NekoLib.Devices</c>
    /// is forbidden outright.
    /// <para/>
    /// So the scenario opens the port the emulator would have held and answers
    /// for itself. The cost is real and accepted: the automated modes and the
    /// oracle pass cannot run at the same time, because both want these ports.
    /// They prove different things and neither replaces the other.
    /// <para/>
    /// Every fault below is a switch on <b>this</b> object. Nothing here reaches
    /// into the transport under test, which only ever sees bytes arriving late,
    /// not at all, in pieces, wrong, or from a port that has gone away.
    /// </summary>
    internal sealed class OwnedPeer : IDisposable
    {
        private readonly object _sync = new object();
        private readonly string _portName;
        private readonly PeerKind _kind;

        private SerialPort? _port;
        private Thread? _pump;
        private volatile bool _running;
        private bool _disposed;

        private volatile int _delayMilliseconds;
        private volatile bool _silent;
        private volatile bool _malformed;
        private volatile int _chunkBytes;
        private volatile int _chunkGapMilliseconds;

        private long _responses;
        private long _bytesRead;
        private long _restarts;

        /// <summary>Upper bound on unframed bytes held while waiting for a terminator.</summary>
        private const int MaxBufferedBytes = 8 * 1024;

        public OwnedPeer(string portName, PeerKind kind)
        {
            _portName = portName;
            _kind = kind;
        }

        public string PortName => _portName;
        public PeerKind Kind => _kind;

        public bool IsOpen
        {
            get { lock (_sync) return _port != null && _port.IsOpen; }
        }

        public long Responses => Interlocked.Read(ref _responses);
        public long BytesRead => Interlocked.Read(ref _bytesRead);
        public long Restarts => Interlocked.Read(ref _restarts);

        /// <summary>Opens the peer port and starts answering. Throws if the port is taken.</summary>
        public void Open()
        {
            lock (_sync) OpenCore();
        }

        private void OpenCore()
        {
            if (_port != null && _port.IsOpen) return;

            SerialPort port = new SerialPort(_portName)
            {
                BaudRate = 115200,
                Parity = Parity.None,
                DataBits = 8,
                StopBits = StopBits.One,
                Handshake = Handshake.None,
                DtrEnable = true,
                RtsEnable = true,
                ReadTimeout = 50,
                WriteTimeout = 2000,
                NewLine = PcbA.NewLine
            };

            try
            {
                port.Open();
                port.DiscardInBuffer();
                port.DiscardOutBuffer();
            }
            catch (Exception)
            {
                // A failed open still leaves a SerialPort holding whatever it
                // acquired. Over a soak's repeated restart faults that is the
                // handle leak this scenario exists to notice.
                try { port.Dispose(); } catch (Exception) { }
                throw;
            }

            _port = port;
            _running = true;

            Thread pump = new Thread(Pump);
            pump.IsBackground = true;
            pump.Name = "e3dev-peer-" + _portName;
            pump.Start();
            _pump = pump;
        }

        /// <summary>
        /// Closes the peer port without disposing the peer: the subject sees the
        /// far end of its pair go away. This is the "connection loss" fault.
        /// </summary>
        public void Disconnect()
        {
            Thread? pump;
            SerialPort? port;

            lock (_sync)
            {
                _running = false;
                pump = _pump;
                port = _port;
                _pump = null;
                _port = null;
            }

            // Joined outside the lock: the pump takes no lock, but a bounded
            // join inside one would still be a lock held across a wait.
            if (pump != null && !pump.Join(TimeSpan.FromSeconds(3)))
            {
                // Falls through deliberately. The pump is a background thread
                // that only reads and writes a port about to be closed; every
                // call in it is inside a catch, so a slow exit costs nothing and
                // the process will not be held open by it.
            }

            Close(port);
        }

        /// <summary>
        /// Brings the peer back on the same port. This is the "restart" fault.
        /// <para/>
        /// Retried briefly, because a port closed milliseconds ago can still
        /// refuse the next open while the driver finishes tearing the handle
        /// down. That is a property of the peer's own plumbing, not of the
        /// transport under test, so absorbing it here is what keeps a driver
        /// hiccup from being reported as a failed recovery.
        /// </summary>
        public void Reconnect()
        {
            lock (_sync)
            {
                for (int attempt = 0; ; attempt++)
                {
                    try
                    {
                        OpenCore();
                        break;
                    }
                    catch (Exception) when (attempt < 3)
                    {
                        Thread.Sleep(300);
                    }
                }

                Interlocked.Increment(ref _restarts);
            }
        }

        /// <summary>
        /// Clears every fault switch and discards whatever is buffered on this
        /// end, so one check cannot inherit another's leftovers.
        /// </summary>
        public void Reset()
        {
            _delayMilliseconds = 0;
            _silent = false;
            _malformed = false;
            _chunkBytes = 0;
            _chunkGapMilliseconds = 0;

            lock (_sync)
            {
                if (_port == null || !_port.IsOpen) return;

                try { _port.DiscardInBuffer(); } catch (Exception) { }
                try { _port.DiscardOutBuffer(); } catch (Exception) { }
            }
        }

        public void Delay(TimeSpan amount) =>
            _delayMilliseconds = (int)Math.Max(0.0, amount.TotalMilliseconds);

        public void Silence(bool silent) => _silent = silent;

        public void Malform(bool malformed) => _malformed = malformed;

        /// <summary>
        /// Splits every response into <paramref name="bytes"/>-sized pieces with
        /// <paramref name="gap"/> between them, which is what "responses
        /// delivered in partial chunks" means on a byte stream.
        /// </summary>
        public void Chunk(int bytes, TimeSpan gap)
        {
            _chunkBytes = bytes;
            _chunkGapMilliseconds = (int)Math.Max(0.0, gap.TotalMilliseconds);
        }

        /// <summary>A one-line description of the faults currently armed.</summary>
        public string DescribeFaults()
        {
            List<string> parts = new List<string>();
            if (_delayMilliseconds > 0) parts.Add("delay=" + _delayMilliseconds + "ms");
            if (_silent) parts.Add("silent");
            if (_malformed) parts.Add("malformed");
            if (_chunkBytes > 0) parts.Add("chunk=" + _chunkBytes + "B/" + _chunkGapMilliseconds + "ms");
            if (!IsOpen) parts.Add("disconnected");

            return parts.Count == 0 ? "none" : string.Join(" ", parts.ToArray());
        }

        private void Pump()
        {
            List<byte> buffer = new List<byte>();

            while (_running)
            {
                try
                {
                    SerialPort? port;
                    lock (_sync) port = _port;

                    if (port == null || !port.IsOpen)
                    {
                        Thread.Sleep(20);
                        continue;
                    }

                    int available = port.BytesToRead;
                    if (available <= 0)
                    {
                        Thread.Sleep(5);
                        continue;
                    }

                    byte[] chunk = new byte[available];
                    int read = port.Read(chunk, 0, available);
                    for (int i = 0; i < read; i++) buffer.Add(chunk[i]);
                    Interlocked.Add(ref _bytesRead, read);

                    byte[]? request;
                    while (TryTakeRequest(buffer, out request))
                        Respond(request!);
                }
                catch (Exception)
                {
                    // A port closed underneath this thread is not an error here:
                    // it is one of the faults. Everything else is equally
                    // uninteresting to a peer whose job is to be misbehaving
                    // hardware.
                    Thread.Sleep(20);
                }
            }
        }

        private bool TryTakeRequest(List<byte> buffer, out byte[]? request)
        {
            request = null;
            if (buffer.Count == 0) return false;

            // Bounded on purpose. Nothing this scenario sends is unframed, but a
            // four-hour soak is long enough for one lost terminator to turn this
            // list into the leak the run exists to detect.
            if (buffer.Count > MaxBufferedBytes) buffer.Clear();

            if (_kind == PeerKind.TextPcbA)
            {
                int terminator = buffer.IndexOf((byte)';');
                if (terminator < 0) return false;

                request = new byte[terminator + 1];
                buffer.CopyTo(0, request, 0, request.Length);
                buffer.RemoveRange(0, request.Length);
                return true;
            }

            // Binary: resynchronise on the sync bytes rather than assuming the
            // stream starts on a frame boundary. A malformed-frame fault or a
            // reconnect can leave a partial frame behind, and a peer that could
            // not resynchronise would stop answering for the rest of the run.
            while (buffer.Count > 0 && buffer[0] != PcbB.SyncHigh)
                buffer.RemoveAt(0);

            if (buffer.Count < 3) return false;
            if (buffer[1] != PcbB.SyncLow)
            {
                buffer.RemoveAt(0);
                return false;
            }

            int total = buffer[2] + 5;
            if (buffer.Count < total) return false;

            request = new byte[total];
            buffer.CopyTo(0, request, 0, total);
            buffer.RemoveRange(0, total);
            return true;
        }

        private void Respond(byte[] request)
        {
            int delay = _delayMilliseconds;
            if (delay > 0) Thread.Sleep(delay);

            if (_silent) return;

            byte[] response = _malformed ? Corrupt(request) : Answer(request);
            if (response.Length == 0) return;

            if (!Write(response)) return;

            Interlocked.Increment(ref _responses);
        }

        private byte[] Answer(byte[] request)
        {
            if (_kind == PeerKind.BinaryPcbB)
            {
                byte sequence = request.Length >= 4 ? request[3] : (byte)0;
                return PcbB.EncodeResponse(sequence, PcbB.ResponsePong);
            }

            string text = PcbA.Latin1.GetString(request);

            if (string.Equals(text, PcbA.Ping, StringComparison.Ordinal))
                return PcbA.Latin1.GetBytes(PcbA.Pong);

            if (string.Equals(text, PcbA.Identify, StringComparison.Ordinal))
                return PcbA.Latin1.GetBytes(PcbA.Identity);

            const string echo = "SYS ECHO ";
            if (text.StartsWith(echo, StringComparison.Ordinal) && text.EndsWith(";", StringComparison.Ordinal))
            {
                string token = text.Substring(echo.Length, text.Length - echo.Length - 1);
                return PcbA.Latin1.GetBytes(PcbA.EchoResponse(token));
            }

            return PcbA.Latin1.GetBytes(PcbA.Unknown);
        }

        /// <summary>
        /// What a malformed response looks like on each side: a binary frame
        /// whose CRC does not match its body, and a text reply that never sends
        /// its terminator.
        /// </summary>
        private byte[] Corrupt(byte[] request)
        {
            if (_kind == PeerKind.BinaryPcbB)
            {
                byte sequence = request.Length >= 4 ? request[3] : (byte)0;
                return PcbB.EncodeCorrupt(sequence);
            }

            return PcbA.Latin1.GetBytes("!!BROKEN");
        }

        private bool Write(byte[] response)
        {
            SerialPort? port;
            lock (_sync) port = _port;

            if (port == null || !port.IsOpen) return false;

            int size = _chunkBytes;
            int gap = _chunkGapMilliseconds;

            try
            {
                if (size <= 0 || size >= response.Length)
                {
                    port.Write(response, 0, response.Length);
                    return true;
                }

                int offset = 0;
                while (offset < response.Length)
                {
                    int count = Math.Min(size, response.Length - offset);
                    port.Write(response, offset, count);
                    offset += count;

                    if (offset < response.Length && gap > 0) Thread.Sleep(gap);
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void Close(SerialPort? port)
        {
            if (port == null) return;

            try { if (port.IsOpen) port.Close(); } catch (Exception) { }
            try { port.Dispose(); } catch (Exception) { }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;
            }

            Disconnect();
        }
    }
}
