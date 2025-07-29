using dnlib.W32Resources;
using ERW;
using ICSharpCode.Decompiler;
using Mono.Cecil;
using PropertyChanged;
using Shared;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xaml;
using static ERWEditor.S2CPositionXConverter;
using static ICSharpCode.Decompiler.SingleFileBundle;
using static Vanara.PInvoke.ComCtl32;
using static Vanara.PInvoke.User32.RAWINPUT;
using DependsOnAttribute = PropertyChanged.DependsOnAttribute;
using Point = System.Windows.Point;
using TaskDialog = ERW.TaskDialog;

namespace ERWEditor
{
    public class DefinedWindowIdentifierConverter : IValueConverter
    {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return MainWindow.Instance.Config.DefinedWindows.Select(x => x.Identifier).ToList();
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


        internal ShowWindowControl? ctrl = null;
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
        [OnChangedMethod(nameof(OnConfigChanged))]
        public ERWJson Config { get; set; } = new ERWJson();
        public static bool Modified { get; set; } = false;

        public MainWindow()
        {
            InitializeComponent();
            ERW.TaskDialog.CreateDaddyDialog();
            this.DataContext = this;
            ctrl = new ShowWindowControl() { DataContext = this };
            Config.PropertyChanged += (_, _) =>
            {
                Modified = true;
            };

        }
        public void OnConfigChanged()
        {
            Modified = false;
            Config.PropertyChanged += (_, _) =>
            {
                Modified = true;
            };
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
                if((string)tb.Header == "Timestamps")
                {
                    AudioTimestampsElement.UpdateMP3();

                }
            }
        }
        /* https://github.dev/icsharpcode/ILSpy */
        

