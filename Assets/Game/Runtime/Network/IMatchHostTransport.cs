using System;

namespace ProtectTree.Runtime.Network
{
    /// <summary>
    /// Byte transport boundary for the authoritative match host.
    /// </summary>
    public interface IMatchHostTransport : IDisposable
    {
        bool IsRunning { get; }

        event Action<int> ClientConnected;

        event Action<int> ClientDisconnected;

        event Action<int, byte[]> MessageReceived;

        void Start(ushort port, int maxConnections);

        void Pump();

        void Send(int connectionId, byte[] payload);

        void DisconnectClient(int connectionId);

        void Stop();
    }
}
