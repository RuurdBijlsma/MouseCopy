using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Timer = System.Threading.Timer;

namespace MouseCopy
{
    internal static class Program
    {
        private static readonly List<string> Servers = new List<string>();
        private static readonly List<SocketClient> SocketClients = new List<SocketClient>();

        private static void Main(string[] args)
        {
            Task.Run(Initialize);

            Console.ReadKey();
        }

        private static async Task UpdateServers()
        {
            var currentServers = await LanFinder.GetServersByPort(SocketServer.Port);
            var newServers = currentServers.Except(Servers).ToList();

            Console.WriteLine($"Updating servers, {newServers.Count} new servers found");

            var tasks = newServers.Select(SocketClient.Connect);
            var newSocketClients = (await Task.WhenAll(tasks)).ToList();

            newSocketClients.ForEach(async client => await client.Send(
                new WsMessage {Action = Action.Greet, Text = "How u doin"}
            ));

            SocketClients.AddRange(newSocketClients);
            Servers.AddRange(newServers);
        }

        private static async Task SendToAll(WsMessage message)
        {
            var tasks = SocketClients.Select(client => client.Send(message));

            await Task.WhenAll(tasks);
        }

        private static Timer _timer;

        private static async Task Initialize()
        {
            var mouseId = await GetMouseId();
            Console.WriteLine(mouseId);

            var ftpServer = new FtpServer(LanFinder.GetLocalIpAddress(), "clipboard");
            var socketServer = new SocketServer();
            socketServer.Message += (sender, args) => { Console.WriteLine("Received WS: " + args.Message.Text); };

            var clipboardManager = new ClipboardManager();
            clipboardManager.Copy += async (sender, eventArgs) =>
            {
                Console.WriteLine("ONCOPY");
                await ftpServer.SetClipboard(mouseId, clipboardManager);
            };

            _timer = new Timer(async state => await UpdateServers(), null, 0, 5000);

            Console.WriteLine("DONE");
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