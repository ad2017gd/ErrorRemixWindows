
using CSCore;
using CSCore.Codecs;
using CSCore.CoreAudioAPI;
using CSCore.SoundOut;
using CSCore.Streams;
using Emgu.CV;
using Emgu.CV.Ocl;
using Shared;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;
using Vanara.Extensions;
using Vanara.PInvoke;
using static System.Net.Mime.MediaTypeNames;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static Vanara.PInvoke.ComCtl32;
using static Vanara.PInvoke.OleAut32.PICTDESC.PICTDEC_UNION;
using static Vanara.PInvoke.User32;

namespace ERW
{
    public static class ERWProgram
    {
        static ERWJson? jsonData;
        public static Bitmap bitmap;

        public static void Main(string[] args)
        {
            using (Process p = Process.GetCurrentProcess())
                p.PriorityClass = ProcessPriorityClass.High;

            TaskDialog.CreateDaddyDialog();

            var random = new Random();
            var tempFileAudio = $"_ErrorRemixWindows_ad2017gd_{random.NextInt64(10000, 100000)}.mp3";
            var tempFileVideo = $"_ErrorRemixWindows_ad2017gd_{random.NextInt64(10000, 100000)}.mp4";
            var audioFullPath = Path.Join(Environment.GetEnvironmentVariable("temp"), tempFileAudio);
            var videoFullPath = Path.Join(Environment.GetEnvironmentVariable("temp"), tempFileVideo);

            var hasVideo = false ;

            var assembly = Assembly.GetExecutingAssembly();

            string jsonDataRaw;

            using (Stream stream = assembly.GetManifestResourceStream("ERW.config.json"))
            {
                if(stream is null)
                {
                    MessageBox(0, "ERW: No config injected!", "ErrorRemixWindows", MB_FLAGS.MB_ICONERROR);
                }
                using (StreamReader reader = new StreamReader(stream))
                    jsonDataRaw = reader.ReadToEnd();
            }

            
            using (Stream stream = assembly.GetManifestResourceStream("ERW.audio.mp3"))
            {
                if (stream is null)
                {
                    MessageBox(0, "ERW: No audio injected!", "ErrorRemixWindows", MB_FLAGS.MB_ICONERROR);
                }
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    var bytes = new byte[stream.Length];
                    reader.Read(bytes, 0, bytes.Length);


                    File.WriteAllBytes(audioFullPath, bytes);
                }
            }

            using (Stream stream = assembly.GetManifestResourceStream("ERW.video.mp4"))
            {
                if (stream is not null)
                {
                    hasVideo = true;
                    using (BinaryReader reader = new BinaryReader(stream))
                    {
                        var bytes = new byte[stream.Length];
                        reader.Read(bytes, 0, bytes.Length);

                        File.WriteAllBytes(videoFullPath, bytes);
                    }
                }
            }



            jsonData = JsonSerializer.Deserialize<ERWJson>(jsonDataRaw);

            if (jsonData is null) return;

            VideoCapture? video = null;
            if(hasVideo)
            {
                video = new VideoCapture(videoFullPath);
            }

            var Reader = CodecFactory.Instance.GetCodec(audioFullPath);
            var AudioOut = new WasapiOut() { Latency = 100, Device = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia) };
            AudioOut.Initialize(Reader);

            var player = new Player(jsonData);


            Thread.Sleep(800);
            player.Playing = true;

            TimeSpan pos = Reader.GetPosition();


            var content = true;
            if (hasVideo)
            {
                video.Set(Emgu.CV.CvEnum.CapProp.PosMsec, 0);
                var dispatcher = Dispatcher.CurrentDispatcher;
                HWND hwnd = Video.LoadWindow();

                
                var gr = Graphics.FromHwnd((nint)hwnd);

                var lastAdjust = DateTime.MinValue;
                Task.Run(() =>
                {

                    double targetFPS = video.Get(Emgu.CV.CvEnum.CapProp.Fps);
                    double targetFrameTime = 1000.0 / targetFPS;
                    var frameCount = video.Get(Emgu.CV.CvEnum.CapProp.FrameCount);
                    var videoTime = frameCount / targetFPS;

                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();
                    stopwatch.Restart();

                    long lastFrameTime = stopwatch.ElapsedMilliseconds;

                    Task.Run(() =>
                    {
                        while (content && player.Playing)
                        {
                            try
                            {
                                
                                gr.DrawImage(bitmap, 0, 0, (int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight);
                                if (Math.Abs(bitmap.Width - SystemParameters.PrimaryScreenWidth) > 32 || Math.Abs(bitmap.Height - SystemParameters.PrimaryScreenHeight) > 32)
                                {
                                    bitmap = bitmap.Resize((int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight, System.Drawing.Drawing2D.InterpolationMode.Low);

                                }
                                bitmap.Dispose();
                            }
                            catch {
                            
                            }
                        }
                    });

                    while (content && player.Playing)
                    {
                        long currentTime = stopwatch.ElapsedMilliseconds;
                        long elapsed = currentTime - lastFrameTime;

                        if (pos.TotalMilliseconds > 500 && DateTime.Now - lastAdjust > TimeSpan.FromSeconds(4))
                        {
                            if (hasVideo)
                            {
                                video.Set(Emgu.CV.CvEnum.CapProp.PosMsec, pos.TotalMilliseconds);
                            }
                            lastAdjust = DateTime.Now;
                        }

                        if (elapsed >= targetFrameTime)
                        {

                            lastFrameTime = currentTime;
                            using (var image = video.QueryFrame())
                            {
                                if (image is null) {
                                    content = false;
                                    break;
                                }
                                bitmap = image.ToBitmap();
                                
                                
                            }
                        }
                        else
                        {
                            int sleepTime = (int)(targetFrameTime - elapsed);
                            if (sleepTime > 12)
                                Thread.Sleep(sleepTime - 1);
                            else
                                Thread.Yield();
                        }
                    }


                });
            }

            player.Play(Reader.GetPosition());

            AudioOut.Play();
            pos = Reader.GetPosition();

            var length = TimeSpan.FromSeconds((double)Reader.Length / Reader.WaveFormat.BytesPerSecond);
            Task.Run(() =>
            {
                Thread.CurrentThread.Priority = ThreadPriority.Highest;
                pos = Reader.GetPosition();
                while (player.Playing && pos < length)
                {
                    pos = Reader.GetPosition();
                    player.OnAudioPositionChanged(pos);
                    Thread.Sleep(1);
                }
                
            });
            player.OnStop += () =>
            {
                player.Playing = false;
                Video.window.Close();

                AudioOut.Stop();
                Reader.Dispose();
                AudioOut.Dispose();
                File.Delete(audioFullPath);
                if (hasVideo)
                {
                    content = false;
                    video.Dispose();

                    File.Delete(videoFullPath);
                }
                Process.GetCurrentProcess().Kill();
            };
            int bRet;
            while ((bRet = GetMessage(out MSG msg)) != 0)
            {
                TranslateMessage(msg);
                DispatchMessage(msg);
            }

            



        }
    }
}