        public static unsafe bool GetBundleHeaderOffsetOffset(MemoryMappedViewAccessor view, out long bundleHeaderOffsetOffset)
        {
            var buffer = view.SafeMemoryMappedViewHandle;
            byte* data = null;
            buffer.AcquirePointer(ref data);

            ReadOnlySpan<byte> bundleSignature = new byte[] {
				// 32 bytes represent the bundle signature: SHA-256 for ".net core bundle"
				0x8b, 0x12, 0x02, 0xb9, 0x6a, 0x61, 0x20, 0x38,
                0x72, 0x7b, 0x93, 0x02, 0x14, 0xd7, 0xa0, 0x32,
                0x13, 0xf5, 0xb9, 0xe6, 0xef, 0xae, 0x33, 0x18,
                0xee, 0x3b, 0x2d, 0xce, 0x24, 0xb3, 0x6a, 0xae
            };

            byte* end = data + (checked((long)buffer.ByteLength) - bundleSignature.Length);
            for (byte* ptr = data; ptr < end; ptr++)
            {
                if (*ptr == 0x8b && bundleSignature.SequenceEqual(new ReadOnlySpan<byte>(ptr, bundleSignature.Length)))
                {
                    bundleHeaderOffsetOffset = (long)(ptr - data - sizeof(long));
                    
                    buffer.ReleasePointer();
                    return true;
                }
            }

            bundleHeaderOffsetOffset = 0;
            buffer.ReleasePointer();
            return false;
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            // i am so proud of this code but i hate single file bundles now

            var diag = new SaveFileDialog();
            diag.Filter = "ERW bundled executable|*.exe";
            diag.DefaultExt = ".exe";
            if (diag.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                try
                {
                    var file = Properties.Resources.ERW;

                    using var mmf = MemoryMappedFile.CreateNew(null, file.Length);
                    using var accessor = mmf.CreateViewAccessor();
                    accessor.WriteArray(0, file, 0, file.Length);

                    SingleFileBundle.IsBundle(accessor, out long bundleHeaderOffset);
                    var assembly = SingleFileBundle.ReadManifest(accessor, bundleHeaderOffset);

                    var tryFindDll = assembly.Entries.ToList().Cast<Entry?>().FirstOrDefault(x => x.Value.RelativePath == "ERW.dll", null);

                    if (tryFindDll is null)
                    {
                        System.Windows.Forms.MessageBox.Show("Couldn't find ERW.dll in ERW.exe! Please report this issue on GitHub: https://github.com/ad2017gd/ErrorRemixWindows", "ERW Editor", 0, MessageBoxIcon.Error);
                        return;
                    }
                    var dll = tryFindDll.Value;

                    var ConfigClean = JsonSerializer.Deserialize<ERWJson>(JsonSerializer.Serialize(Config));
                    ConfigClean.MP4Location = "";
                    ConfigClean.MP3Location = "";

                    var jsonBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(ConfigClean));
                    using var jsonStream = new MemoryStream(jsonBytes);

                    if (!File.Exists(Config.MP3Location))
                    {
                        System.Windows.Forms.MessageBox.Show("Cannot export without audio file!", "ERW Editor", 0, MessageBoxIcon.Error);
                        return;
                    }

                    using var mp3Stream = File.OpenRead(Config.MP3Location);

                    // allocate with excess
                    long mp4Length = 0;

                    if (!string.IsNullOrEmpty(Config.MP4Location) && File.Exists(Config.MP4Location))
                    {
                        using var mp4Stream = File.OpenRead(Config.MP4Location);
                        mp4Length = mp4Stream.Length;
                    }


                    var bytes = new byte[dll.Size + jsonBytes.Length + mp3Stream.Length + mp4Length + 8192];
                    accessor.ReadArray(dll.Offset, bytes, 0, (int)(dll.Size));

                    using var stream = new MemoryStream(bytes);
                    var dllAssembly = Mono.Cecil.AssemblyDefinition.ReadAssembly(stream, new() { ReadWrite = true });


                    var newResource = new EmbeddedResource("ERW.config.json", ManifestResourceAttributes.Public, jsonStream);
                    dllAssembly.MainModule.Resources.Add(newResource);

                    var newResource2 = new EmbeddedResource("ERW.audio.mp3", ManifestResourceAttributes.Public, mp3Stream);
                    dllAssembly.MainModule.Resources.Add(newResource2);


                    if (!string.IsNullOrEmpty(Config.MP4Location) && File.Exists(Config.MP4Location))
                    {
                        using var mp4Stream = File.OpenRead(Config.MP4Location);
                        mp4Length = mp4Stream.Length;

                        var newResource3 = new EmbeddedResource("ERW.video.mp4", ManifestResourceAttributes.Public, mp4Stream);
                        dllAssembly.MainModule.Resources.Add(newResource3);

                        dllAssembly.Write();
                    } else
                    {
                        dllAssembly.Write();
                    }





                        stream.Position = 0;
                    var length = stream.Length;
                    var oldLength = dll.Size;

                    var additional = length - oldLength;


                    using var newmmf = MemoryMappedFile.CreateNew(null, file.Length + additional);
                    using var newAccessor = newmmf.CreateViewAccessor();
                    newAccessor.WriteArray(0, file, 0, (int)dll.Offset);
                    newAccessor.WriteArray(dll.Offset, bytes, 0, (int)length);
                    newAccessor.WriteArray(dll.Offset + length, file, (int)(dll.Offset + oldLength), (int)(file.Length - dll.Offset - oldLength));

                    GetBundleHeaderOffsetOffset(newAccessor, out long Off);
                    newAccessor.Write(Off, (long)bundleHeaderOffset + additional);

                    // fix offsets

                    var list = assembly.Entries.ToList();

                    var found = false;

                    for (int i = 0; i < list.Count; i++)
                    {
                        if (!found)
                        {
                            if (list[i].RelativePath == "ERW.dll")
                            {
                                found = true;

                                Entry ent = list[i];
                                ent.Size += additional;
                                list[i] = ent;
                            }
                        }
                        else
                        {
                            Entry ent = list[i];
                            ent.Offset += additional;
                            list[i] = ent;
                        }
                    }



                    var newStream = newmmf.CreateViewStream(0, file.Length + additional, MemoryMappedFileAccess.ReadWrite);
                    using var writer = new BinaryWriter(newStream, Encoding.UTF8, leaveOpen: true);
                    writer.Seek((int)(bundleHeaderOffset + additional), SeekOrigin.Begin);
                    writer.Write(assembly.MajorVersion);
                    writer.Write(assembly.MinorVersion);
                    writer.Write(assembly.FileCount);
                    writer.Write("=ad2017gd=ERW=" + new string(Enumerable.Range(0, 32 - 14).Select(_ => "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"[Random.Shared.Next(62)]).ToArray()));
                    writer.Write(assembly.DepsJsonOffset += additional);
                    writer.Write(assembly.DepsJsonSize);
                    writer.Write(assembly.RuntimeConfigJsonOffset += additional);
                    writer.Write(assembly.RuntimeConfigJsonSize);
                    writer.Write(assembly.Flags);

                    list.ForEach(entry =>
                    {
                        writer.Write(entry.Offset);
                        writer.Write(entry.Size);
                        writer.Write(entry.CompressedSize);
                        writer.Write((byte)entry.Type);
                        writer.Write(entry.RelativePath);
                    });


                    using (var fileStream = File.Create(diag.FileName))
                    {
                        newStream.Seek(0, SeekOrigin.Begin);
                        newStream.CopyTo(fileStream);
                    }
                } catch (Exception er)
                {
                    System.Windows.Forms.MessageBox.Show($"Unexpected error: {er.Message}", "ERW Editor", 0, MessageBoxIcon.Error);
                }
                

            }
        }

        private void OpenSaved_Click(object sender, RoutedEventArgs e)
        {
            var diag = new Microsoft.Win32.OpenFileDialog();
            diag.Filter = "ERW config files|*.json";
            diag.DefaultExt = ".json";


            if (diag.ShowDialog() ?? false)
            {
                try
                {
                    var newCfg = JsonSerializer.Deserialize<ERWJson>(File.ReadAllText(diag.FileName));
                    if (newCfg is not null)
                    {
                        Config = newCfg;
                        Config.Init();
                        PropertyChanged.Invoke(this, new(nameof(Config)));
                    }
                    
                } catch
                {
                    System.Windows.Forms.MessageBox.Show("Couldn't load selected config.");
                }
            }
        }

        private void ThisWindow_Closing(object sender, CancelEventArgs e)
        {
            if(Modified)
            {
                var res = System.Windows.Forms.MessageBox.Show("Quit without saving?", "ERW Editor", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (res != System.Windows.Forms.DialogResult.Yes)
                {
                    e.Cancel = true;
                }
            }
            
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var diag = new SaveFileDialog();
            diag.Filter = "ERW config files|*.json";
            diag.DefaultExt = ".json";

            if (diag.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                try
                {
                    var newCfg = JsonSerializer.Serialize(Config);
                    File.WriteAllText(diag.FileName, newCfg);
                    Modified = false;
                }
                catch
                {
                    System.Windows.Forms.MessageBox.Show("Couldn't save config!");
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
            return MainWindow.Instance.Config.DefinedWindows.FirstOrDefault((d) => d.Identifier == Identifier, null);
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
