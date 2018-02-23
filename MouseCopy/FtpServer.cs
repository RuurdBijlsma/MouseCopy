using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

namespace MouseCopy
{
    public class FtpServer
    {
        public const int Port = 30954;
        private const string MetadataFile = "metadata.txt";

        private readonly List<string> _otherServers;

        public FtpServer(string directory, List<string> otherServers)
        {
            Folder = directory;
            if (!Directory.Exists(Folder))
                Directory.CreateDirectory(Folder);

            _otherServers = otherServers;

            CreateServer();
        }

        private string Folder { get; }

        private static async void CreateServer()
        {
            const string dir = "clipboard";
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var ip = new IPEndPoint(IPAddress.Any, Port);
            var server = new Zhaobang.FtpServer.FtpServer(ip, dir);
            await server.RunAsync(CancellationToken.None);
        }

        public async Task SetClipboard(string mouseId, ClipboardManager clipboard)
        {
            FixPath(ref mouseId);
            switch (clipboard.Type)
            {
                case DataType.Text:
                    await CreateTextFile(mouseId, MetadataFile, clipboard.Type + Environment.NewLine + clipboard.Text);
                    break;
                case DataType.Audio:
                    //create wav file
                    break;
                case DataType.Files:
                    //copy files to mouse dir
                    break;
                case DataType.Image:
                    await CreateImageFile(mouseId, "image", clipboard.Image);
                    break;
                case DataType.Unknown:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            CopyToOtherServers(Path.Combine(Folder, mouseId), mouseId);
        }

        private readonly object _lockObject = new object();

        private void CopyToOtherServers(string directory, string destination)
        {
            foreach (var server in _otherServers)
            {
                lock (_lockObject)
                {
                    var client = new WebClient
                    {
                        BaseAddress = "ftp://" + server + ":" + Port
                    };
                    //copy contents of `directory` on local pc to `destination` on ftp
                    UploadDirectory(client, directory, destination);
                }
            }
        }

        private static void UploadDirectory(WebClient client, string localDirectory, string serverDirectory)
        {
            string[] files;
            string[] subDirs;
            try
            {
                files = Directory.GetFiles(localDirectory, "*.*");
                subDirs = Directory.GetDirectories(localDirectory);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw new Exception();
            }

            foreach (var file in files)
            {
                var uri = new Uri(Path.Combine(client.BaseAddress, serverDirectory, Path.GetFileName(file)));
                client.UploadFile(uri, WebRequestMethods.Ftp.UploadFile, file);
            }

            foreach (var subDir in subDirs)
            {
                var uri = Path.Combine(serverDirectory, Path.GetFileName(subDir) ?? throw new ArgumentException());
                CreateDirectory(client.BaseAddress, uri);
                UploadDirectory(client, subDir, uri);
            }
        }

        private static void CreateDirectory(string client, string serverDirectory)
        {
            var request = WebRequest.Create(client + "/" + serverDirectory);
            request.Method = WebRequestMethods.Ftp.MakeDirectory;
            request.GetResponse();
        }

        private async Task CreateImageFile(string mouseId, string baseFileName, Image image)
        {
            CheckDirectory(mouseId);
            var rawImageFormat = image.RawFormat;
            var type = "png";
            if (rawImageFormat.Equals(ImageFormat.Jpeg))
                type = "jpg";
            else if (rawImageFormat.Equals(ImageFormat.Png))
                type = "png";
            else if (rawImageFormat.Equals(ImageFormat.Bmp))
                type = "bmp";
            else if (rawImageFormat.Equals(ImageFormat.Gif))
                type = "gif";

            var fileName = $"{baseFileName}.{type}";
            var path = Path.Combine(Folder, mouseId, fileName);
            image.Save(path);

            await CreateTextFile(mouseId, MetadataFile, DataType.Image + Environment.NewLine + fileName);
        }

        private async Task CreateTextFile(string mouseId, string fileName, string content)
        {
            CheckDirectory(mouseId);
            var path = Path.Combine(Folder, mouseId, fileName);
            using (var sw = File.CreateText(path))
            {
                await sw.WriteAsync(content);
            }
        }

        private void CheckDirectory(string mouseId)
        {
            var path = Path.Combine(Folder, mouseId);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        private static void FixPath(ref string path)
        {
            var invalid = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());

            foreach (var c in invalid) path = path.Replace(c.ToString(), "");
        }
    }
}