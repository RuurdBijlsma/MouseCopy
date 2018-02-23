using System;
using vtortola.WebSockets;

namespace MouseCopy.Model.Communication
{
    public class SocketEventArgs : EventArgs
    {
        // Constructor.
        public SocketEventArgs(WebSocket socket, WsMessage message)
        {
            Message = message;
            Socket = socket;
        }

        public WsMessage Message { get; }
        public WebSocket Socket { get; }
    }
}