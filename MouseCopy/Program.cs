using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace MouseCopy
{
    internal static class Program
    {
        private static readonly List<string> Servers = new List<string>();
        private static readonly List<SocketClient> SocketClients = new List<SocketClient>();

        private static void Main(string[] args)
        {
            Initialize();

            Console.ReadKey();
        }

        private static async Task UpdateServers()
        {
            var sw = new Stopwatch();
            sw.Start();
            var currentServers = await LanFinder.GetServersByPort(SocketServer.Port);
            Console.WriteLine($"Time taken: {sw.ElapsedMilliseconds}");
            var newServers = currentServers.Except(Servers).ToList();

            var tasks = newServers.Select(SocketClient.Connect);
            SocketClients.AddRange(await Task.WhenAll(tasks));
            Servers.AddRange(newServers);

            SocketClients.ForEach(client => client.Send("Hoe gaat die patat"));
        }


        private static async Task Initialize()
        {
            var mouseId = await GetMouseId();
            Console.WriteLine(mouseId);

            var ftpServer = new FtpServer(LanFinder.GetLocalIpAddress(), "clipboard");
            var socketServer = new SocketServer();
            socketServer.Message += (sender, args) => { Console.WriteLine("Received ws: " + args.Message); };

            var clipboardManager = new ClipboardManager();
            clipboardManager.Copy += async (sender, eventArgs) =>
            {
                Console.WriteLine("ONCOPY");
                await ftpServer.SetClipboard(mouseId, clipboardManager);
            };

            await UpdateServers();

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