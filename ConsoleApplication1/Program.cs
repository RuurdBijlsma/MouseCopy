using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.Remoting;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;

namespace MouseCopy
{
    internal static class Program
    {
        //todo: 
        private static void Main(string[] args)
        {
            Initialize();

            Console.ReadKey();
        }

        private static async Task Initialize()
        {
            var mouseId = await GetMouseId();
            Console.WriteLine(mouseId);

            var server = new Server(GetLocalIpAddress(), "clipboard");
            var socketServer = new SocketServer();
            socketServer.Message += (sender, args) => { Console.WriteLine("event message: " + args.Message); };
            
            var socketClient = await SocketClient.Connect("localhost");
            socketClient.Send("Hoe gaat de patat");

//            var otherServers = await GetServers(InterCommunication.Port);
            Console.WriteLine("DONE");


            var clipboardManager = new ClipboardManager();
            clipboardManager.Copy += async (sender, eventArgs) =>
            {
                Console.WriteLine("ONCOPY");
                await server.SetClipboard(mouseId, clipboardManager);
            };
        }

        private static async Task<List<string>> GetServers(int port)
        {
            var ips = GetLocalIps();
            var tasks = new List<Task<bool>>();
            foreach (var ip in ips)
            {
                var task = IsServerUp(ip, port);
                tasks.Add(task);
            }

            var result = await Task.WhenAll(tasks);
            var localIp = GetLocalIpAddress();
            return ips.Where((ip, i) => result[i]).ToList();
//                .Where(ip => ip != localIp).ToList();
        }

        private static string GetLocalIpAddress()
        {
            string localIp;
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                if (socket.LocalEndPoint is IPEndPoint endPoint)
                    localIp = endPoint.Address.ToString();
                else
                    throw new Exception("Could not get local ip adress");
            }

            return localIp;
        }

        private static async Task<bool> IsServerUp(string server, int port, int timeout = 5)
        {
            using (var client = new TcpClient() {ReceiveTimeout = timeout, SendTimeout = timeout})
            {
                try
                {
                    await client.ConnectAsync(server, port);
                    return true;
                }
                catch (Exception e)
                {
                    // ignored
                }

                return false;
            }
        }

        private static IPAddress GetDefaultGateway()
        {
            return NetworkInterface
                .GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties()?.GatewayAddresses)
                .Select(g => g?.Address)
                .FirstOrDefault(a => a != null);
        }

        private static List<string> GetLocalIps()
        {
            var gateway = GetDefaultGateway().ToString().Split('.').Take(3);
            var ipBase = string.Join(".", gateway) + '.';

            var upIps = new List<string>();
            var countdown = new CountdownEvent(1);
            var sw = new Stopwatch();
            sw.Start();
            for (var i = 1; i < 255; i++)
            {
                var ip = ipBase + i;

                var p = new Ping();
                p.PingCompleted += (sender, e) =>
                {
                    if (e.Reply != null && e.Reply.Status == IPStatus.Success)
                        lock (LockObj)
                            upIps.Add(ip);
                    else if (e.Reply == null)
                        Console.WriteLine("Pinging {0} failed. (Null Reply object?)", ip);

                    countdown.Signal();
                };
                countdown.AddCount();
                p.SendAsync(ip, 100, ip);
            }

            countdown.Signal();
            countdown.Wait();
            sw.Stop();

            return upIps;
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

        private static readonly object LockObj = new object();
    }
}