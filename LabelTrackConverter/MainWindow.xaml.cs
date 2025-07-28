using ERW;
using Shared;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xaml;
using static ERWEditor.S2CPositionXConverter;
using static Vanara.PInvoke.ComCtl32;
using static Vanara.PInvoke.User32.RAWINPUT;
using Point = System.Windows.Point;

namespace ERWEditor
{
    public class DefinedWindowIdentifierConverter : IValueConverter
    {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return MainWindow.Config.DefinedWindows.Select(x => x.Identifier).ToList();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class S2CPositionXConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] == DependencyProperty.UnsetValue || values[0] == null || MainWindow.Instance.SelectedType != "Shared.ERWShowWindow") return (double)0;
            ERWTimestamp tmp = values[0] as ERWTimestamp;
            ERWShowWindow data = tmp.Data as ERWShowWindow;
            TaskDialogDesign taskDialogDesign = values[3] as TaskDialogDesign;
            ShowWindowControl showWindowCtrl = values[4] as ShowWindowControl;
            if (data is null) return 0;

            double canvasWidth = showWindowCtrl.TimestampCanvas.ActualWidth;

            double x = ERW.TaskDialog.RelativeToAbsolute(
                (int)data.X, 
                data.IsRelative ? data.XAxis : RelativeAxis.Start, 
                data.IsRelative ? data.SelfXAxis : RelativeAxis.Start, 
                (int)(taskDialogDesign.ActualWidth),
                (int)canvasWidth);
            return x;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        
    }
    

    public class S2CPositionYConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] == DependencyProperty.UnsetValue || values[0] == null || MainWindow.Instance.SelectedType != "Shared.ERWShowWindow") return (double)0;
            ERWTimestamp tmp = values[0] as ERWTimestamp;
            ERWShowWindow data = tmp.Data as ERWShowWindow;
            TaskDialogDesign taskDialogDesign = values[3] as TaskDialogDesign;
            ShowWindowControl showWindowCtrl = values[4] as ShowWindowControl;
            if (data is null) return 0;

            double screenheight = showWindowCtrl.TimestampCanvas.ActualHeight;

            double y = ERW.TaskDialog.RelativeToAbsolute(
                (int)data.Y,
                data.IsRelative ? data.YAxis : RelativeAxis.Start,
                data.IsRelative ? data.SelfYAxis : RelativeAxis.Start,
                (int)(taskDialogDesign.ActualHeight), 
                (int)screenheight);


            return y;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DefinedRelativeAxisConverter : IValueConverter
    {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return new List<string>() { "Start", "Center", "End" };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 

    public partial class MainWindow : Window, INotifyPropertyChanged
    {

        public static MainWindow Instance { get => (Window.GetWindow(App.Current.MainWindow) as MainWindow); }

        public ERWWindow? SelectedWindow { get; set; }
        public int SelectedWindowIndex { get; set; }

        public ERWTimestamp? SelectedTimestamp { get; set; }
        public int SelectedTimestampIndex { get => Config.Timestamps.OrderBy(x => x.Timestamp).ToList().IndexOf(SelectedTimestamp as ERWTimestamp); }

        public ERWTimestamp? PreviousTimestamp { get => SelectedTimestampIndex >= 1 ? Config.Timestamps.OrderBy(x => x.Timestamp).ToList()[SelectedTimestampIndex-1] : null; }
        public bool HasPreviousTimestamp { get => SelectedTimestampIndex >= 1; }

        public ERWTimestamp? PreviousTimestamp2 { get => SelectedTimestampIndex >= 2 ? Config.Timestamps.OrderBy(x => x.Timestamp).ToList()[SelectedTimestampIndex - 2] : null; }
        
        public bool HasPreviousTimestamp2 { get => SelectedTimestampIndex >= 2; }

        public double ScreenWidth { get => SystemParameters.VirtualScreenWidth; }
        public double ScreenHeight { get => SystemParameters.VirtualScreenHeight; }
        public bool UseOnionSkin { get; set; } = true;

        [DependsOn(nameof(SelectedTimestamp))]
        public string? SelectedType { get => SelectedTimestamp?.Data.GetType().ToString(); }


        private ShowWindowControl? ctrl = null;
        [DependsOn(nameof(SelectedTimestamp))]
        public UIElement? SelectedControl
        {
            get
            {
                switch (SelectedTimestamp?.Action)
                {
                    case ERWActionEnum.ShowWindow:
                        if (ctrl is null) return null;

                        ctrl.DataContext = this;
                        return ctrl;
                    case ERWActionEnum.ClearWindow: 
                        return new ClearWindowControl() { DataContext = this };
                    case ERWActionEnum.SetPercentage:
                        return new SetPercentControl() { DataContext = this };
                    case ERWActionEnum.SetVisibility:
                        return new SetVisibilityControl() { DataContext = this };
                    case ERWActionEnum.GoTo:
                        return new GoToControl() { DataContext = this };
                    case ERWActionEnum.Animate:
                        return new AnimateControl() { DataContext = this };

                    default:
                        return null ;
                }
            }
        }

        public static ERWJson Config { get; set; } = new ERWJson();

        public MainWindow()
        {
            InitializeComponent();
            ERW.TaskDialog.CreateDaddyDialog();
            this.DataContext = this;
            ctrl = new ShowWindowControl() { DataContext = this };

        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void TimestampChanged()
        {
            PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedTimestamp)));
            PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(PreviousTimestamp)));
            PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(PreviousTimestamp2)));
            PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(HasPreviousTimestamp)));
            PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(HasPreviousTimestamp2)));
            Task.Run(() =>
            {
                Thread.Sleep(50);
                Dispatcher.Invoke(() =>
                {
                    PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedTimestamp)));
                    PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(PreviousTimestamp)));
                    PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(PreviousTimestamp2)));
                    PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(HasPreviousTimestamp)));
                    PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(HasPreviousTimestamp2)));

                });
            });
        }

        private void AppBarButton_Click(object sender, RoutedEventArgs e)
        {
            Config.DefinedWindows.Add(new ERWWindow());
        }

        private void AppBarButton_Click_1(object sender, RoutedEventArgs e)
        {
            try
            {
                Config.DefinedWindows.RemoveAt(SelectedWindowIndex);
            }
            catch { }
        }

        private void PreviewButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedWindow is null) return;
            var task = new TaskDialog(SelectedWindow);
            task.WaitForHWND();
            task.Center();
        }


        internal void DeleteTimestamp(ERWTimestamp timestamp)
        {
            Config.Timestamps.Remove(timestamp);
        }
        

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0) return;
            if(e.AddedItems[0] is TabItem tb)
            {
                if(((string)tb.Header) == "Export")
                {
                    var json = JsonSerializer.Serialize(Config);
                    ;
                }
            }
        }
    }

    public class ButtonConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return String.Join(";",(value as BindingList<ERWButton>).Select(x => x.ButtonText)); 
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return new BindingList<ERWButton>((value as string).Split(";").Select(x => new ERWButton() { ButtonText= x }).ToList());
        }
    }


    public class WindowConverter : IValueConverter
    {
        public static ERWWindow? FindWindow(string Identifier)
        {
            return MainWindow.Config.DefinedWindows.FirstOrDefault((d) => d.Identifier == Identifier, null);
        }

        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return FindWindow(value as string);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return new NotImplementedException();
        }
    }
}
