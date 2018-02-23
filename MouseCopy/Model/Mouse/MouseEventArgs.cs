using System;

namespace MouseCopy.Model.Mouse
{
    public enum MouseChangeType
    {
        Removed,
        Added
    }

    public class MouseEventArgs : EventArgs
    {
        public MouseChangeType ChangeType;
        public string MouseId;

        public MouseEventArgs(string mouseId, MouseChangeType changeType)
        {
            MouseId = mouseId;
            ChangeType = changeType;
        }
    }
}