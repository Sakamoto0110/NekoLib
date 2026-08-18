using NekoLib.Devices.Core.Abstractions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NekoLib.Devices.Core.Transport
{
    /// <summary>
    /// Shared implementation for byte-stream transports such as TCP and named pipes.
    /// A background receive pump keeps transport reads independent from command timeouts,
    /// so a timed-out caller never leaves an orphaned stream read consuming a later reply.
    /// </summary>
    public abstract class StreamCommTransport : ICommTransport, IDisposable, IAsyncDisposable
    {
        private const int ReceivePumpShutdownTimeoutMs = 1000;

        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _dataAvailable = new SemaphoreSlim(0, int.MaxValue);
        private readonly object _receiveSync = new object();
        private readonly List<byte> _receiveBuffer = new List<byte>();

        private SerialConfig _config = CreateDefaultConfig();
        private Stream? _stream;
        private CancellationTokenSource? _connectionCts;
        private Task? _receivePump;
        private string? _endpoint;
        private bool _connectionClosed = true;
        private bool _disposed;

        /// <inheritdoc/>
        public HardwareLogHandler? Log { get; set; }

        /// <inheritdoc/>
        public SerialConfig PortInfo
        {
            get
            {
                lock(_receiveSync)
                    return CloneConfig(_config);
            }
        }

        /// <inheritdoc/>
        public string PortName
        {
            get
            {
                lock(_receiveSync)
                    return _endpoint ?? string.Empty;
            }
        }

        /// <inheritdoc/>
        public bool IsOpen
        {
            get
            {
                lock(_receiveSync)
                    return _stream != null && !_connectionClosed;
            }
        }

        /// <summary>
        /// Initializes a stream transport without a preconfigured endpoint.
        /// Call <see cref="Open(string, CancellationToken)"/> or configure
        /// <see cref="SerialConfig.PortName"/> before opening.
        /// </summary>
        protected StreamCommTransport()
        {
        }

        /// <summary>
        /// Initializes a stream transport with a transport-specific endpoint.
        /// </summary>
        protected StreamCommTransport(string endpoint)
        {
            SetEndpoint(endpoint);
        }

        /// <summary>Human-readable transport name used in diagnostics.</summary>
        protected abstract string TransportName { get; }

        /// <summary>Normalizes and validates a transport-specific endpoint.</summary>
        protected abstract string NormalizeEndpoint(string endpoint);

        /// <summary>Creates and connects the underlying stream.</summary>
        protected abstract Task<Stream> ConnectStream(string normalizedEndpoint, CancellationToken ct);

        /// <inheritdoc/>
        public void Configure(SerialConfig cfg)
        {
            ThrowIfDisposed();

            if(cfg == null)
                throw new ArgumentNullException(nameof(cfg));

            ValidateStreamConfig(cfg);

            lock(_receiveSync)
            {
                string? configuredEndpoint = null;
                if(!string.IsNullOrWhiteSpace(cfg.PortName))
                    configuredEndpoint = NormalizeEndpoint(cfg.PortName);

                if(_stream != null && !_connectionClosed &&
                   configuredEndpoint != null &&
                   !string.Equals(_endpoint, configuredEndpoint, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Transport is already open as '{_endpoint}' and cannot be reconfigured to '{configuredEndpoint}'.");
                }

                // The supplied config is caller-owned and is never written back: the
                // resolved endpoint is reported through PortName and PortInfo.
                if(configuredEndpoint != null)
                    _endpoint = configuredEndpoint;

                _config = CloneConfig(cfg);
                _config.NewLine = string.IsNullOrEmpty(cfg.NewLine) ? "\r\n" : cfg.NewLine;
                _config.PortName = _endpoint!;
            }

            Log?.Invoke(LogLevel.Info,
                $"[{TransportName}] Config applied: endpoint='{PortName}', NL='{LogUtil.Clean(PortInfo.NewLine)}'");
        }

        /// <inheritdoc/>
        public async Task<ICommTransport> Open(string portName, CancellationToken ct = default)
        {
            ThrowIfDisposed();

            if(string.IsNullOrWhiteSpace(portName))
                throw new ArgumentException("Endpoint is required.", nameof(portName));

            var endpoint = NormalizeEndpoint(portName);

            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if(IsOpen)
                {
                    if(!string.Equals(PortName, endpoint, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            $"Transport is already open as '{PortName}' and cannot be reopened as '{endpoint}'.");
                    }

                    Log?.Invoke(LogLevel.Debug, $"[{TransportName}] Endpoint already open: {endpoint}");
                    return this;
                }

                await StopConnection().ConfigureAwait(false);
                SetEndpoint(endpoint);
                await OpenCore(endpoint, ct).ConfigureAwait(false);
                return this;
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <inheritdoc/>
        public async Task<ICommTransport> Open(CancellationToken ct = default)
        {
            ThrowIfDisposed();

            string endpoint;
            lock(_receiveSync)
            {
                endpoint = _endpoint ?? string.Empty;
            }

            if(string.IsNullOrWhiteSpace(endpoint))
                throw new InvalidOperationException("Endpoint is required before opening the transport.");

            return await Open(endpoint, ct).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task Close()
        {
            ThrowIfDisposed();

            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                Log?.Invoke(LogLevel.Info, $"[{TransportName}] Closing {PortName}");
                await StopConnection().ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <inheritdoc/>
        public Task Write(string text, CancellationToken ct = default)
        {
            ThrowIfDisposed();

            if(text == null)
                throw new ArgumentNullException(nameof(text));

            var bytes = Encoding.ASCII.GetBytes(text);
            return Write(bytes, 0, bytes.Length, ct);
        }

        /// <inheritdoc/>
        public async Task Write(
            byte[] data,
            int offset = 0,
            int count = -1,
            CancellationToken ct = default)
        {
            ThrowIfDisposed();
            ValidateBufferRange(data, offset, count);

            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var stream = GetOpenStream();
                if(count < 0)
                    count = data.Length - offset;

                Log?.Invoke(LogLevel.Raw,
                    $"[{TransportName}] WRITE: {LogUtil.Hex(data.Skip(offset).Take(count).ToArray())}");

                await stream.WriteAsync(data, offset, count, ct).ConfigureAwait(false);
                await stream.FlushAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <inheritdoc/>
        public async Task<string?> ReadLine(int timeoutMs = 2000, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            ValidateTimeout(timeoutMs, nameof(timeoutMs));

            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                EnsureOpenOrBuffered();

                var terminator = Encoding.ASCII.GetBytes(PortInfo.NewLine);
                if(terminator.Length == 0)
                    terminator = Encoding.ASCII.GetBytes("\r\n");

                var sw = Stopwatch.StartNew();
                while(true)
                {
                    byte[]? line = TryTakeLine(terminator);
                    if(line != null)
                    {
                        var text = Encoding.ASCII.GetString(line);
                        Log?.Invoke(LogLevel.Raw, $"[{TransportName}] ReadLine DONE: {LogUtil.Clean(text)}");
                        return text;
                    }

                    int remaining = Remaining(timeoutMs, sw);
                    if(remaining <= 0 || !await WaitForData(remaining, ct).ConfigureAwait(false))
                    {
                        Log?.Invoke(LogLevel.Raw, $"[{TransportName}] ReadLine DONE: <null>");
                        return null;
                    }
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <inheritdoc/>
        public async Task<byte[]?> ReadExact(
            int length,
            int timeoutMs = 2000,
            CancellationToken ct = default)
        {
            ThrowIfDisposed();

            if(length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "Length cannot be negative.");

            ValidateTimeout(timeoutMs, nameof(timeoutMs));

            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                EnsureOpenOrBuffered();
                if(length == 0)
                    return Array.Empty<byte>();

                var sw = Stopwatch.StartNew();
                while(true)
                {
                    byte[]? result = TryTakeExact(length);
                    if(result != null)
                    {
                        Log?.Invoke(LogLevel.Raw, $"[{TransportName}] ReadExact DONE: {LogUtil.Hex(result)}");
                        return result;
                    }

                    int remaining = Remaining(timeoutMs, sw);
                    if(remaining <= 0 || !await WaitForData(remaining, ct).ConfigureAwait(false))
                    {
                        Log?.Invoke(LogLevel.Raw, $"[{TransportName}] ReadExact DONE: <null>");
                        return null;
                    }
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <inheritdoc/>
        public async Task<byte[]?> ReadAll(
            int timeoutMs = 2000,
            int quietPeriodMs = 100,
            CancellationToken ct = default)
        {
            ThrowIfDisposed();
            ValidateTimeout(timeoutMs, nameof(timeoutMs));
            ValidateTimeout(quietPeriodMs, nameof(quietPeriodMs));

            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                EnsureOpenOrBuffered();

                var result = new List<byte>();
                var overall = Stopwatch.StartNew();
                var quiet = Stopwatch.StartNew();
                bool receivedAny = false;

                while(true)
                {
                    var chunk = TakeAvailable();
                    if(chunk.Length > 0)
                    {
                        result.AddRange(chunk);
                        receivedAny = true;
                        quiet.Restart();
                    }

                    int overallRemaining = Remaining(timeoutMs, overall);
                    if(overallRemaining <= 0)
                        break;

                    if(receivedAny && quiet.ElapsedMilliseconds >= quietPeriodMs)
                        break;

                    if(IsConnectionClosedAndEmpty())
                        break;

                    int wait = receivedAny
                        ? Math.Min(overallRemaining, Remaining(quietPeriodMs, quiet))
                        : overallRemaining;

                    if(wait <= 0 || !await WaitForData(wait, ct).ConfigureAwait(false))
                        break;
                }

                var bytes = result.Count == 0 ? null : result.ToArray();
                Log?.Invoke(LogLevel.Raw,
                    $"[{TransportName}] ReadAll DONE: {LogUtil.Hex(bytes ?? Array.Empty<byte>())}");
                return bytes;
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if(_disposed)
                return;

            await Close().ConfigureAwait(false);
            _disposed = true;
            _gate.Dispose();
            _dataAvailable.Dispose();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if(_disposed)
                return;

            _gate.Wait();
            try
            {
                StopConnection().GetAwaiter().GetResult();
                _disposed = true;
            }
            finally
            {
                _gate.Release();
                _gate.Dispose();
                _dataAvailable.Dispose();
            }
        }

        private async Task OpenCore(string endpoint, CancellationToken ct)
        {
            Log?.Invoke(LogLevel.Info, $"[{TransportName}] Opening {endpoint}");

            try
            {
                var stream = await ConnectStream(endpoint, ct).ConfigureAwait(false);
                var connectionCts = new CancellationTokenSource();

                lock(_receiveSync)
                {
                    _stream = stream;
                    _connectionCts = connectionCts;
                    _connectionClosed = false;
                    _receiveBuffer.Clear();
                    _config.PortName = endpoint;
                }

                _receivePump = Task.Run(
                    () => ReceivePump(stream, connectionCts.Token),
                    CancellationToken.None);

                Log?.Invoke(LogLevel.Info, $"[{TransportName}] OPEN OK");
            }
            catch(Exception ex)
            {
                Log?.Invoke(LogLevel.Error, $"[{TransportName}] OPEN FAIL: {ex.Message}");
                throw;
            }
        }

        private async Task ReceivePump(Stream stream, CancellationToken ct)
        {
            var buffer = new byte[8192];

            try
            {
                while(!ct.IsCancellationRequested)
                {
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false);
                    if(read <= 0)
                        break;

                    lock(_receiveSync)
                    {
                        if(!ReferenceEquals(_stream, stream))
                            return;

                        for(int i = 0; i < read; i++)
                            _receiveBuffer.Add(buffer[i]);
                    }

                    SignalData();
                }
            }
            catch(OperationCanceledException) when(ct.IsCancellationRequested)
            {
            }
            catch(ObjectDisposedException) when(ct.IsCancellationRequested)
            {
            }
            catch(IOException ex)
            {
                Log?.Invoke(LogLevel.Error, $"[{TransportName}] RECEIVE CLOSED: {ex.Message}");
            }
            catch(Exception ex)
            {
                Log?.Invoke(LogLevel.Error, $"[{TransportName}] RECEIVE FAIL: {ex.Message}");
            }
            finally
            {
                lock(_receiveSync)
                {
                    if(ReferenceEquals(_stream, stream))
                        _connectionClosed = true;
                }

                SignalData();
            }
        }

        private async Task StopConnection()
        {
            Stream? stream;
            CancellationTokenSource? connectionCts;
            Task? receivePump;

            lock(_receiveSync)
            {
                stream = _stream;
                connectionCts = _connectionCts;
                receivePump = _receivePump;
                _stream = null;
                _connectionCts = null;
                _receivePump = null;
                _connectionClosed = true;
                _receiveBuffer.Clear();
            }

            if(connectionCts != null)
                connectionCts.Cancel();

            if(stream != null)
                stream.Dispose();

            SignalData();

            if(receivePump != null)
            {
                try
                {
                    var completed = await Task.WhenAny(
                        receivePump,
                        Task.Delay(ReceivePumpShutdownTimeoutMs)).ConfigureAwait(false);

                    if(ReferenceEquals(completed, receivePump))
                    {
                        await receivePump.ConfigureAwait(false);
                    }
                    else
                    {
                        Log?.Invoke(
                            LogLevel.Error,
                            $"[{TransportName}] Receive pump did not stop within " +
                            $"{ReceivePumpShutdownTimeoutMs} ms; connection resources were released.");
                    }
                }
                catch(OperationCanceledException)
                {
                }
                catch(ObjectDisposedException)
                {
                }
            }

            connectionCts?.Dispose();
            DrainSignals();
        }

        private Stream GetOpenStream()
        {
            lock(_receiveSync)
            {
                if(_stream == null || _connectionClosed)
                    throw new InvalidOperationException("Transport not open.");

                return _stream;
            }
        }

        private void EnsureOpenOrBuffered()
        {
            lock(_receiveSync)
            {
                if((_stream == null || _connectionClosed) && _receiveBuffer.Count == 0)
                    throw new InvalidOperationException("Transport not open.");
            }
        }

        private async Task<bool> WaitForData(int timeoutMs, CancellationToken ct)
        {
            if(HasBufferedData())
                return true;

            if(IsConnectionClosedAndEmpty())
                return false;

            bool signaled = await _dataAvailable.WaitAsync(timeoutMs, ct).ConfigureAwait(false);
            return signaled && (HasBufferedData() || !IsConnectionClosedAndEmpty());
        }

        private bool HasBufferedData()
        {
            lock(_receiveSync)
                return _receiveBuffer.Count > 0;
        }

        private bool IsConnectionClosedAndEmpty()
        {
            lock(_receiveSync)
                return _connectionClosed && _receiveBuffer.Count == 0;
        }

        private byte[] TakeAvailable()
        {
            lock(_receiveSync)
            {
                if(_receiveBuffer.Count == 0)
                    return Array.Empty<byte>();

                var result = _receiveBuffer.ToArray();
                _receiveBuffer.Clear();
                return result;
            }
        }

        private byte[]? TryTakeExact(int length)
        {
            lock(_receiveSync)
            {
                if(_receiveBuffer.Count < length)
                    return null;

                var result = _receiveBuffer.GetRange(0, length).ToArray();
                _receiveBuffer.RemoveRange(0, length);
                return result;
            }
        }

        private byte[]? TryTakeLine(byte[] terminator)
        {
            lock(_receiveSync)
            {
                int index = IndexOf(_receiveBuffer, terminator);
                if(index < 0)
                    return null;

                var result = _receiveBuffer.GetRange(0, index).ToArray();
                _receiveBuffer.RemoveRange(0, index + terminator.Length);
                return result;
            }
        }

        private void SetEndpoint(string endpoint)
        {
            if(string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("Endpoint is required.", nameof(endpoint));

            var normalized = NormalizeEndpoint(endpoint);
            lock(_receiveSync)
            {
                _endpoint = normalized;
                _config.PortName = normalized;
            }
        }

        private void SignalData()
        {
            try
            {
                _dataAvailable.Release();
            }
            catch(ObjectDisposedException)
            {
            }
            catch(SemaphoreFullException)
            {
            }
        }

        private void DrainSignals()
        {
            while(_dataAvailable.Wait(0))
            {
            }
        }

        private static int IndexOf(List<byte> source, byte[] value)
        {
            if(value.Length == 0)
                return 0;

            int last = source.Count - value.Length;
            for(int i = 0; i <= last; i++)
            {
                bool match = true;
                for(int j = 0; j < value.Length; j++)
                {
                    if(source[i + j] == value[j])
                        continue;

                    match = false;
                    break;
                }

                if(match)
                    return i;
            }

            return -1;
        }

        private static int Remaining(int timeoutMs, Stopwatch sw)
        {
            if(timeoutMs == 0)
                return 0;

            long remaining = timeoutMs - sw.ElapsedMilliseconds;
            if(remaining <= 0)
                return 0;

            return remaining > int.MaxValue ? int.MaxValue : (int)remaining;
        }

        private static SerialConfig CreateDefaultConfig()
        {
            return new SerialConfig
            {
                BaudRate = 115200,
                Parity = System.IO.Ports.Parity.None,
                DataBits = 8,
                StopBits = System.IO.Ports.StopBits.One,
                ReadTimeout = 50,
                WriteTimeout = 100,
                NewLine = "\r\n"
            };
        }

        private static SerialConfig CloneConfig(SerialConfig source)
        {
            return new SerialConfig
            {
                BaudRate = source.BaudRate,
                Parity = source.Parity,
                DataBits = source.DataBits,
                StopBits = source.StopBits,
                Handshake = source.Handshake,
                DtrEnable = source.DtrEnable,
                RtsEnable = source.RtsEnable,
                ReadTimeout = source.ReadTimeout,
                WriteTimeout = source.WriteTimeout,
                NewLine = source.NewLine,
                PortName = source.PortName
            };
        }

        private static void ValidateStreamConfig(SerialConfig cfg)
        {
            if(cfg.ReadTimeout < -1)
                throw new ArgumentOutOfRangeException(
                    nameof(cfg.ReadTimeout),
                    cfg.ReadTimeout,
                    "ReadTimeout must be -1 (infinite) or greater.");

            if(cfg.WriteTimeout < -1)
                throw new ArgumentOutOfRangeException(
                    nameof(cfg.WriteTimeout),
                    cfg.WriteTimeout,
                    "WriteTimeout must be -1 (infinite) or greater.");
        }

        private static void ValidateBufferRange(byte[] data, int offset, int count)
        {
            if(data == null)
                throw new ArgumentNullException(nameof(data));

            if(offset < 0 || offset > data.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if(count < -1)
                throw new ArgumentOutOfRangeException(nameof(count));

            if(count >= 0 && count > data.Length - offset)
                throw new ArgumentOutOfRangeException(nameof(count));
        }

        private static void ValidateTimeout(int timeoutMs, string paramName)
        {
            if(timeoutMs < 0)
                throw new ArgumentOutOfRangeException(paramName, "Timeout cannot be negative.");
        }

        private void ThrowIfDisposed()
        {
            if(_disposed)
                throw new ObjectDisposedException(GetType().Name);
        }
    }
}
