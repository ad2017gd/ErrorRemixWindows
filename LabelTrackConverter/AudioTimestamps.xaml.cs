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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
            var timestamp = values[0] as ERWTimestamp;
            if (values[1] is null) return new Thickness(0);
            var height = ((double)values[1]) * 0.65;
            var available = MainWindow.Instance.AudioTimestampsElement.AudioAndTimestamps.ActualHeight;
            int fit = (int)(available / height);


            var index = MainWindow.Instance.Config.Timestamps.IndexOf(timestamp);

            return new Thickness(0, height * (index % fit), 0, 0);

        }


        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    
    public class LineColorConverter : IMultiValueConverter
    {
        [DllImport("shlwapi.dll")]
        public static extern int ColorHLSToRGB(int H, int L, int S);

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var timestamp = values[0] as ERWTimestamp;
            var index = MainWindow.Instance.Config.Timestamps.IndexOf(timestamp);
            var color = ColorHLSToRGB($"{index}{System.Convert.ToString(index,2)}{index+1}".GetHashCode()%240, 180, 240);

            var sdcolor = System.Drawing.Color.FromArgb(color);
            var brush = new SolidColorBrush(Color.FromRgb(sdcolor.R, sdcolor.G, sdcolor.B));
            brush.Freeze();

            return brush;

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
        

        [DependsOn(nameof(MainWindow.Instance.Config))]
        public ERWJson Config { get => MainWindow.Instance.Config; }

        [DependsOn(nameof(Config))]
        public Player Player { get; set; }

        public AudioTimestamps()
        {
            InitializeComponent();
            this.DataContext = this;
            Player = new Player(Config);
            Player.OnStop += () =>
            {
                if (AudioOut is not null) AudioOut.Stop();
            };

            MainWindow.Instance.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == "Config")
                {
                    OnPropertyChanged(nameof(Config));

                    MainWindow.Instance.Config.PropertyChanged += Config_PropertyChanged;
                    Player = new Player(Config);
                    Player.OnStop += () =>
                    {
                        if (AudioOut is not null) AudioOut.Stop();
                    };
                }
            };

            AudioPositionPropertyChangedTimer.Interval = new TimeSpan(0, 0, 0, 0, 20);
            AudioPositionPropertyChangedTimer.Tick += (_, _) =>
            {
                OnPropertyChanged(nameof(AudioPosition));
                OnAudioPositionChanged();
            };
            AudioPositionPropertyChangedTimer.Start();
            MainWindow.Instance.Config.PropertyChanged += Config_PropertyChanged;
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
                var ts = TimeSpan.FromSeconds(TimeUtil.PositionToTime(pos.X, Zoom));
                Reader.SetPosition(ts);



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

        DateTime last = DateTime.Now;
        public void OnAudioPositionChanged()
        {
            Player.OnAudioPositionChanged(AudioPosition);
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

            if (e.PropertyName == nameof(Config.MP4Location))
            {
                //UpdateMP3();

            }

        }
        public void UpdateMP3()
        {
            try
            {
                if (AudioOut is not null) AudioOut.Stop();


                Reader = CodecFactory.Instance.GetCodec(Config.MP3Location);
                AudioOut = new WasapiOut() { Latency = 100, Device = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia) };
                AudioOut.Initialize(Reader);


                //Config.Timestamps.Clear();

                GenWaveformsAsync();
            }
            catch { }
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
            bool configNull = false;
            Dispatcher.Invoke(() => configNull = Config is null);
            if (configNull) return;

            IWaveSource tReader = null;
            Dispatcher.Invoke(() => tReader = CodecFactory.Instance.GetCodec(Config.MP3Location));

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
        private void ChooseMP4Button_Click(object sender, RoutedEventArgs e)
        {
            var diag = new OpenFileDialog();
            diag.Filter = "MP4 video files|*.mp4";
            diag.DefaultExt = ".mp4";


            if (diag.ShowDialog() ?? false)
            {
                Config.MP4Location = diag.FileName;
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
            Player.Stop();
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
        private void Preview_Click(object sender, RoutedEventArgs e)
        {
            IsPreview = true;
            Player.Play(AudioPosition);
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
                case ERWActionEnum.Stop:
                    {
                        return $"STOP";
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
