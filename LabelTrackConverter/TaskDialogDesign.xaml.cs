using Shared;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
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

namespace ERMEditor
{
    /// <summary>
    /// Interaction logic for TaskDialogDesign.xaml
    /// </summary>
    /// 

    public partial class TaskDialogDesign : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;


        public ERMWindow ERMWindow
        {
            get { return (ERMWindow)GetValue(WindowProperty); }
            set { SetValue(WindowProperty, value); }
        }
        public static readonly DependencyProperty WindowProperty
            = DependencyProperty.Register(
                  "ERMWindow",
                  typeof(ERMWindow),
                  typeof(TaskDialogDesign),
                  new FrameworkPropertyMetadata(
            null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

        

        public TaskDialogDesign()
        {
            this.DataContext = this;
            InitializeComponent();
        }
    }
}
