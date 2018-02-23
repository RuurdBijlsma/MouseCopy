using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

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
            Task.Run(Initialize);

            Console.ReadKey();
        }

        private static async Task Initialize()
        {
            var mouseId = await GetMouseId();
            Console.WriteLine(mouseId);

            var ftpServer = new FtpServer("clipboard", Servers);

            await Task.Delay(1000);
            
            var client = new WebClient
            {
                BaseAddress = $"ftp://localhost:{FtpServer.Port}"
            };

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
            var currentServers = await LanFinder.GetServersByPort(SocketServer.Port)
                ;
//                .Where(server => server != LocalIp);
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