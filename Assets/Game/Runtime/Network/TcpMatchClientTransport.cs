using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ProtectTree.Runtime.Network
{
    public sealed class TcpMatchClientTransport : IMatchClientTransport
    {
        private readonly object _syncRoot = new object();
        private readonly ConcurrentQueue<Action> _mainThreadEvents =
            new ConcurrentQueue<Action>();
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);

        private TcpClient _client;
        private NetworkStream _stream;
        private CancellationTokenSource _cancellation;
        private bool _isConnected;
        private bool _isConnecting;

        public bool IsConnected
        {
            get
            {
                lock (_syncRoot)
                {
                    return _isConnected;
                }
            }
        }

        public event Action Connected;

        public event Action Disconnected;

        public event Action<string> ConnectionFailed;

        public event Action<byte[]> MessageReceived;

        public void Connect(string address, ushort port)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException(
                    "Server address is required.",
                    nameof(address));
            }

            lock (_syncRoot)
            {
                if (_isConnected || _isConnecting)
                {
                    throw new InvalidOperationException(
                        "TCP client transport is already connecting or connected.");
                }

                _isConnecting = true;
                _cancellation = new CancellationTokenSource();
            }

            _ = Task.Run(() => ConnectAsync(
                address,
                port,
                _cancellation.Token));
        }

        public void Pump()
        {
            while (_mainThreadEvents.TryDequeue(out Action action))
            {
                action();
            }
        }

        public void Send(byte[] payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            NetworkStream stream;
            CancellationToken token;
            lock (_syncRoot)
            {
                if (!_isConnected || _stream == null)
                {
                    throw new InvalidOperationException(
                        "TCP client transport is not connected.");
                }

                stream = _stream;
                token = _cancellation.Token;
            }

            _ = Task.Run(() => SendAsync(stream, payload, token));
        }

        public void Disconnect()
        {
            Cleanup(notifyDisconnected: true);
        }

        public void Dispose()
        {
            Cleanup(notifyDisconnected: false);
            _writeLock.Dispose();
        }

        private async Task ConnectAsync(
            string address,
            ushort port,
            CancellationToken cancellationToken)
        {
            TcpClient client = new TcpClient();
            try
            {
                await client.ConnectAsync(address, port).ConfigureAwait(false);
                if (cancellationToken.IsCancellationRequested)
                {
                    client.Close();
                    return;
                }

                lock (_syncRoot)
                {
                    _client = client;
                    _stream = client.GetStream();
                    _isConnected = true;
                    _isConnecting = false;
                }

                Enqueue(() => Connected?.Invoke());
                await ReceiveLoopAsync(_stream, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                client.Close();
                bool shouldNotify;
                lock (_syncRoot)
                {
                    shouldNotify = _isConnecting || _isConnected;
                    _isConnecting = false;
                    _isConnected = false;
                }

                if (shouldNotify && !cancellationToken.IsCancellationRequested)
                {
                    string message = exception.Message;
                    Enqueue(() => ConnectionFailed?.Invoke(message));
                }
            }
        }

        private async Task ReceiveLoopAsync(
            NetworkStream stream,
            CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    byte[] payload = await TcpPayloadFraming.ReadPayloadAsync(
                        stream,
                        cancellationToken).ConfigureAwait(false);
                    if (payload == null)
                    {
                        break;
                    }

                    Enqueue(() => MessageReceived?.Invoke(payload));
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
            }
            finally
            {
                Cleanup(notifyDisconnected: true);
            }
        }

        private async Task SendAsync(
            NetworkStream stream,
            byte[] payload,
            CancellationToken cancellationToken)
        {
            try
            {
                await _writeLock.WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
                try
                {
                    await TcpPayloadFraming.WritePayloadAsync(
                        stream,
                        payload,
                        cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    _writeLock.Release();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                Cleanup(notifyDisconnected: true);
            }
        }

        private void Cleanup(bool notifyDisconnected)
        {
            TcpClient client = null;
            NetworkStream stream = null;
            bool wasConnected;

            lock (_syncRoot)
            {
                wasConnected = _isConnected || _isConnecting;
                _isConnected = false;
                _isConnecting = false;

                _cancellation?.Cancel();
                _cancellation?.Dispose();
                _cancellation = null;

                stream = _stream;
                client = _client;
                _stream = null;
                _client = null;
            }

            stream?.Dispose();
            client?.Close();

            if (notifyDisconnected && wasConnected)
            {
                Enqueue(() => Disconnected?.Invoke());
            }
        }

        private void Enqueue(Action action)
        {
            _mainThreadEvents.Enqueue(action);
        }
    }
}
