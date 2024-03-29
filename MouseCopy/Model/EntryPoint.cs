﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using MouseCopy.Model.Communication;
using MouseCopy.Model.Mouse;
using Action = MouseCopy.Model.Communication.Action;

namespace MouseCopy.Model
{
    //todo:
    //    detect mouse change and update clipboard
    //    upload to all clients when copy event happens
    //    Dont do anything when no mouse is connected
    //    When nothing is on clipboard oncopy keeps firing
    //    ftpserver.setclipboard ook bij startup doen zodat de huidige clipboard erop komt
    //    muis id is anders op laptop
    internal static class EntryPoint
    {
        private static readonly List<string> Servers = new List<string>();
        private static readonly List<SocketClient> SocketClients = new List<SocketClient>();

        private static Timer _timer;
        private static string LocalIp { get; } = LanFinder.GetLocalIpAddress();


        public static async void Initialize(Window window)
        {
            var mouseManager = new MouseManager(window);
            Console.WriteLine("Current mouse id: " + mouseManager.CurrentMouseId);

            var ftpServer = new FtpServer("clipboard", Servers);

            var socketServer = new SocketServer();
            socketServer.Message += async (sender, args) =>
            {
                switch (args.Message.Action)
                {
                    case Action.Connect:
                        await AddServer(args.Message.Text);
                        var mouseId = mouseManager.CurrentMouseId;
                        FtpServer.FixName(ref mouseId);
                        ftpServer.SyncMouse(mouseId);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            };

            var clipboardManager = new ClipboardManager();
            clipboardManager.Copy += async (sender, eventArgs) =>
            {
                Console.WriteLine("ONCOPY");
                await ftpServer.SetClipboard(mouseManager.CurrentMouseId, clipboardManager);
            };
            
            clipboardManager.Ready += async (sender, args) =>
            {
                await ftpServer.SetClipboard(mouseManager.CurrentMouseId, clipboardManager);
            };
            
            mouseManager.MouseChange += (sender, args) =>
            {
                if (args.ChangeType != MouseChangeType.Removed) return;

                Console.WriteLine("Mouse removed, syncing mouse files to everyone: " + args.MouseId);
                // Nieuwe muis connected, override clipboard
                clipboardManager.SetClipboardFromMetadata(ftpServer, args.MouseId);
            };

            await UpdateServers();

            foreach (var server in Servers) Console.WriteLine(server);

            Console.WriteLine("DONE");
        }

        private static async Task UpdateServers()
        {
            var currentServers = (await LanFinder.GetServersByPort(SocketServer.Port))
                .Where(server => server != LocalIp);
            var newServers = currentServers.Except(Servers).ToList();

            foreach (var server in newServers)
                await AddServer(server, true);
        }

        private static async Task AddServer(string ip, bool initConnection = false)
        {
            Console.WriteLine($"Found new server at {ip}");

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
    }
}