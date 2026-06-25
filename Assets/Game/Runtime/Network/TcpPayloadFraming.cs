using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ProtectTree.Runtime.Network
{
    internal static class TcpPayloadFraming
    {
        private const int HeaderBytes = 4;

        public const int MaxPayloadBytes = 1024 * 1024;

        public static async Task<byte[]> ReadPayloadAsync(
            NetworkStream stream,
            CancellationToken cancellationToken)
        {
            byte[] header = new byte[HeaderBytes];
            bool hasHeader = await ReadExactAsync(
                stream,
                header,
                allowCleanEnd: true,
                cancellationToken).ConfigureAwait(false);
            if (!hasHeader)
            {
                return null;
            }

            int payloadLength = IPAddress.NetworkToHostOrder(
                BitConverter.ToInt32(header, 0));
            if (payloadLength < 0 || payloadLength > MaxPayloadBytes)
            {
                throw new InvalidDataException(
                    $"TCP payload length is invalid: {payloadLength}.");
            }

            byte[] payload = new byte[payloadLength];
            if (payloadLength == 0)
            {
                return payload;
            }

            await ReadExactAsync(
                stream,
                payload,
                allowCleanEnd: false,
                cancellationToken).ConfigureAwait(false);
            return payload;
        }

        public static async Task WritePayloadAsync(
            NetworkStream stream,
            byte[] payload,
            CancellationToken cancellationToken)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            if (payload.Length > MaxPayloadBytes)
            {
                throw new InvalidDataException(
                    $"TCP payload is too large: {payload.Length} bytes.");
            }

            byte[] header = BitConverter.GetBytes(
                IPAddress.HostToNetworkOrder(payload.Length));
            await stream.WriteAsync(
                header,
                0,
                header.Length,
                cancellationToken).ConfigureAwait(false);
            if (payload.Length > 0)
            {
                await stream.WriteAsync(
                    payload,
                    0,
                    payload.Length,
                    cancellationToken).ConfigureAwait(false);
            }

            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        private static async Task<bool> ReadExactAsync(
            NetworkStream stream,
            byte[] buffer,
            bool allowCleanEnd,
            CancellationToken cancellationToken)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                int read = await stream.ReadAsync(
                    buffer,
                    offset,
                    buffer.Length - offset,
                    cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    if (allowCleanEnd && offset == 0)
                    {
                        return false;
                    }

                    throw new EndOfStreamException(
                        "TCP stream ended before a full payload was received.");
                }

                offset += read;
            }

            return true;
        }
    }
}
