namespace MouseCopy
{
    public enum Action
    {
        Connect
    }

    public struct WsMessage
    {
        public Action Action;
        public string Text;
    }
}