using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using vtortola.WebSockets;

namespace MouseCopy.Model.Communication
{
    public sealed class SocketServer
    {
        public delegate void SocketEventHandler(object sender, SocketEventArgs e);

        public const int Port = 47318;

        public SocketServer()
        {
            Server = new WebSocketListener(new IPEndPoint(IPAddress.Any, Port));
            Server.Standards.RegisterStandard(new WebSocketFactoryRfc6455());
            Server.StartAsync();

            Task.Run(() => Listen(Cancellation.Token));
        }

        private WebSocketListener Server { get; }
        private CancellationTokenSource Cancellation { get; } = new CancellationTokenSource();

        public event SocketEventHandler Message;

        private void OnMessage(SocketEventArgs e)
        {
            Message?.Invoke(this, e);
        }

        private async Task HandleSocket(WebSocket socket, CancellationToken token)
        {
            try
            {
                while (socket.IsConnected && !token.IsCancellationRequested)
                {
                    var messageString = await socket.ReadStringAsync(token).ConfigureAwait(false);
                    var message = JsonConvert.DeserializeObject<WsMessage>(messageString);
                    OnMessage(new SocketEventArgs(socket, message));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error Handling connection: " + e.GetBaseException().Message);
                try
                {
                    socket.Close();
                }
                catch
                {
                }
            }
            finally
            {
                socket.Dispose();
            }
        }

        private async Task Listen(CancellationToken token)
        {
            Console.WriteLine("Listening...");
            while (!token.IsCancellationRequested)
                try
                {
                    var socket = await Server.AcceptWebSocketAsync(token).ConfigureAwait(false);
                    Console.WriteLine("Accepting new client");
                    if (socket != null)
                        HandleSocket(socket, token);
                }
                catch
                {
                    // ignored
                }

            Console.WriteLine("Server Stop accepting clients");
        }
    }
}