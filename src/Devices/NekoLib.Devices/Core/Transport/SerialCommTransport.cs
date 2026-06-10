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
                ReadTimeout = _port.ReadTimeout,
                WriteTimeout = _port.WriteTimeout,
                NewLine = _port.NewLine,
                PortName = _port.PortName
            };

        /// <inheritdoc/>
        public void Configure(SerialConfig cfg)
        {
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
           

            _port.BaudRate = cfg.BaudRate;
            _port.Parity = cfg.Parity;
            _port.DataBits = cfg.DataBits;
            _port.StopBits = cfg.StopBits;
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
                $"[Transport] Config applied: {cfg.BaudRate}/{cfg.DataBits}/{cfg.Parity}/{cfg.StopBits}, NL='{cfg.NewLine}'");
        }

        /// <inheritdoc/>
        public async Task<ICommTransport> Open(string portName, CancellationToken ct = default)
        {
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
            if(text == null)
                throw new ArgumentNullException(nameof(text));

            var bytes = Encoding.ASCII.GetBytes(text);
            await Write(bytes, 0, bytes.Length, ct);
        }

        /// <inheritdoc/>
        public async Task Write(byte[] data, int offset = 0, int count = -1, CancellationToken ct = default)
        {
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

                    while(sw.ElapsedMilliseconds < timeoutMs)
                    {
                        ct.ThrowIfCancellationRequested();

                        try
                        {
                            var line = _port.ReadLine();
                            sb.Append(line);
                            break;
                        }
                        catch(TimeoutException)
                        {
                            // Keep trying until total timeout
                        }
                    }

                    var result = sb.ToString();
                    Log?.Invoke(LogLevel.Raw, $"[Transport] ReadLine DONE: {LogUtil.Clean(result)}");
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
    }
}
