using System;
using System.Windows;
using System.Windows.Forms;

namespace MouseCopy
{
    public partial class App
    {
        private NotifyIcon _notifyIcon;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ConsoleManager.Show();

            _notifyIcon = new NotifyIcon
            {
                Icon = MouseCopy.Properties.Resources.MyIcon,
                Visible = true
            };

            CreateContextMenu();
        }

        private void CreateContextMenu()
        {
            _notifyIcon.ContextMenuStrip = new ContextMenuStrip();
            _notifyIcon.ContextMenuStrip.Items.Add("Exit").Click += (s, e) => ExitApplication();
        }

        private void ExitApplication()
        {
            //dispose alles
            _notifyIcon.Dispose();
            _notifyIcon = null;
            Current.Shutdown();
        }
    }
}