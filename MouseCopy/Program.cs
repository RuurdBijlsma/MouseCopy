using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms.VisualStyles;
using System.Windows.Interop;

namespace MouseCopy
{
    //todo:
    //    detect mouse change and update clipboard
    //    upload to all clients when copy event happens
    internal static class Program
    {
        private static readonly List<string> Servers = new List<string>();
        private static readonly List<SocketClient> SocketClients = new List<SocketClient>();

        private static Timer _timer;
        private static string LocalIp { get; } = LanFinder.GetLocalIpAddress();

        private static void Main(string[] args)
        {
            Initialize();

            Console.ReadKey();
        }

        private static void DetectDeviceChange()
        {
            var handle = Process.GetCurrentProcess().MainWindowHandle;
        
            try
            {
                var source = HwndSource.FromHwnd(handle);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            
//            if (source == null) return;
//            
//            var windowHandle = source.Handle;
//            source.AddHook(HwndHandler);
//            UsbNotification.RegisterUsbDeviceNotification(windowHandle);
        }

        private static IntPtr HwndHandler(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam, ref bool handled)
        {
            if (msg == UsbNotification.WmDevicechange)
            {
                switch ((int)wparam)
                {
                    case UsbNotification.DbtDeviceremovecomplete:
                        Console.WriteLine("removed");
                        break;
                    case UsbNotification.DbtDevicearrival:
                        Console.WriteLine("added");
                        break;
                }
            }

            handled = false;
            return IntPtr.Zero;
        }

        private static async void Initialize()
        {
            var mouseId = await GetMouseId();
            Console.WriteLine(mouseId);
            DetectDeviceChange();

//            var usbWatcher = new UsbWatcher();
//            Task.Run(() => usbWatcher.Listen());
//            usbWatcher.MouseChange += (sender, args) => { Console.WriteLine("change"); };

            var ftpServer = new FtpServer("clipboard", Servers);

            var socketServer = new SocketServer();
            socketServer.Message += async (sender, args) =>
            {
                switch (args.Message.Action)
                {
                    case Action.Connect:
                        await AddServer(args.Message.Text);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            };

            var clipboardManager = new ClipboardManager();
            clipboardManager.Copy += async (sender, eventArgs) =>
            {
                Console.WriteLine("ONCOPY");
                await ftpServer.SetClipboard(mouseId, clipboardManager);
            };

            await UpdateServers();

            Console.WriteLine("DONE");
        }

        private static async Task UpdateServers()
        {
            var currentServers = (await LanFinder.GetServersByPort(SocketServer.Port))
                .Where(server => server != LocalIp);
            var newServers = currentServers.Except(Servers).ToList();

            if (newServers.Count > 0)
                Console.WriteLine($"{newServers.Count} new server(s) found");

            foreach (var server in newServers)
                await AddServer(server, true);
        }

        private static async Task AddServer(string ip, bool initConnection = false)
        {
            if (!Servers.Contains(ip))
            {
                var wsClient = await SocketClient.Connect(ip);

                if (initConnection)
                    await wsClient.Send(new WsMessage {Action = Action.Connect, Text = LocalIp});

                Servers.Add(ip);
                SocketClients.Add(wsClient);
            }
        }

        private static async Task SendToAll(WsMessage message)
        {
            var tasks = SocketClients.Select(client => client.Send(message));

            await Task.WhenAll(tasks);
        }


        private static async Task<string> GetMouseId()
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
                    UseShellExecute = false
                }
            };
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var id = output.Split(new[] {Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries)
                .First(line => line.Contains("DeviceID"))
                .Split(new[] {"DeviceID="}, StringSplitOptions.RemoveEmptyEntries).First();

            return id;
        }
    }
}