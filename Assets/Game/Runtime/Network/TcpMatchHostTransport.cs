using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ProtectTree.Runtime.Network
{
    public sealed class TcpMatchHostTransport : IMatchHostTransport
    {
        private readonly object _syncRoot = new object();
        private readonly ConcurrentQueue<Action> _mainThreadEvents =
            new ConcurrentQueue<Action>();
        private readonly Dictionary<int, ClientConnection> _connections =
            new Dictionary<int, ClientConnection>();

        private TcpListener _listener;
        private CancellationTokenSource _cancellation;
        private int _nextConnectionId = 1;
        private bool _isRunning;

        public bool IsRunning
        {
            get
            {
                lock (_syncRoot)
                {
                    return _isRunning;
                }
            }
        }

        public event Action<int> ClientConnected;

        public event Action<int> ClientDisconnected;

        public event Action<int, byte[]> MessageReceived;

        public void Start(ushort port, int maxConnections)
        {
            if (maxConnections <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxConnections));
            }

            lock (_syncRoot)
            {
                if (_isRunning)
                {
                    throw new InvalidOperationException(
                        "TCP host transport is already running.");
                }

                _cancellation = new CancellationTokenSource();
                _listener = new TcpListener(IPAddress.Any, port);
                _listener.Start();
                _isRunning = true;

                _ = Task.Run(
                    () => AcceptLoopAsync(maxConnections, _cancellation.Token));
            }
        }

        public void Pump()
        {
            while (_mainThreadEvents.TryDequeue(out Action action))
            {
                action();
            }
        }

        public void Send(int connectionId, byte[] payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            ClientConnection connection;
            CancellationToken cancellationToken;
            lock (_syncRoot)
            {
                if (!_connections.TryGetValue(connectionId, out connection))
                {
                    return;
                }

                cancellationToken = _cancellation.Token;
            }

            _ = Task.Run(() => SendAsync(
                connection,
                payload,
                cancellationToken));
        }

        public void DisconnectClient(int connectionId)
        {
            RemoveConnection(connectionId, notify: true);
        }

        public void Stop()
        {
            List<ClientConnection> connections;
            lock (_syncRoot)
            {
                if (!_isRunning)
                {
                    return;
                }

                _isRunning = false;
                _cancellation.Cancel();
                _listener.Stop();
                connections = new List<ClientConnection>(_connections.Values);
                _connections.Clear();
            }

            foreach (ClientConnection connection in connections)
            {
                connection.Dispose();
                Enqueue(() => ClientDisconnected?.Invoke(connection.Id));
            }

            _cancellation.Dispose();
            _cancellation = null;
            _listener = null;
        }

        public void Dispose()
        {
            Stop();
        }

        private async Task AcceptLoopAsync(
            int maxConnections,
            CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient tcpClient;
                try
                {
                    tcpClient = await _listener.AcceptTcpClientAsync()
                        .ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    continue;
                }

                ClientConnection connection = null;
                lock (_syncRoot)
                {
                    if (_isRunning && _connections.Count < maxConnections)
                    {
                        int connectionId = _nextConnectionId;
                        _nextConnectionId++;
                        connection = new ClientConnection(connectionId, tcpClient);
                        _connections.Add(connectionId, connection);
                    }
                }

                if (connection == null)
                {
                    tcpClient.Close();
                    continue;
                }

                Enqueue(() => ClientConnected?.Invoke(connection.Id));
                _ = Task.Run(() => ReceiveLoopAsync(
                    connection,
                    cancellationToken));
            }
        }

        private async Task ReceiveLoopAsync(
            ClientConnection connection,
            CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    byte[] payload = await TcpPayloadFraming.ReadPayloadAsync(
                        connection.Stream,
                        cancellationToken).ConfigureAwait(false);
                    if (payload == null)
                    {
                        break;
                    }

                    int connectionId = connection.Id;
                    Enqueue(() => MessageReceived?.Invoke(connectionId, payload));
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
                RemoveConnection(connection.Id, notify: true);
            }
        }

        private async Task SendAsync(
            ClientConnection connection,
            byte[] payload,
            CancellationToken cancellationToken)
        {
            try
            {
                await connection.WriteLock.WaitAsync(
                    cancellationToken).ConfigureAwait(false);
                try
                {
                    await TcpPayloadFraming.WritePayloadAsync(
                        connection.Stream,
                        payload,
                        cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    connection.WriteLock.Release();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                RemoveConnection(connection.Id, notify: true);
            }
        }

        private void RemoveConnection(int connectionId, bool notify)
        {
            ClientConnection connection = null;
            lock (_syncRoot)
            {
                if (_connections.TryGetValue(connectionId, out connection))
                {
                    _connections.Remove(connectionId);
                }
            }

            if (connection == null)
            {
                return;
            }

            connection.Dispose();
            if (notify)
            {
                Enqueue(() => ClientDisconnected?.Invoke(connectionId));
            }
        }

        private void Enqueue(Action action)
        {
            _mainThreadEvents.Enqueue(action);
        }

        private sealed class ClientConnection : IDisposable
        {
            private readonly TcpClient _client;

            public ClientConnection(int id, TcpClient client)
            {
                Id = id;
                _client = client;
                Stream = client.GetStream();
                WriteLock = new SemaphoreSlim(1, 1);
            }

            public int Id { get; }

            public NetworkStream Stream { get; }

            public SemaphoreSlim WriteLock { get; }

            public void Dispose()
            {
                WriteLock.Dispose();
                Stream.Dispose();
                _client.Close();
            }
        }
    }
}
