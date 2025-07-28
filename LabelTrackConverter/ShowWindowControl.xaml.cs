using ERW;
using Shared;
using System;
using System.Collections.Generic;
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

namespace ERWEditor
{
    /// <summary>
    /// Interaction logic for ShowWindowControl.xaml
    /// </summary>
    public partial class ShowWindowControl : UserControl
    {
        public ShowWindowControl()
        {
            InitializeComponent();

            DataContext = MainWindow.Instance;
        }


        private void PreviewButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Instance.SelectedWindow is null) return;
            var task = new TaskDialog(MainWindow.Instance.SelectedWindow);
            task.WaitForHWND();
            task.Center();
        }

        private Point translation = new Point(0, 0);
        private bool isDragging;
        private void TaskDialogDesign_MouseMove(object sender, MouseEventArgs e)
        {
            if (MainWindow.Instance.SelectedTimestamp is null) return;
            if (e.Source is TaskDialogDesign shape)
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    Point p = e.GetPosition(TimestampCanvas);

                    if (!isDragging)
                    {
                        translation = e.GetPosition(TimestampDialog);
                        //translation.X *= TimestampDialog.LayoutTransform.Value.M11;
                        //translation.Y *= TimestampDialog.LayoutTransform.Value.M22;
                        isDragging = true;
                    }

                    double finalX = p.X - translation.X;
                    double finalY = p.Y - translation.Y;

                    double diagWidth = TimestampDialog.ActualWidth /** TimestampDialog.LayoutTransform.Value.M11*/;
                    double diagHeight = TimestampDialog.ActualHeight /** TimestampDialog.LayoutTransform.Value.M22*/;

                    // Snap right
                    if (Math.Abs((finalX + diagWidth) - TimestampCanvas.ActualWidth) < 12)
                    {
                        finalX = TimestampCanvas.ActualWidth - diagWidth;
                    }
                    // Snap left
                    if (Math.Abs(finalX - 0) < 12)
                    {
                        finalX = 0;
                    }
                    // Snap top
                    if (Math.Abs(finalY - 0) < 12)
                    {
                        finalY = 0;
                    }
                    // Snap bottom
                    if (Math.Abs((finalY + diagHeight) - TimestampCanvas.ActualHeight) < 12)
                    {
                        finalY = TimestampCanvas.ActualHeight - diagHeight;
                    }

                    // Snap center
                    if (
                        (Math.Abs((finalY + diagHeight / 2) - TimestampCanvas.ActualHeight / 2) < 12) &&
                        (Math.Abs((finalX + diagWidth / 2) - TimestampCanvas.ActualWidth / 2) < 12)
                        )
                    {
                        finalY = TimestampCanvas.ActualHeight / 2 - diagHeight / 2;
                        finalX = TimestampCanvas.ActualWidth / 2 - diagWidth / 2;
                    }
                    ERWShowWindow wnd = (MainWindow.Instance.SelectedTimestamp.Data as ERWShowWindow);
                    wnd.X = (int)PositionUtils.CoordsToScreen(ERW.TaskDialog.AbsoluteToRelative((int)finalX, wnd.IsRelative ? wnd.XAxis : RelativeAxis.Start, wnd.IsRelative ? wnd.SelfXAxis : RelativeAxis.Start, (int)diagWidth, (int)TimestampCanvas.ActualWidth),
                        TimestampCanvas.ActualWidth, ERW.TaskDialog.GetScreenDimensions().X);
                    wnd.Y = (int)PositionUtils.CoordsToScreen(ERW.TaskDialog.AbsoluteToRelative((int)finalY, wnd.IsRelative ? wnd.YAxis : RelativeAxis.Start, wnd.IsRelative ? wnd.SelfYAxis : RelativeAxis.Start, (int)diagHeight, (int)TimestampCanvas.ActualHeight),
                        TimestampCanvas.ActualHeight, ERW.TaskDialog.GetScreenDimensions().Y);
                    shape.CaptureMouse();
                }
                else
                {
                    shape.ReleaseMouseCapture();
                    isDragging = false;
                }
            }
        }

        private void PreviewTimestamp_Click(object sender, RoutedEventArgs e)
        {
            
            if (MainWindow.Instance.SelectedTimestamp is null) return;
            var act = (MainWindow.Instance.SelectedTimestamp.Data as ERWShowWindow);
            var wnd = WindowConverter.FindWindow(act.WindowIdentifier);
            if (wnd is null) return;
            var task = new TaskDialog(wnd);
            task.WaitForHWND();
            if (act.IsRelative)
            {
                task.MoveRelative(act.X, act.Y, act.XAxis, act.YAxis, act.SelfXAxis, act.SelfYAxis);
            }
            else
            {
                task.Move(act.X, act.Y);
            }
        }
    }
}
