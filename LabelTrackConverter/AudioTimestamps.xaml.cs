using CSCore;
using CSCore.Codecs;
using CSCore.CoreAudioAPI;
using CSCore.SoundOut;
using ERW;
using Microsoft.Win32;
using ModernWpf;
using NAudio.Utils;
using PropertyChanged;
using Shared;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using static Vanara.PInvoke.ComCtl32;

namespace ERWEditor
{
    public class TimestampConverter : IMultiValueConverter
    {

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] == DependencyProperty.UnsetValue) return new List<string>();

            TimeSpan timespan = (TimeSpan)values[0];
            double zoom = (double)values[1];

            return Enumerable.Repeat(1, (int)(timespan.TotalSeconds / zoom)).Select((_, i) => (i * zoom).ToString("N2"));
        }


        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ButtonAlignmentConverter : IMultiValueConverter
    {

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            return null;

        }


        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public static class TimeUtil
    {
        public const double OFF = 10;
        public const double MULT = 48;

        public static double TimeToPosition(TimeSpan time, double zoom)
        {
            return 1 / zoom * time.TotalSeconds * (MULT + OFF / zoom);
        }

        public static double PositionToTime(double position, double zoom)
        {
            return position * zoom / (MULT + OFF / zoom);
        }


    }

    public class TextCenterConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return new Thickness(-(((double)value) / 2), 0, 0, 0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class TimestampSelectedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return MainWindow.Instance.SelectedTimestamp == value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CursorConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] == DependencyProperty.UnsetValue) return new Thickness(0);
            double zoom = (double)values[1];

            if (values[0] is TimeSpan currentTime)
            {
                // cursor

                return new Thickness(TimeUtil.TimeToPosition(currentTime, zoom), 0, 0, 0);
            } else if (values[0] is string position)
            {
                currentTime = TimeSpan.FromSeconds(double.Parse(position));
                // timestamp text/lines

                return new Thickness(TimeUtil.TimeToPosition(TimeSpan.FromSeconds(double.Parse(position)), zoom),0,0,0);
            }

            return 0;
            
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }



    /// <summary>
    /// Interaction logic for AudioTimestamps.xaml
    /// </summary>
    public partial class AudioTimestamps : UserControl, INotifyPropertyChanged
    {

        public bool IsPreview { get; set; } = false;
        private List<TaskDialog> opened = new List<TaskDialog>();
        private IEnumerator<ERWTimestamp> enumerator;
        private ERWTimestamp last;

        [DependsOn(nameof(MainWindow.Config))]
        public ERWJson Config { get => MainWindow.Config; }

        public AudioTimestamps()
        {
            InitializeComponent();
            this.DataContext = this;
            AudioPositionPropertyChangedTimer.Interval = new TimeSpan(0, 0, 0, 0, 20);
            AudioPositionPropertyChangedTimer.Tick += (_, _) =>
            {
                OnPropertyChanged(nameof(AudioPosition));
                OnAudioPositionChanged();
            };
            AudioPositionPropertyChangedTimer.Start();
            MainWindow.Config.PropertyChanged += Config_PropertyChanged;
            TimestampsGrid.MouseDown += TimestampsGrid_MouseDown;
            TimestampsGrid.MouseUp += TimestampsGrid_MouseUp;
        }


        bool wasPressed = false;

        private void TimestampsGrid_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Released && e.RightButton == MouseButtonState.Released)
            {
                wasPressed = false;
            }
        }

        private void TimestampsGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if ((e.LeftButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed) && !wasPressed)
            {
                Point pos = e.GetPosition(TimestampsGrid);
                if (Reader is null) return;
                Reader.SetPosition(TimeSpan.FromSeconds(TimeUtil.PositionToTime(pos.X, Zoom)));

                wasPressed = true;
            }
        }

        [OnChangedMethod(nameof(GenWaveformsAsync))]
        public double Zoom { get; set; } = 0.5;



        public WasapiOut AudioOut { get; set; }

        public TimeSpan AudioPosition { get {
                if(Reader is not null)
                {
                    try
                    {
                        return Reader.GetPosition();
                    } catch
                    {
                        return TimeSpan.Zero;
                    }
                }
                return TimeSpan.Zero;
            } }


        public void OnAudioPositionChanged()
        {
            if (last is null) return;
            if(AudioPosition > last.Timestamp)
            {
                var actual = last;
                enumerator.MoveNext();
                last = enumerator.Current;


                var random = new Random();
                switch (actual.Action)
                {
                    case ERWActionEnum.ShowWindow:
                        {
                            var act = actual.Data as ERWShowWindow;
                            Task.Run(() =>
                            {
                                var wnd = WindowConverter.FindWindow(act.WindowIdentifier);
                                if (wnd is null) return;
                                var task = new ERW.TaskDialog(wnd, act);
                                opened.Add(task);
                                task.WaitAsyncForHWND().ContinueWith((t) =>
                                {
                                    if (act.IsRelative)
                                    {
                                        task.MoveRelative(act.X, act.Y, act.XAxis, act.YAxis, act.SelfXAxis, act.SelfYAxis);
                                    }
                                    else
                                    {
                                        task.Move(act.X, act.Y);
                                    }
                                });

                            });
                            break;
                        }
                    case ERWActionEnum.ClearWindow:
                        {
                            var act = actual.Data as ERWClearWindow;
                            var toClose = string.IsNullOrEmpty(act.Group) ? opened : opened.Where(x => x.AssociatedAction?.Group == act.Group);

                            toClose.ToList().ForEach(x => x.Close());
                            break;
                        }
                    case ERWActionEnum.SetPercentage:
                        {
                            var act = actual.Data as ERWSetPercentage;
                            var toClose = string.IsNullOrEmpty(act.Group) ? opened : opened.Where(x => x.AssociatedAction?.Group == act.Group);

                            toClose.ToList().ForEach(x => x.SetProgressBarPercentage(act.Percentage));
                            break;
                        }
                    case ERWActionEnum.SetVisibility:
                        {
                            var act = actual.Data as ERWSetVisibility;
                            var toClose = string.IsNullOrEmpty(act.Group) ? opened : opened.Where(x => x.AssociatedAction?.Group == act.Group);

                            toClose.ToList().ForEach(x => {
                                if (act.Visible) x.ShowCompletely(); else x.HideCompletely();
                                    });
                            break;
                        }
                    case ERWActionEnum.GoTo:
                        {
                            var act = actual.Data as ERWGoTo;
                            var toClose = string.IsNullOrEmpty(act.Group) ? opened : opened.Where(x => x.AssociatedAction?.Group == act.Group);


                            toClose.ToList().ForEach(x =>
                            {
                                if (act.Random) x.Move((int)random.NextInt64(0, (int)MainWindow.Instance.ScreenWidth - x.Width), (int)random.NextInt64(0, (int)MainWindow.Instance.ScreenHeight - x.Height));
                                else x.Move(act.X, act.Y);
                            });
                            break;
                        }
                    case ERWActionEnum.Animate:
                        {
                            var act = actual.Data as ERWAnimate;
                            var toClose = string.IsNullOrEmpty(act.Group) ? opened : opened.Where(x => x.AssociatedAction?.Group == act.Group);

                            toClose.ToList().ForEach(x =>
                            {
                                
                                int targetFPS = Math.Max(Math.Min(60,act.FPS),1);
                                double targetFrameTime = 1000.0 / targetFPS;

                                Stopwatch stopwatch = new Stopwatch();
                                stopwatch.Start();

                                long lastFrameTime = stopwatch.ElapsedMilliseconds;

                                var randomUnique = random.NextDouble();

                                var myIndex = animIndex++;

                                Task.Run(() =>
                                {
                                    while (stopwatch.ElapsedMilliseconds < act.Duration * 1000)
                                    {
                                        long currentTime = stopwatch.ElapsedMilliseconds;
                                        long elapsed = currentTime - lastFrameTime;

                                        if (elapsed >= targetFrameTime)
                                        {
                                            lastFrameTime = currentTime;

                                            var dt = new DataTable();
                                            int xp = x.X, yp = x.Y, perc = -1;

                                            var args = new object[]
                                            {
                                                AudioPosition.TotalMilliseconds / 1000.0,
                                                stopwatch.ElapsedMilliseconds/1000.0,
                                                randomUnique,
                                                myIndex
                                            };

                                            if (!string.IsNullOrEmpty(act.X))
                                                xp = Convert.ToInt32(new NCalc.Expression(string.Format(act.X, [..args,random.NextDouble()] )).Evaluate());
                                            if (!string.IsNullOrEmpty(act.Y))
                                                yp = Convert.ToInt32(new NCalc.Expression(string.Format(act.Y, [.. args, random.NextDouble()])).Evaluate());
                                            if (!string.IsNullOrEmpty(act.Percentage))
                                                perc = Convert.ToInt32(new NCalc.Expression(string.Format(act.Percentage, [.. args, random.NextDouble()] )).Evaluate());

                                            if (xp != x.X || yp != x.Y) x.Move(xp, yp);
                                            if (perc != -1) x.SetProgressBarPercentage(perc);
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
                            });
                            break;
                        }
                }
                
            }
        }

        public TimeSpan AudioLength
        {
            get
            {
                if (Reader is null) return TimeSpan.Zero;
                try
                {
                    return Reader.GetLength();
                }
                catch
                {
                    return TimeSpan.Zero;
                }
            }
        }

        public IWaveSource Reader { get; set; }


        private void Config_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if(e.PropertyName == nameof(Config.MP3Location))
            {
                try
                {
                    if(AudioOut is not null) AudioOut.Stop();
                } catch { }
                Reader = CodecFactory.Instance.GetCodec(Config.MP3Location);
                AudioOut = new WasapiOut() { Latency = 100, Device = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia) };
                AudioOut.Initialize(Reader);

                Config.Timestamps.Clear();

                GenWaveformsAsync();
            }
        }

        public void GenWaveformsAsync()
        {
            new Thread(() =>
            {
                GenWaveforms();
            }).Start();
        }
            
        public void GenWaveforms()
        {
            if (Config is null) return;


            var tReader = CodecFactory.Instance.GetCodec(Config.MP3Location);

            var mono = tReader.ToSampleSource().ToMono();
            float[] data = new float[mono.Length];
            mono.Read(data, 0, (int)(mono.Length));
            double perSec = TimeUtil.TimeToPosition(TimeSpan.FromSeconds(1), Zoom);
            double samplesToShowPerSec = mono.WaveFormat.SampleRate / perSec;

            RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap((int)(data.Length / samplesToShowPerSec), (int)TimestampsGrid.ActualHeight,
                VisualTreeHelper.GetDpi(TimestampsGrid).PixelsPerInchX, VisualTreeHelper.GetDpi(TimestampsGrid).PixelsPerInchY, PixelFormats.Pbgra32);

            var vis = new DrawingVisual();
            var ctx = vis.RenderOpen();

            long cnt = 0;
            for (double i = 0; i < data.Length; i += samplesToShowPerSec)
            {
                float f = data[(int)i];
                double height = (TimestampsGrid.ActualHeight/3*2) * Math.Abs(f) + 1;
                ctx.DrawLine(new Pen(new SolidColorBrush(Colors.Green), 1), new Point(cnt, TimestampsGrid.ActualHeight / 2 - height), new Point(cnt++,
                    height + TimestampsGrid.ActualHeight/2));
            }
            ctx.Close();
            double W = renderTargetBitmap.Width;

            renderTargetBitmap.Render(vis);
            renderTargetBitmap.Freeze();
            WaveformsImage.Dispatcher.Invoke(() =>
            {

                WaveformsImage.Source = renderTargetBitmap;

                WaveformsImage.Width = W;
                WaveformsImage.Height = TimestampsGrid.ActualHeight;
            });




        }

        private void ChooseFileButton_Click(object sender, RoutedEventArgs e)
        {
            var diag = new OpenFileDialog();
            diag.Filter = "MP3 audio files|*.mp3";
            diag.DefaultExt = ".mp3";
            

            if(diag.ShowDialog() ?? false)
            {
                Config.MP3Location = diag.FileName;
            }
        }

        public DispatcherTimer AudioPositionPropertyChangedTimer { get; set; } = new DispatcherTimer(DispatcherPriority.Render);

        private void Play()
        {
            
            try
            {
                if (AudioOut is not null)
                {
                    AudioOut.Volume = 0.5F;
                    AudioOut.Play();
                }
            }
            catch { }
        }

        private void Play_Click(object sender, RoutedEventArgs e)
        {

            Play();

        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            IsPreview = false;

            opened.ForEach((x) => x.Close());
            opened.Clear();

            //AudioPositionPropertyChangedTimer.Stop();
            try
            {
                if (AudioOut is not null) AudioOut.Stop();
                if (Reader is null) return;
            }
            catch { }
        }


        private Point translation = new Point(0, 0);
        private bool isDragging;
        private void ConfigTimestampsStackPanel_MouseMove(object sender, MouseEventArgs e)
        {
            StackPanel src = (StackPanel)(sender is StackPanel stack ? stack : (sender as FrameworkElement).FindAscendantByName("ConfigTimestampsStackPanel"));

                if (e.LeftButton == MouseButtonState.Pressed)
                {


                    Point p = e.GetPosition(ConfigTimestampsItemsControl);

                    if (!isDragging)
                    {
                        translation = e.GetPosition(src);
                        isDragging = true;
                    }

                    double finalX = p.X - translation.X;



                (src.DataContext as ERWTimestamp).Timestamp = TimeSpan.FromSeconds(TimeUtil.PositionToTime(finalX, Zoom));
            

                    //src.CaptureMouse();
                }
                else
                {
                    //src.ReleaseMouseCapture();
                    if(isDragging)
                {
                    MainWindow.Instance.TimestampChanged();
                }
                    isDragging = false;
                }
            
        }
        int animIndex = 0;
        private void Preview_Click(object sender, RoutedEventArgs e)
        {
            animIndex = 0;
            IsPreview = true;
            List<ERWTimestamp> ordered = MainWindow.Config.Timestamps.OrderBy((t) => t.Timestamp).ToList();
            enumerator = ordered.GetEnumerator();
            enumerator.MoveNext();
            last = enumerator.Current;
            while(last is not null && last.Timestamp < AudioPosition)
            {
                enumerator.MoveNext();
                last = enumerator.Current;
            }
            Play();

        }

        private void ScrollWheel(object sender, MouseWheelEventArgs e)
        {
            if(Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                if(e.Delta > 0) {
                    Zoom -= 0.1;
                } else
                {
                    Zoom += 0.1;
                }
            }
        }
    }

    public class SelectTimestampCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return true;
        }

        public void Execute(object? parameter)
        {
            MainWindow.Instance.SelectedTimestamp = parameter as ERWTimestamp;
        }
    }


    public class DisplayStringConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] == DependencyProperty.UnsetValue) return string.Empty;
            var timestamp = values[0] as ERWTimestamp;
            switch (timestamp.Action)
            {
                case ERWActionEnum.ShowWindow:
                    {
                        var w = (timestamp.Data as ERWShowWindow);
                        string name = w.WindowIdentifier;
                        if (string.IsNullOrWhiteSpace(name)) name = "(NULL)";
                        return $"{name}{(w.Group != string.Empty ? $" - {w.Group}" : "")}";
                    }
                case ERWActionEnum.ClearWindow:
                    {
                        var w = timestamp.Data as ERWClearWindow;
                        if (w is null) return "CLEAR";
                        return $"CLEAR{(w.Group != string.Empty ? $" - {w.Group}" : "")}";
                    }
                case ERWActionEnum.SetPercentage:
                    {
                        var w = timestamp.Data as ERWSetPercentage;
                        if (w is null) return "P% (NULL)";
                        return $"P%{(w.Group != string.Empty ? $" - {w.Group}" : " (NULL)")}";
                    }
                case ERWActionEnum.SetVisibility:
                    {
                        var w = timestamp.Data as ERWSetVisibility;
                        if (w is null) return "SHOW (NULL)";
                        return $"{(w.Visible?"SHOW":"HIDE")}{(w.Group != string.Empty ? $" - {w.Group}" : " (NULL)")}";
                    }
                case ERWActionEnum.GoTo:
                    {
                        var w = timestamp.Data as ERWGoTo;
                        if (w is null) return "GOTO (NULL)";
                        return $"GOTO{(w.Group != string.Empty ? $" - {w.Group}" : " (NULL)")}";
                    }
                case ERWActionEnum.Animate:
                    {
                        var w = timestamp.Data as ERWAnimate;
                        if (w is null) return "ANIM (NULL)";
                        return $"ANIM{(w.Group != string.Empty ? $" - {w.Group}" : " (NULL)")}";
                    }
                default:
                    return string.Empty;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}
