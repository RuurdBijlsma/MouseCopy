﻿using System;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;

namespace MouseCopy
{
    public class Server
    {
        private string Folder { get; }
        private const string MetadataFile = "metadata.txt";

        public Server(string directory)
        {
            Folder = directory;
            if (!Directory.Exists(Folder))
                Directory.CreateDirectory(Folder);

            Task.Run(() => new SimpleHttpServer(directory, 27938));
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
                    break;
                case DataType.Files:
                    break;
                case DataType.Image:
                    await CreateTextFile(mouseId, MetadataFile, clipboard.Type.ToString());

                    CheckDirectory(mouseId);
                    var rawImageFormat = clipboard.Image.RawFormat;
                    var type = "png";
                    if (rawImageFormat.Equals(ImageFormat.Jpeg))
                    {
                        type = "jpg";
                    }
                    else if (rawImageFormat.Equals(ImageFormat.Png))
                    {
                        type = "png";
                    }
                    else if (rawImageFormat.Equals(ImageFormat.Bmp))
                    {
                        type = "bmp";
                    }
                    else if (rawImageFormat.Equals(ImageFormat.Gif))
                    {
                        type = "gif";
                    }

                    var imagePath = Path.Combine(Folder, mouseId, "image." + type);
                    clipboard.Image.Save(imagePath);
                    break;
                case DataType.Unknown:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private async Task CreateTextFile(string mouseId, string fileName, string content)
        {
            CheckDirectory(mouseId);
            var path = Path.Combine(Folder, mouseId, fileName);
            using (var sw = File.CreateText(path))
                await sw.WriteAsync(content);
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

            foreach (var c in invalid)
            {
                path = path.Replace(c.ToString(), "");
            }
        }
    }
}