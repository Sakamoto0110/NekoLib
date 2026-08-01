using NekoLib.Devices.Core.Abstractions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NekoLib.Devices.Core.Transport
{
    /// <summary>
    /// Provides a thread-safe asynchronous wrapper around <see cref="SerialPort"/>.
    /// All access is serialized via <see cref="SemaphoreSlim"/> to prevent:
    /// - Mixed reads during Write()
    /// - Fragmented responses
    /// - Buffer corruption
    /// </summary>
    public sealed class SerialCommTransport : ICommTransport, IDisposable, IAsyncDisposable
    {
        private readonly SerialPort _port = new SerialPort();
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private bool _hasExplicitPortName;
        private bool _disposed;

        /// <inheritdoc/>
        public HardwareLogHandler Log { get; set; }

        /// <inheritdoc/>
        public string PortName
        {
            get => _port.PortName;
            set
            {
                if(string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("Port name is required.", nameof(value));

                _port.PortName = value;
                _hasExplicitPortName = true;
            }
        }

        /// <inheritdoc/>
        public bool IsOpen => _port.IsOpen;

        /// <summary>
        /// Initializes the transport by optionally providing a default port name.
        /// It will not open automatically until <see cref="Open"/> is called.
        /// </summary>
        public SerialCommTransport(string portName = null)
        {
            if(!string.IsNullOrWhiteSpace(portName))
                PortName = portName;
        }

        /// <inheritdoc/>
        public SerialConfig PortInfo =>
            new SerialConfig
            {
                BaudRate = _port.BaudRate,
                Parity = _port.Parity,
                DataBits = _port.DataBits,
                StopBits = _port.StopBits,
                Handshake = _port.Handshake,
                DtrEnable = _port.DtrEnable,
                RtsEnable = _port.RtsEnable,
                ReadTimeout = _port.ReadTimeout,
                WriteTimeout = _port.WriteTimeout,
                NewLine = _port.NewLine,
                PortName = _port.PortName
            };

        /// <inheritdoc/>
        public void Configure(SerialConfig cfg)
        {
            ThrowIfDisposed();

            if(cfg == null)
                throw new ArgumentNullException(nameof(cfg));

            if(_port.IsOpen)
            {
                if(!string.IsNullOrWhiteSpace(cfg.PortName) &&
                   !string.Equals(_port.PortName, cfg.PortName, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        $"Port is already open as '{_port.PortName}' and cannot be reconfigured to '{cfg.PortName}'.");

                if(string.IsNullOrWhiteSpace(cfg.PortName))
                    cfg.PortName = _port.PortName;

                return;
            }

            ValidateSerialConfig(cfg);

            _port.BaudRate = cfg.BaudRate;
            _port.Parity = cfg.Parity;
            _port.DataBits = cfg.DataBits;
            _port.StopBits = cfg.StopBits;
            _port.Handshake = cfg.Handshake;
            _port.DtrEnable = cfg.DtrEnable;
            _port.RtsEnable = cfg.RtsEnable;
            _port.NewLine = string.IsNullOrEmpty(cfg.NewLine) ? "\r\n" : cfg.NewLine;
            _port.ReadTimeout = cfg.ReadTimeout;
            _port.WriteTimeout = cfg.WriteTimeout;

            if(!string.IsNullOrWhiteSpace(cfg.PortName))
            {
                _port.PortName = cfg.PortName;
                _hasExplicitPortName = true;
            }
            else if(_hasExplicitPortName)
            {
                cfg.PortName = _port.PortName;
            }

            Log?.Invoke(LogLevel.Info,
                $"[Transport] Config applied: {cfg.BaudRate}/{cfg.DataBits}/{cfg.Parity}/{cfg.StopBits}, " +
                $"Handshake={cfg.Handshake}, DTR={cfg.DtrEnable}, RTS={cfg.RtsEnable}, NL='{cfg.NewLine}'");
        }

        /// <inheritdoc/>
        public async Task<ICommTransport> Open(string portName, CancellationToken ct = default)
        {
            ThrowIfDisposed();

            if(string.IsNullOrWhiteSpace(portName))
                throw new ArgumentException("Port name is required.", nameof(portName));

            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if(_port.IsOpen &&
                   !string.Equals(_port.PortName, portName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Port is already open as '{_port.PortName}' and cannot be reopened as '{portName}'.");
                }

                _port.PortName = portName;
                _hasExplicitPortName = true;
                await OpenCore(ct).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }

            return this;
        }

        /// <inheritdoc/>
        public async Task<ICommTransport> Open(CancellationToken ct = default)
        {
            ThrowIfDisposed();

            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if(!_hasExplicitPortName)
                    throw new InvalidOperationException("Port name is required before opening the transport.");

                await OpenCore(ct).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }

            return this;
        }

        /// <inheritdoc/>
        public async Task Close()
        {
            ThrowIfDisposed();

            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                Log?.Invoke(LogLevel.Info, $"[Transport] Closing {PortName}");
                if(_port.IsOpen)
                    _port.Close();
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <inheritdoc/>
        public async Task Write(string text, CancellationToken ct = default)
        {
            ThrowIfDisposed();

            if(text == null)
                throw new ArgumentNullException(nameof(text));

            var bytes = Encoding.ASCII.GetBytes(text);
            await Write(bytes, 0, bytes.Length, ct);
        }

        /// <inheritdoc/>
        public async Task Write(byte[] data, int offset = 0, int count = -1, CancellationToken ct = default)
        {
            ThrowIfDisposed();

            ValidateBufferRange(data, offset, count);

            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if(!_port.IsOpen)
                    throw new InvalidOperationException("Port not open.");

                if(count < 0)
                    count = data.Length - offset;

                Log?.Invoke(LogLevel.Raw, $"[Transport] WRITE: {LogUtil.Hex(data.Skip(offset).Take(count).ToArray())}");

                await Task.Run(() => _port.Write(data, offset, count), ct).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <inheritdoc/>
        public async Task<byte[]> ReadAll(int timeoutMs = 2000, int quietPeriodMs = 100, CancellationToken ct = default)
        {
            ThrowIfDisposed();

            ValidateTimeout(timeoutMs, nameof(timeoutMs));
            ValidateTimeout(quietPeriodMs, nameof(quietPeriodMs));

            await _gate.WaitAsync(ct).ConfigureAwait(false);

            try
            {
                if(!_port.IsOpen)
                    throw new InvalidOperationException("Port not open.");

                Log?.Invoke(LogLevel.Debug,
                    $"[Transport] ReadAll start, timeout={timeoutMs}ms quiet={quietPeriodMs}ms");

                return await Task.Run(() => {
                    var buffer = new List<byte>();
                    var sw = Stopwatch.StartNew();
                    var lastData = Stopwatch.StartNew();

                    while(sw.ElapsedMilliseconds < timeoutMs)
                    {
                        ct.ThrowIfCancellationRequested();

                        int available = _port.BytesToRead;
                        if(available > 0)
                        {
                            var tmp = new byte[available];
                            _port.Read(tmp, 0, available);

                            buffer.AddRange(tmp);
                            lastData.Restart();
                        }
                        else
                        {
                            if(buffer.Count > 0 && lastData.ElapsedMilliseconds >= quietPeriodMs)
                                break;

                            Thread.Sleep(5);
                        }
                    }

                    var result = buffer.Count > 0 ? buffer.ToArray() : null;
                    Log?.Invoke(LogLevel.Raw,
                        $"[Transport] ReadAll DONE: {LogUtil.Hex(result ?? Array.Empty<byte>())}");
                    return result;
                }, ct).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <inheritdoc/>
        public async Task<string> ReadLine(int timeoutMs = 2000, CancellationToken ct = default)
        {
            ThrowIfDisposed();

            ValidateTimeout(timeoutMs, nameof(timeoutMs));

            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if(!_port.IsOpen)
                    throw new InvalidOperationException("Port not open.");

                Log?.Invoke(LogLevel.Debug, $"[Transport] ReadLine start");

                return await Task.Run(() => {
                    var sb = new StringBuilder();
                    var sw = Stopwatch.StartNew();
                    var receivedLine = false;
                    var configuredReadTimeout = _port.ReadTimeout;

                    try
                    {
                        while(sw.ElapsedMilliseconds < timeoutMs)
                        {
                            ct.ThrowIfCancellationRequested();

                            var remaining = timeoutMs - (int)sw.ElapsedMilliseconds;
                            _port.ReadTimeout = Math.Max(1, Math.Min(50, remaining));

                            try
                            {
                                var line = _port.ReadLine();
                                sb.Append(line);
                                receivedLine = true;
                                break;
                            }
                            catch(TimeoutException)
                            {
                                // Use short bounded reads so the method-level timeout and
                                // cancellation token remain authoritative even when the
                                // configured SerialPort timeout is infinite.
                            }
                        }
                    }
                    finally
                    {
                        _port.ReadTimeout = configuredReadTimeout;
                    }

                    var result = receivedLine ? sb.ToString() : null;
                    Log?.Invoke(LogLevel.Raw,
                        result != null
                            ? $"[Transport] ReadLine DONE: {LogUtil.Clean(result)}"
                            : "[Transport] ReadLine DONE: <null>");
                    return result;
                }, ct).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <inheritdoc/>
        public async Task<byte[]> ReadExact(int length, int timeoutMs = 2000, CancellationToken ct = default)
        {
            ThrowIfDisposed();

            if(length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "Length cannot be negative.");

            ValidateTimeout(timeoutMs, nameof(timeoutMs));

            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if(!_port.IsOpen)
                    throw new InvalidOperationException("Port not open.");

                Log?.Invoke(LogLevel.Debug, $"[Transport] ReadExact start, length={length}");

                return await Task.Run(() => {
                    var buffer = new byte[length];
                    int read = 0;
                    var sw = Stopwatch.StartNew();

                    while(read < length && sw.ElapsedMilliseconds < timeoutMs)
                    {
                        ct.ThrowIfCancellationRequested();

                        int avail = _port.BytesToRead;
                        if(avail > 0)
                            read += _port.Read(buffer, read, Math.Min(avail, length - read));
                        else
                            Thread.Sleep(5);
                    }

                    var result = read == length ? buffer : null;
                    Log?.Invoke(LogLevel.Raw, $"[Transport] ReadExact DONE: {LogUtil.Hex(result ?? Array.Empty<byte>())}");
                    return result;
                }, ct).ConfigureAwait(false);
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
            _port.Dispose();
            _gate.Dispose();
            _disposed = true;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if(_disposed)
                return;

            try
            {
                if(_port.IsOpen)
                    _port.Close();
            }
            finally
            {
                _port.Dispose();
                _gate.Dispose();
                _disposed = true;
            }
        }

        private async Task OpenCore(CancellationToken ct)
        {
            if(_port.IsOpen)
            {
                Log?.Invoke(LogLevel.Debug, $"[Transport] Port already open: {PortName}");
                return;
            }

            Log?.Invoke(LogLevel.Info, $"[Transport] Opening {PortName}");

            try
            {
                await Task.Run(() => _port.Open(), ct).ConfigureAwait(false);
                Log?.Invoke(LogLevel.Info, $"[Transport] OPEN OK");
            }
            catch(Exception ex)
            {
                Log?.Invoke(LogLevel.Error, $"[Transport] OPEN FAIL: {ex.Message}");
                throw;
            }
        }

        private static void ValidateBufferRange(byte[] data, int offset, int count)
        {
            if(data == null)
                throw new ArgumentNullException(nameof(data));

            if(offset < 0 || offset > data.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if(count < -1)
                throw new ArgumentOutOfRangeException(nameof(count));

            if(count == -1)
                return;

            if(count > data.Length - offset)
                throw new ArgumentOutOfRangeException(nameof(count));
        }

        private static void ValidateTimeout(int timeoutMs, string paramName)
        {
            if(timeoutMs < 0)
                throw new ArgumentOutOfRangeException(paramName, "Timeout cannot be negative.");
        }

        private static void ValidateSerialConfig(SerialConfig cfg)
        {
            if(cfg.BaudRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(cfg.BaudRate), cfg.BaudRate, "BaudRate must be greater than zero.");

            if(cfg.DataBits < 5 || cfg.DataBits > 8)
                throw new ArgumentOutOfRangeException(nameof(cfg.DataBits), cfg.DataBits, "DataBits must be between 5 and 8.");

            if(!Enum.IsDefined(typeof(StopBits), cfg.StopBits) || cfg.StopBits == StopBits.None)
                throw new ArgumentOutOfRangeException(nameof(cfg.StopBits), cfg.StopBits, "StopBits must be a defined value other than None.");

            if(!Enum.IsDefined(typeof(Handshake), cfg.Handshake))
                throw new ArgumentOutOfRangeException(nameof(cfg.Handshake), cfg.Handshake, "Handshake must be a defined value.");

            if(cfg.ReadTimeout < -1)
                throw new ArgumentOutOfRangeException(nameof(cfg.ReadTimeout), cfg.ReadTimeout, "ReadTimeout must be -1 (infinite) or greater.");

            if(cfg.WriteTimeout < -1)
                throw new ArgumentOutOfRangeException(nameof(cfg.WriteTimeout), cfg.WriteTimeout, "WriteTimeout must be -1 (infinite) or greater.");
        }

        private void ThrowIfDisposed()
        {
            if(_disposed)
                throw new ObjectDisposedException(nameof(SerialCommTransport));
        }
    }
}
