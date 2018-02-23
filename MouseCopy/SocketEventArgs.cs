using System;
using vtortola.WebSockets;

namespace MouseCopy
{
    public class SocketEventArgs:EventArgs
    {
        public string Message { get; }
        public WebSocket Socket { get; }


        // Constructor.
        public SocketEventArgs(WebSocket socket, string message)
        {
            Message = message;
            Socket = socket;
        }
    }
}