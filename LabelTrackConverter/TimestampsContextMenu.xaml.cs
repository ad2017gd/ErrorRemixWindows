using Kasay;
using PropertyChanged;
using Shared;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Vanara.PInvoke;

namespace ERMEditor
{
    /// <summary>
    /// Interaction logic for TimestampsContextMenu.xaml
    /// </summary>
    public partial class TimestampsContextMenu : ContextMenu, INotifyPropertyChanged
    {
        [Bind] public object? Timestamp { get; set; } = null;

 
        public Visibility ShowDelete { get => Timestamp is not null ? Visibility.Visible : Visibility.Collapsed; }

        

        public TimestampsContextMenu()
        {
            InitializeComponent();
            this.DataContext = this;
        }

        private TimeSpan timeAtClick = TimeSpan.Zero;


        private void MenuItem_Click_1(object sender, RoutedEventArgs e)
        {
            MainWindow.Instance.DeleteTimestamp((Timestamp as StackPanel).DataContext as ERMTimestamp);
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            timeAtClick = TimeSpan.FromSeconds(TimeUtil.PositionToTime(Mouse.GetPosition(MainWindow.Instance.AudioTimestampsElement.TimestampsGrid).X, MainWindow.Instance.AudioTimestampsElement.Zoom));
            OnPropertyChanged(nameof(ShowDelete));
        }

        private void ShowWindow_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Config.Timestamps.Add(new ERMTimestamp()
            {
                Timestamp = timeAtClick,
                Action = ERMActionEnum.ShowWindow,
                Data = new ERMShowWindow()
            });
        }

        private void HideWindow_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Config.Timestamps.Add(new ERMTimestamp()
            {
                Timestamp = timeAtClick,
                Action = ERMActionEnum.ClearWindow,
                Data = new ERMClearWindow()
            });
        }
    }
}
