namespace MouseCopy
{
    public enum Action
    {
        Greet
    }

    public struct WsMessage
    {
        public Action Action;
        public string Text;
    }
}