using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Vanara.PInvoke;
using System.Windows.Media;
using static Vanara.PInvoke.ComCtl32;
using System.Drawing;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows;
using Vanara.Extensions;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using ERM;
using System.Media;
using PropertyChanged;
using System;

[PInvokeData("Commctrl.h", MSDNShortId = "bb787475")]
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
public struct TASKDIALOG_BUTTON_FIXED
{
    /// <summary>Indicates the value to be returned when this button is selected.</summary>
    public int nButtonID;

    /// <summary>
    /// Pointer that references the string to be used to label the button. This parameter can be either a null-terminated string or
    /// an integer resource identifier passed to the MAKEINTRESOURCE macro. When using Command Links, you delineate the command from
    /// the note by placing a new line character in the string.
    /// </summary>
    public IntPtr pszButtonText;
}

[PInvokeData("Commctrl.h", MSDNShortId = "bb787473")]
[SuppressMessage("Microsoft.Design", "CA1028:EnumStorageShouldBeInt32")] // Type comes from CommCtrl.h
public enum TaskDialogIconExtended : uint
{
    // NON-STANDARD!
    None = 0,
    TD_QUESTION_ICON = 0xFFF6,

    /// <summary>An exclamation-point icon appears in the task dialog.</summary>
    TD_WARNING_ICON = 0xFFFF, // MAKEINTRESOURCEW(-1)

    /// <summary>A stop-sign icon appears in the task dialog.</summary>
    TD_ERROR_ICON = 0xFFFE, // MAKEINTRESOURCEW(-2)

    /// <summary>An icon consisting of a lowercase letter i in a circle appears in the task dialog.</summary>
    TD_INFORMATION_ICON = 0xFFFD, // MAKEINTRESOURCEW(-3)

    /// <summary>A shield icon appears in the task dialog.</summary>
    TD_SHIELD_ICON = 0xFFFC, // MAKEINTRESOURCEW(-4)

    /// <summary>Shield icon on a blue background. Only available on Windows 8 and later.</summary>
    TD_SHIELDBLUE_ICON = 0xFFFB, // MAKEINTRESOURCEW(-5)

    /// <summary>Warning Shield icon on a yellow background. Only available on Windows 8 and later.</summary>
    TD_SECURITYWARNING_ICON = 0xFFFA, // MAKEINTRESOURCEW(-6)

    /// <summary>Error Shield icon on a red background. Only available on Windows 8 and later.</summary>
    TD_SECURITYERROR_ICON = 0xFFF9, // MAKEINTRESOURCEW(-7)

    /// <summary>Success Shield icon on a green background. Only available on Windows 8 and later.</summary>
    TD_SECURITYSUCCESS_ICON = 0xFFF8, // MAKEINTRESOURCEW(-8)

    /// <summary>Shield icon on a gray background. Only available on Windows 8 and later.</summary>
    TD_SHIELDGRAY_ICON = 0xFFF7, // MAKEINTRESOURCEW(-9)
}

namespace Shared
{

    public static class PositionUtils
    {
        public static double CoordsToScreen(double x, double w, double w2)
        {
            return (x / w) * w2;
        }
    }


    public class ERMButton : INotifyPropertyChanged
    {
        public string ButtonText { get; set; } = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;
    }
    public enum BarType
    {
        Normal,
        Marquee
    }

    public class ERMWindow : INotifyPropertyChanged
    {

        private static int id = 0;
        public string Identifier { get; set; } = (id++).ToString();
        public string Title { get; set; } = String.Empty;
        public string WindowTitle { get; set; } = "Unnamed window";
        public string Content { get; set; } = "This is a window!";
        public string Footer { get; set; } = String.Empty;
        public TaskDialogIconExtended Icon { get; set; } = 0;
        public string CustomSystemSound { get; set; } = "None";
        public BindingList<ERMButton> Buttons { get; set; } = new BindingList<ERMButton>() { new() {
            ButtonText = "OK"
        }};
        public bool EnableProgressBar { get; set; } = false;
        public BarType ProgressBarType { get; set; } = BarType.Normal;
        
        public int ProgressBarPercentage { get; set; } = 0;

        // Helpers for WPF and others
        [JsonIgnore]
        public List<BarType> BarTypes { get; set; } = new List<BarType>() { BarType.Normal, BarType.Marquee };

        [JsonIgnore]
        public SystemSound? CustomSound { get
            {
                if(CustomSystemSound is not null)
                {
                    switch(CustomSystemSound)
                    {
                        case "Question":
                            return SystemSounds.Question;
                        case "Warning":
                            return SystemSounds.Exclamation;
                        case "Information":
                            return SystemSounds.Asterisk;
                        case "Error":
                            return SystemSounds.Hand;
                        default:
                            return null;
                    }
                } else
                {
                    return null;
                }
            } }
        [JsonIgnore]
        public List<string> CustomSounds { get; } = new List<string>()
        {
            "None","Question","Warning","Information","Error"
        };

