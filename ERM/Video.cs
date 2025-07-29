using Emgu.CV.Ocl;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Vanara.PInvoke;
using static Vanara.PInvoke.Gdi32;
using static Vanara.PInvoke.Kernel32;
using static Vanara.PInvoke.OleAut32.PICTDESC.PICTDEC_UNION;
using static Vanara.PInvoke.Shell32;
using static Vanara.PInvoke.User32;
namespace ERW
{
    internal class Video
    {


        public static HWND FindWindowRecursive(HWND hParent, string? szClass, string? szCaption)
        {
            HWND hResult = FindWindowEx(hParent, HWND.NULL, szClass, szCaption);
            if (hResult != HWND.NULL)
                return hResult;
            HWND hChild = FindWindowEx(hParent, HWND.NULL, null, null);
            if (hChild != HWND.NULL)
            {
                do
                {
                    hResult = FindWindowRecursive(hChild, szClass, szCaption);
                    if (hResult != HWND.NULL)
                        return hResult;
                } while ((hChild = GetWindow(hChild, GetWindowCmd.GW_HWNDNEXT)) != HWND.NULL);
                return HWND.NULL;
            }
            else
                return HWND.NULL;
        }
        public static VisibleWindow window;
        public static WindowClass c;
        public static WindowProc wproc;
        /* Thanks to https://github.com/rocksdanister/lively/issues/2074#issuecomment-3017842549 */
        public static HWND LoadWindow()
        {
            HWND hProgman = FindWindow("Progman", null);
            nint a = 0;
            SendMessageTimeout(hProgman, 0x052C, 0, 0, SMTO.SMTO_NORMAL, 500, ref a);

            HWND hShellView = FindWindowEx(hProgman, 0, "ShellDLL_DefView");
            HWND hWorkerW = FindWindowEx(hProgman, 0, "WorkerW");

            WindowStylesEx exstyle = WindowStylesEx.WS_EX_LAYERED | WindowStylesEx.WS_EX_TRANSPARENT | WindowStylesEx.WS_EX_TOOLWINDOW | WindowStylesEx.WS_EX_NOACTIVATE;

            wproc = (hWnd, msg, wParam, lParam) =>
            {
                if(msg == (uint)WindowMessage.WM_NCHITTEST)
                {
                    return (nint)HitTestValues.HTTRANSPARENT;
                }
                return DefWindowProc(hWnd, msg, wParam, lParam);
            };

            c = WindowClass.MakeVisibleWindowClass("ERW_Desktop", wproc);

            window = new(c, null,
                new((int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight),
                new(0, 0), WindowStyles.WS_CHILD, exstyle, hProgman);
            //window.CreateHandle(c, null,
            //    new((int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight),
            //    new(0, 0), WindowStyles.WS_CHILD, exstyle, hProgman);


            HWND hLiveWP = window.Handle;





            SetLayeredWindowAttributes(hLiveWP, 0, 255, LayeredWindowAttributes.LWA_ALPHA);

            SetWindowPos(hLiveWP, hShellView, 0, 0, 0, 0,
                         SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOACTIVATE);
            SetWindowPos(hWorkerW, hLiveWP, 0, 0, 0, 0,
                         SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOACTIVATE);

            ShowWindow(hLiveWP, ShowWindowCommand.SW_SHOW);

            return hLiveWP;
        }
    }
}
