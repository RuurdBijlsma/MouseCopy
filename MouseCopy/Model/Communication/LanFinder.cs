using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace MouseCopy.Model.Communication
{
    public static class LanFinder
    {
        private static readonly object LockObj = new object();

        public static string GetLocalIpAddress()
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

        public static async Task<IEnumerable<string>> GetServersByPort(int port)
        {
            Console.WriteLine("Searching for other servers on LAN...");

            var gateway = GetDefaultGateway().ToString().Split('.').Take(3);
            var ipBase = string.Join(".", gateway) + '.';

            var tasks = new List<Task>();
            var ips = new List<string>();
            const int ipRangeStart = 1;
            for (var i = ipRangeStart; i < 255; i++)
            {
                var i1 = i;
                var task = Task.Run(async () =>
                {
                    if (await IsServerUp(ipBase + i1, port))
                        lock (LockObj)
                            ips.Add(ipBase + i1);
                });
                tasks.Add(task);
            }

            await Task.WhenAll(tasks);

            Console.WriteLine("Done searching servers");
            return ips;
        }

        private static async Task<bool> IsServerUp(string server, int port, int timeout = 100)
        {
            var client = new TcpClient {ReceiveTimeout = timeout, SendTimeout = timeout};
            try
            {
                Task.Run(async () =>
                {
                    await Task.Delay(timeout);
                    client.Close();
                    client.Dispose();
                });

                await client.ConnectAsync(server, port);
                
                return true;
            }
            catch
            {
                // ignored
            }
            return false;
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

        private static List<string> GetLocalIps(int timeout = 50)
        {
            var gateway = GetDefaultGateway().ToString().Split('.').Take(3);
            var ipBase = string.Join(".", gateway) + '.';

            var upIps = new List<string>();
            var countdown = new CountdownEvent(1);
            for (var i = 1; i < 255; i++)
            {
                var ip = ipBase + i;

                var p = new Ping();
                p.PingCompleted += (sender, e) =>
                {
                    if (e.Reply != null && e.Reply.Status == IPStatus.Success)
                        lock (LockObj)
                        {
                            upIps.Add(ip);
                        }
                    else if (e.Reply == null)
                        Console.WriteLine("Pinging {0} failed. (Null Reply object?)", ip);

                    countdown.Signal();
                };
                countdown.AddCount();
                p.SendAsync(ip, timeout, ip);
            }

            countdown.Signal();
            countdown.Wait();

            return upIps;
        }
    }
}