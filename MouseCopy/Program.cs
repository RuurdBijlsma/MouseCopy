using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace MouseCopy
{
    internal static class Program
    {
        //todo: Clipboard event opvangen, de metadata in data.json ofzo in ./clipboard/ zetten, paste event opvangen en de server data daar plakken, zien of de muis id hetzelfde blijft
        private static void Main(string[] args)
        {
//            const string clipboardDir = "./clipboard";
//            if (!Directory.Exists(clipboardDir))
//                Directory.CreateDirectory(clipboardDir);
//            Task.Run(() => new SimpleHttpServer("clipboard", 27938));
//            var ips = GetLocalIps();
//            return;

            var withMouse = GetUsbDevices().Select(device => device.DeviceId);
            Console.WriteLine("Unplug your mouse now, when it is unplugged press any key");
            Console.ReadKey();
            var withoutMouse = GetUsbDevices().Select(device => device.DeviceId);
            Console.WriteLine("You can plug your mouse back in");

            Console.WriteLine(withMouse.Except(withoutMouse).First());
        }

        private static IEnumerable<UsbDeviceInfo> GetUsbDevices()
        {
            var devices = new List<UsbDeviceInfo>();

            ManagementObjectCollection collection;
            using (var searcher = new ManagementObjectSearcher(@"Select * From Win32_USBHub"))
                collection = searcher.Get();

            foreach (var device in collection)
            {
                devices.Add(new UsbDeviceInfo(
                    (string) device.GetPropertyValue("DeviceID"),
                    (string) device.GetPropertyValue("PNPDeviceID"),
                    (string) device.GetPropertyValue("Description")
                ));
            }

            collection.Dispose();
            return devices;
        }

        private static readonly object LockObj = new object();

        private static List<string> GetLocalIps()
        {
            var upIps = new List<string>();
            var countdown = new CountdownEvent(1);
            var sw = new Stopwatch();
            sw.Start();
            const string ipBase = "192.168.0.";
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
    }

    internal class UsbDeviceInfo
    {
        public UsbDeviceInfo(string deviceId, string pnpDeviceId, string description)
        {
            DeviceId = deviceId;
            PnpDeviceId = pnpDeviceId;
            Description = description;
        }

        public string DeviceId { get; }
        public string PnpDeviceId { get; }
        public string Description { get; }
    }
}