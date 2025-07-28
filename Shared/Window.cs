using System;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vanara.PInvoke;
using static Vanara.PInvoke.User32;
using static Vanara.PInvoke.ComCtl32;
using System.Runtime.InteropServices;
using Vanara.Extensions;
using System.Drawing;
using Shared;
using Point = System.Drawing.Point;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using static Vanara.PInvoke.Shell32;
using System.Media;
using static Shared.ERWWindow;

namespace ERW
{
    public static class IconExtension
    {
        public static ImageSource ToImageSource(this Icon icon)
        {
            ImageSource imageSource = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            return imageSource;
        }

        public static ImageSource ToImageSource(this HICON icon)
        {
            ImageSource imageSource = Imaging.CreateBitmapSourceFromHIcon(
                (IntPtr)icon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            return imageSource;
        }
    }

    public static class SystemIconsBetter
    {
        public static HICON Get(TaskDialogIconExtended icon)
        {
            SHSTOCKICONINFO sii = new SHSTOCKICONINFO();
            sii.cbSize = (UInt32)Marshal.SizeOf(typeof(SHSTOCKICONINFO));

            SHSTOCKICONID iconid;

            switch(icon)
            {
                case TaskDialogIconExtended.TD_SHIELD_ICON:
                    iconid = SHSTOCKICONID.SIID_SHIELD;
                    break;
                case TaskDialogIconExtended.TD_ERROR_ICON:
                    iconid = SHSTOCKICONID.SIID_ERROR;
                    break;
                case TaskDialogIconExtended.TD_INFORMATION_ICON:
                    iconid = SHSTOCKICONID.SIID_INFO;
                    break;
                case TaskDialogIconExtended.TD_WARNING_ICON:
                    iconid = SHSTOCKICONID.SIID_WARNING;
                    break;
                case TaskDialogIconExtended.TD_QUESTION_ICON:
                    HICON[] hICONs = { HICON.NULL };
                    HICON[] hICONsmall = { HICON.NULL };
                    ExtractIconEx(@"C:\Windows\System32\imageres.dll", 94, hICONs,hICONsmall, 1);
                    return hICONs[0];
                default:
                    iconid = SHSTOCKICONID.SIID_INFO;
                    break;
            }

            SHGetStockIconInfo(iconid,
                    SHGSI.SHGSI_ICON,
                    ref sii);

            return sii.hIcon;
        }
    }

    public class TaskDialog
    {
        public HWND HWND = HWND.NULL;
        public Task task;
        RECT diagRect;

        public ERWShowWindow? AssociatedAction { get; set; }

        // DaddyDialog
        // A dialog placed away from the screen. Used to hide the child dialogs before HWND is aquired and they are moved to the required position.
        public static TaskDialog? DaddyDialog = null;

        public static void CreateDaddyDialog()
        {
            DaddyDialog = new TaskDialog("TempDiagERW", "Temporary dialog for ErrorRemixWindows", foreground:false, isDaddyDiag: true);
            DaddyDialog.WaitForHWND();
            DaddyDialog.Move(-9999, -9999);
            DaddyDialog.HideCompletely();
        }
        public TaskDialog(ERWWindow wnd, ERWShowWindow? assoc = null) : this(
            wnd.WindowTitle, wnd.Content, wnd.ButtonsConverted.ToArray(), 
            wnd.Title, wnd.Icon, wnd.Footer, true,
            wnd.CustomSound, wnd.EnableProgressBar, wnd.ProgressBarType, 
            wnd.ProgressBarPercentage
            ) 
        {
            AssociatedAction = assoc;
        }
    
