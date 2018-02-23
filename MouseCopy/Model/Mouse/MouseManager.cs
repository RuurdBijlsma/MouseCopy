using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace MouseCopy.Model.Mouse
{
    public class MouseManager
    {
        public delegate void MouseEventHandler(object sender, MouseEventArgs e);

        public MouseManager(Window window)
        {
            Console.WriteLine("Detecting mouse changes...");
            DetectDeviceChange(window);
            CurrentMouseId = GetMouseId();
        }

        public string CurrentMouseId { get; private set; }

        private void DetectDeviceChange(Window window)
        {
            var handle = new WindowInteropHelper(window).EnsureHandle();

            var source = HwndSource.FromHwnd(handle);

            if (source == null) return;

            var windowHandle = source.Handle;
            source.AddHook(HwndHandler);
            UsbNotification.RegisterUsbDeviceNotification(windowHandle);
        }

        private IntPtr HwndHandler(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam, ref bool handled)
        {
            if (msg == UsbNotification.WmDevicechange)
            {
                var param = (int) wparam;
                // ReSharper disable once ConvertIfStatementToSwitchStatement
                if (param == UsbNotification.DbtDeviceremovecomplete)
                    OnDeviceChange(MouseChangeType.Removed);
                else if (param == UsbNotification.DbtDevicearrival)
                    OnDeviceChange(MouseChangeType.Added);
            }

            handled = false;
            return IntPtr.Zero;
        }

        public event MouseEventHandler MouseChange;

        private void OnDeviceChange(MouseChangeType changeType)
        {
//            Console.WriteLine($"Device changed: {changeType}");

            var newId = GetMouseId();

            if (newId != CurrentMouseId)
            {
//                Console.WriteLine($"Mouse id has changed from {CurrentMouseId} to {newId}");
                CurrentMouseId = newId;
                OnMouseChange(new MouseEventArgs(CurrentMouseId, changeType));
            }
            else
            {
//                Console.WriteLine($"Mouse id hasn't changed {newId}");
            }
        }

        private void OnMouseChange(MouseEventArgs e)
        {
            MouseChange?.Invoke(this, e);
        }

        private static string GetMouseId()
        {
            const string cmd = "wmic path  Win32_PointingDevice get * /FORMAT:Textvaluelist.xsl";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    FileName = "cmd.exe",
                    Arguments = "/C " + cmd,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };
            process.Start();

            //todo when no mouse is attached return "nomouse" ofzo

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var lines = new List<string>();

            process.ErrorDataReceived += (sender, args) => { lines.Add(args.Data); };
            process.OutputDataReceived += (sender, args) => { lines.Add(args.Data); };

            process.WaitForExit(500);

            var valid = lines
                .Where(line => line != null)
                .Any(line => line.Contains("DeviceID"));
            if (!valid)
            {
                return "NoMouse";
            }


            var ids = lines
                .Where(line => line != null)
                .Where(line => line.StartsWith("DeviceID"))
                .Select(line => line.Split(new[] {"DeviceID="}, StringSplitOptions.RemoveEmptyEntries).First())
                .ToList();


            var newIds = ids.Count > 1 ? ids.Except(previousIds).ToList(): ids;
            previousIds = newIds;

            return previousIds.First();
        }

        private static List<string> previousIds = new List<string>();
    }
}