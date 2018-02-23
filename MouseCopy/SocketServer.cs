using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets;
using WebSocket = vtortola.WebSockets.WebSocket;

namespace MouseCopy
{
    public sealed class SocketServer
    {
        public const int Port = 47318;

        private WebSocketListener Server { get; }
        private CancellationTokenSource Cancellation { get; } = new CancellationTokenSource();

        public SocketServer()
        {
            Server = new WebSocketListener(new IPEndPoint(IPAddress.Any, Port));
            Server.Standards.RegisterStandard(new WebSocketFactoryRfc6455());
            Server.StartAsync();

            Task.Run(() => Listen(Cancellation.Token));
        }

        public delegate void SocketEventHandler(object sender, SocketEventArgs e);

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
                    var message = await socket.ReadStringAsync(token).ConfigureAwait(false);
                    OnMessage(new SocketEventArgs(socket, message));
                    if (message != null)
                        socket.WriteString(message);
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
            {
                try
                {
                    var socket = await Server.AcceptWebSocketAsync(token).ConfigureAwait(false);
                    Console.WriteLine("Accepting new client");
                    if (socket != null)
                        HandleSocket(socket, token);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error Accepting clients: " + e.GetBaseException().Message);
                }
            }

            Console.WriteLine("Server Stop accepting clients");
        }
    }
}