        [JsonIgnore]
        public List<TaskDialogIconExtended> AvailableIcons { get; } = new List<TaskDialogIconExtended>()
        {
            0,
            TaskDialogIconExtended.TD_SHIELD_ICON,
            TaskDialogIconExtended.TD_ERROR_ICON,
            TaskDialogIconExtended.TD_WARNING_ICON,
            TaskDialogIconExtended.TD_INFORMATION_ICON,
            TaskDialogIconExtended.TD_QUESTION_ICON
        };

        [JsonIgnore]
        public bool HasIcon { get => Icon != 0; }

        [JsonIgnore]
        public string EditorDescription { get => $"ID:{Identifier} - {string.Concat(WindowTitle.Take(16)) + (WindowTitle.Length > 16 ? "..." : "")} - {(string.IsNullOrEmpty(Content.Trim()) ? 
            string.Concat(Title.Take(24)) + (Title.Length > 16 ? "..." : "") : string.Concat(Content.Take(24)) + (Content.Length > 16 ? "..." : "")
            )}"; }

        [JsonIgnore]
        public ImageSource IconSource
        { get {
                return SystemIconsBetter.Get(this.Icon).ToImageSource();
            } }

        [JsonIgnore]
        public IEnumerable<TASKDIALOG_BUTTON_FIXED> ButtonsConverted { get
            {
                return Buttons.Select((but,i) => new TASKDIALOG_BUTTON_FIXED() { 
                    pszButtonText = Marshal.StringToHGlobalUni(but.ButtonText), nButtonID = i
                });
            } }

        [JsonIgnore]
        public bool HasTitle { get
            {
                return !string.IsNullOrEmpty(Title.Trim());
            } }

        public event PropertyChangedEventHandler? PropertyChanged;
    }


    public enum ERMActionEnum
    {
        ShowWindow,
        ClearWindow,
        PlaySystemSound
    }

    [JsonDerivedType(typeof(ERMShowWindow))]
    [JsonDerivedType(typeof(ERMClearWindow))]
    public abstract class ERMAction : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    public class ERMShowWindow : ERMAction {
        public string WindowIdentifier { get; set; } = String.Empty;
        public string Group { get; set; } = string.Empty;
        public bool IsRelative { get; set; } = true;
        public int X { get; set; } = 0;
        public int Y { get; set; } = 0;


        public RelativeAxis XAxis { get; set; } = RelativeAxis.Center;
        public RelativeAxis YAxis { get; set; } = RelativeAxis.Center;
        public RelativeAxis SelfXAxis { get; set; } = RelativeAxis.Center;
        public RelativeAxis SelfYAxis { get; set; } = RelativeAxis.Center;

        [JsonIgnore]
        public List<RelativeAxis> AvailableRelativeAxis { get; set; } = new List<RelativeAxis>() { RelativeAxis.Start, RelativeAxis.Center, RelativeAxis.End };
        
    }

    public class ERMClearWindow : ERMAction
    {
        public string Group { get; set; } = string.Empty;

    }

    public class ERMTimestamp : INotifyPropertyChanged
    {
        public ERMActionEnum Action { get; set; } = ERMActionEnum.ShowWindow;

        public ERMAction Data { get; set; } = null;

        public TimeSpan Timestamp { get; set; } = TimeSpan.Zero;

        public event PropertyChangedEventHandler? PropertyChanged;
        public Action? OnTimestampChangedCallback;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        [JsonIgnore]
        public bool Executed { get; set; } = false;

        public ERMTimestamp()
        {
        }

        public void OnDataChanged()
        {
            Data.PropertyChanged += Data_PropertyChanged;
        }

        private void Data_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(Data));
        }
    }



    public class ERMJson : INotifyPropertyChanged
    {
        public BindingList<ERMWindow> DefinedWindows { get; set; } = new BindingList<ERMWindow>();
        public BindingList<ERMTimestamp> Timestamps { get; set; } = new BindingList<ERMTimestamp>()
        {
            
        };
        public string MP3Location { get; set; } = string.Empty;


        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        public void Init()
        {
            DefinedWindows.ListChanged += (_, _) => OnPropertyChanged(nameof(DefinedWindows));
            Timestamps.ListChanged += (_, _) => OnPropertyChanged(nameof(Timestamps));
        }


        public ERMJson()
        {
            Init();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }


}