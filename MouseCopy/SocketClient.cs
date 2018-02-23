using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;


namespace MouseCopy
{
    public class SocketClient : IDisposable
    {
        private readonly ClientWebSocket _socket;

        private SocketClient(ClientWebSocket socket)
        {
            _socket = socket;
        }

        public void Dispose()
        {
            _socket?.Dispose();
        }

        public static async Task<SocketClient> Connect(string ip)
        {
            var ws = new ClientWebSocket();
            await ws.ConnectAsync(new Uri($"ws://{ip}:{SocketServer.Port}"), CancellationToken.None);

            return new SocketClient(ws);
        }

        public async Task Send(WsMessage message)
        {
            var sendbuffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message)));
            await _socket.SendAsync(sendbuffer, WebSocketMessageType.Text, true, CancellationToken.None)
                .ConfigureAwait(false);
        }
    }
}