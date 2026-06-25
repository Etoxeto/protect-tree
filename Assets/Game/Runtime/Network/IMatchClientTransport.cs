using System;

namespace ProtectTree.Runtime.Network
{
    /// <summary>
    /// Byte transport boundary for a match client.
    /// </summary>
    public interface IMatchClientTransport : IDisposable
    {
        bool IsConnected { get; }

        event Action Connected;

        event Action Disconnected;

        event Action<string> ConnectionFailed;

        event Action<byte[]> MessageReceived;

        void Connect(string address, ushort port);

        void Pump();

        void Send(byte[] payload);

        void Disconnect();
    }
}
