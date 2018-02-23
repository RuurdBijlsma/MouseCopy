using System;
using System.Collections;
using System.Collections.Specialized;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using MouseCopy.Model.Communication;

namespace MouseCopy.Model
{
    public class ClipboardManager
    {
        private bool _firstFire = true;

        public ClipboardManager(int updateInterval = 250)
        {
            Console.WriteLine("Clipboard checker starting...");

            var thread = new Thread(() =>
            {
                while (true)
                {
                    Thread.Sleep(updateInterval);
                    Type = GetClipboardDataType();

                    switch (Type)
                    {
                        case DataType.Text:
                            var text = Clipboard.GetText();
                            if (text != Text)
                            {
                                Text = text;
                                CallCopyEvent();
                            }

                            break;
                        case DataType.Audio:
                            var audio = Clipboard.GetAudioStream();
                            if (!StreamEquals(audio, Audio))
                            {
                                Audio = audio;
                                CallCopyEvent();
                            }

                            break;
                        case DataType.Files:
                            var files = Clipboard.GetFileDropList();
                            if (!FilesEquals(files, Files))
                            {
                                Files = files;
                                CallCopyEvent();
                            }

                            break;
                        case DataType.Image:
                            var image = Clipboard.GetImage();
                            if (!ImageEquals(image, Image))
                            {
                                Image = image;
                                CallCopyEvent();
                            }

                            break;
                        case DataType.Unknown:
                            CallCopyEvent();
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        public string Text { get; private set; }
        public Stream Audio { get; private set; }
        public StringCollection Files { get; private set; }
        public Image Image { get; private set; }
        public DataType Type { get; private set; }

        private static bool StreamEquals(Stream streamA, Stream streamB)
        {
            if (streamA == null || streamB == null)
                return false;

            return streamA.GetHashCode() == streamB.GetHashCode();
        }

        private static bool ImageEquals(Image imageA, Image imageB)
        {
            if (imageA == null || imageB == null)
                return false;

            if (imageA.Width != imageB.Width)
                return false;
            if (imageA.Height != imageB.Height)
                return false;
            if (imageA.PixelFormat != imageB.PixelFormat)
                return false;
            if (imageA.Flags != imageB.Flags)
                return false;
            if (Math.Abs(imageA.HorizontalResolution - imageB.HorizontalResolution) > 0.01)
                return false;
            if (Math.Abs(imageA.VerticalResolution - imageB.VerticalResolution) > 0.01)
                return false;

            var bitmapA = new Bitmap(imageA);
            var bitmapB = new Bitmap(imageB);

            for (var y = 0; y < imageA.Height; y += (int) Math.Floor(imageA.Height / 8f))
            for (var x = 0; x < imageA.Width; x += (int) Math.Floor(imageA.Width / 8f))
                if (bitmapA.GetPixel(x, y) != bitmapB.GetPixel(x, y))
                    return false;

            return true;
        }

        private static bool FilesEquals(IEnumerable filesA, IEnumerable filesB)
        {
            if (filesA == null || filesB == null)
                return false;

            return filesA.Cast<string>().SequenceEqual(filesB.Cast<string>());
        }

        private static DataType GetClipboardDataType()
        {
            var type = DataType.Unknown;

            if (Clipboard.ContainsAudio())
                type = DataType.Audio;
            else if (Clipboard.ContainsFileDropList())
                type = DataType.Files;
            else if (Clipboard.ContainsImage())
                type = DataType.Image;
            else if (Clipboard.ContainsText()) type = DataType.Text;

            return type;
        }

        private void CallCopyEvent()
        {
            if (_firstFire)
                _firstFire = false;
            else
                OnCopy(new EventArgs());
        }

        public event EventHandler Copy;

        protected void OnCopy(EventArgs e)
        {
            Copy?.Invoke(this, e);
        }

        public void SetClipboardFromMetadata(FtpServer server, string mouseId)
        {
            FtpServer.FixName(ref mouseId);
            var clipboardFolder = Path.Combine(server.Folder, mouseId);
            Console.WriteLine(clipboardFolder);
            var files = Directory.GetFiles(clipboardFolder, "*.*");
            var metadataFile = files.First(file => Path.GetFileName(file) == FtpServer.MetadataFile);
            var metadata = File.ReadAllText(metadataFile);
            var metadataLines = metadata.Split(new[] {Environment.NewLine}, StringSplitOptions.None);
            Enum.TryParse(metadataLines.First(), out DataType dataType);
            switch (dataType)
            {
                case DataType.Text:
                    var text = string.Join(Environment.NewLine, metadataLines.Skip(1));
                    SetClipboard(text);
                    break;
                case DataType.Audio:
                    // Todo audio wav stream pakken en op clipboard zetten
                    break;
                case DataType.Files:
                    // Todo files in stringcollection zetten en op clipaboar dzetten
                    break;
                case DataType.Image:
                    var imageFile =Path.Combine(clipboardFolder, metadataLines.First());
                    var image = Image.FromFile(imageFile);
                    SetClipboard(image);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static void SetClipboard(Image image)
        {
            Clipboard.SetImage(image);
        }

        private static void SetClipboard(string text)
        {
            Clipboard.SetText(text);
        }

        private void SetClipboard(Stream audio)
        {
            Clipboard.SetAudio(audio);
        }

        private void SetClipboard(StringCollection files)
        {
            Clipboard.SetFileDropList(files);
        }
    }
}