        public TaskDialog(string windowtitle, string body, TASKDIALOG_BUTTON_FIXED[]? buttons = null, 
            string title = "", TaskDialogIconExtended icon = 0, string? footer = null, bool foreground = true,
            SystemSound? customSound = null, bool progressBar = false, BarType progressBarType = BarType.Normal, 
            int progressBarPercentage = 0, bool isDaddyDiag = false
            )
        {
            var cfg = new TASKDIALOGCONFIG();
            cfg.pButtons = buttons is not null ? buttons.MarshalToPtr(Marshal.AllocHGlobal, out _) : IntPtr.Zero;
            cfg.cButtons = buttons is not null ? (uint)buttons.Length : 0;
            cfg.Content = body;
            cfg.WindowTitle = windowtitle;
            cfg.Footer = footer ?? "";
            cfg.dwFlags = TASKDIALOG_FLAGS.TDF_CAN_BE_MINIMIZED;


            if (icon != TaskDialogIconExtended.TD_QUESTION_ICON)
            {
                cfg.mainIcon = (IntPtr)icon;
            } else
            {
                cfg.dwFlags |= TASKDIALOG_FLAGS.TDF_USE_HICON_MAIN;
                cfg.mainIcon = (IntPtr)SystemIconsBetter.Get(icon);
                SystemSounds.Question.Play();
            }
            if(icon == TaskDialogIconExtended.TD_SHIELD_ICON)
            {
                SystemSounds.Asterisk.Play();
            }
            if(icon == TaskDialogIconExtended.None && customSound is not null)
            {
                customSound.Play();
            }

            cfg.MainInstruction = title;
            
            if (DaddyDialog is not null)
            {
                // HIDE THE DIALOG UNTIL HWND IS OBTAINED USING SHADY TRICK
                cfg.hwndParent = (HWND)DaddyDialog.HWND;
                cfg.dwFlags |= TASKDIALOG_FLAGS.TDF_POSITION_RELATIVE_TO_WINDOW;
                
            }
            if (!foreground) cfg.dwFlags |= TASKDIALOG_FLAGS.TDF_NO_SET_FOREGROUND;
            if (progressBar)
            {
                if(progressBarType == BarType.Normal) cfg.dwFlags |= TASKDIALOG_FLAGS.TDF_SHOW_PROGRESS_BAR;
                else if(progressBarType == BarType.Marquee) cfg.dwFlags |= TASKDIALOG_FLAGS.TDF_SHOW_MARQUEE_PROGRESS_BAR;
            }

            cfg.pfCallbackProc = new TaskDialogCallbackProc((hwnd, msg, _, _, _) =>
            {
                if (this.HWND == HWND.NULL)
                {
                    this.HWND = hwnd;
                    if(progressBar)
                    {
                        this.EnableProgressBar();
                        if (progressBarType == BarType.Normal)
                            this.SetProgressBarPercentage(progressBarPercentage);
                        else if (progressBarType == BarType.Marquee)
                            this.EnableMarqueeProgressBar();
                    }
                    
                }
                if (msg == TaskDialogNotification.TDN_BUTTON_CLICKED)
                {
                    if (isDaddyDiag) return 1;
                    // DON'T CANCEL CLOSING
                    return 0;
                }
                if(msg == TaskDialogNotification.TDN_DESTROYED)
                {
                    // FREE ALLOCATED MEMORY
                    if (buttons is not null)
                    {
                        foreach (var button in buttons)
                        {
                            Marshal.FreeHGlobal(button.pszButtonText);
                        }
                        Marshal.FreeHGlobal(cfg.pButtons);
                    }
                    return 0;
                }
                
                return 1;
            });

            task = Task.Factory.StartNew(() => {
                Thread.CurrentThread.Priority = ThreadPriority.Highest;
                TaskDialogIndirect(cfg, out _, out _, out _); 
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            //task.Start();

        }


        public void WaitForHWND()
        {
            SpinWait.SpinUntil(() => HWND != HWND.NULL);
        }

        public Task WaitAsyncForHWND()
        {
            return Task.Run(() => SpinWait.SpinUntil(() => HWND != HWND.NULL));
        }

        public void WaitForTaskDialog()
        {
            task.Wait();
        }

        // Builder syntax functions below
        public TaskDialog Move(int x, int y)
        {
            if (HWND == HWND.NULL) return this;

            SetWindowPos(HWND, HWND.NULL, x - 7, y, 0, 0, SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOZORDER | SetWindowPosFlags.SWP_NOACTIVATE);
            return this;
        }

        public TaskDialog Resize(int width, int height)
        {
            if (HWND == HWND.NULL) return this;

            SetWindowPos(HWND, HWND.NULL, 0, 0, width, height, SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOZORDER | SetWindowPosFlags.SWP_NOACTIVATE);
            return this;
        }

        public TaskDialog ZOrder(HWND insertAfter)
        {
            if (HWND == HWND.NULL) return this;

            SetWindowPos(HWND, insertAfter, 0, 0, 0, 0, SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOACTIVATE);
            return this;
        }


        public void Close()
        {
            SendMessage(HWND, (uint)1126 /*TDM_CLICK_BUTTON*/, IntPtr.Zero /* lit. button 0 */, IntPtr.Zero);
        }

        public static Point GetScreenDimensions()
        {
            return new Point(GetSystemMetrics(SystemMetric.SM_CXVIRTUALSCREEN), GetSystemMetrics(SystemMetric.SM_CYVIRTUALSCREEN));
        }

        private RECT rc { get
            {
                RECT rc = new RECT();
                GetWindowRect(this.HWND, out rc);
                return rc;
            } }

        public int X
        {
            get
            {
                return rc.left;
            }
        }

        public int Y
        {
            get
            {
                return rc.top;
            }
        }

        public int Width
        {
            get
            {
                return rc.Width;
            }
        }

        public int Height
        {
            get
            {
                return rc.Height;
            }
        }

        public static double RelativeToAbsolute(int y, RelativeAxis YA, RelativeAxis SYA, int SelfHeight, int ScreenHeight, bool relativeLast = false, int relativeLastY = 0)
        {
            int relativeY = 0;


            switch (YA)
            {
                case RelativeAxis.Center:
                    relativeY = ScreenHeight / 2;
                    break;
                case RelativeAxis.End:
                    relativeY = ScreenHeight;
                    break;

            }
            switch (SYA)
            {
                case RelativeAxis.Center:
                    relativeY -= SelfHeight / 2;
                    break;
                case RelativeAxis.End:
                    relativeY -= SelfHeight;
                    break;

            }

            relativeY += relativeLast ? relativeLastY : 0;

            return relativeY + y;
        }

        public static double AbsoluteToRelative(int y, RelativeAxis YA, RelativeAxis SYA, int SelfHeight, int ScreenHeight, bool relativeLast = false, int relativeLastY = 0)
        {
            int relativeY = 0;


            switch (YA)
            {
                case RelativeAxis.Center:
                    relativeY -= ScreenHeight / 2;
                    break;
                case RelativeAxis.End:
                    relativeY -= ScreenHeight;
                    break;

            }
            switch (SYA)
            {
                case RelativeAxis.Center:
                    relativeY += SelfHeight / 2;
                    break;
                case RelativeAxis.End:
                    relativeY += SelfHeight;
                    break;

            }

            relativeY -= relativeLast ? relativeLastY : 0;

            return relativeY + y;
        }



        public TaskDialog MoveRelative(int x, int y, RelativeAxis XA, RelativeAxis YA, RelativeAxis SXA, RelativeAxis SYA)
        {
            if (HWND == HWND.NULL) return this;

            Point p = GetScreenDimensions();


            Move((int)RelativeToAbsolute(x,XA,SXA,Width,p.X), (int)RelativeToAbsolute(y, YA, SYA,Height,p.Y));
            return this;
        }

        public void HideCompletely()
        {
            if (HWND == HWND.NULL) return;

            ShowWindow(HWND, ShowWindowCommand.SW_HIDE);
            SetWindowLong(HWND, WindowLongFlags.GWL_EXSTYLE, GetWindowLong(HWND, WindowLongFlags.GWL_EXSTYLE) | (int)WindowStylesEx.WS_EX_TOOLWINDOW); // set the style
            ShowWindow(HWND, ShowWindowCommand.SW_SHOW);
            ShowWindow(HWND, ShowWindowCommand.SW_HIDE);
        }

        public void ShowCompletely()
        {
            if (HWND == HWND.NULL) return;

            SetWindowLong(HWND, WindowLongFlags.GWL_EXSTYLE, GetWindowLong(HWND, WindowLongFlags.GWL_EXSTYLE) ^ (int)WindowStylesEx.WS_EX_TOOLWINDOW); // remove the style
            ShowWindow(HWND, ShowWindowCommand.SW_SHOW);
        }

        public TaskDialog Center()
        {

            MoveRelative(0, 0, RelativeAxis.Center, RelativeAxis.Center, RelativeAxis.Center, RelativeAxis.Center);
            return this;
        }

        public TaskDialog SetProgressBarPercentage(int percentage)
        {
            if (HWND == HWND.NULL) return this;
            SendMessage(this.HWND, 1130 /*TDM_SET_PROGRESS_BAR_POS*/, (IntPtr)percentage, IntPtr.Zero);
            return this;
        }

        public TaskDialog EnableProgressBar()
        {
            if (HWND == HWND.NULL) return this;
            SendMessage(this.HWND, 1128 /*TDM_SET_PROGRESS_BAR_STATE*/, (IntPtr)1 /*PBST_NORMAL*/, IntPtr.Zero);
            return this;
        }

        public TaskDialog EnableMarqueeProgressBar()
        {
            if (HWND == HWND.NULL) return this;
            SendMessage(this.HWND, 1131 /*TDM_SET_PROGRESS_BAR_MARQUEE*/, (IntPtr)1 /*TRUE*/, IntPtr.Zero);
            return this;
        }

    }
    public enum RelativeAxis
    {
        Start,
        Center,
        End
    }



}
