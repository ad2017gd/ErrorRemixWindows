using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Vanara.PInvoke;
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


        public static HWND LoadWindow()
        {
            HWND window = FindWindowRecursive(HWND.NULL, "Progman", null);

            // Create a window under the desktop icons, but over the desktop
            IntPtr _ = IntPtr.Zero;
            SendMessageTimeout(window,0x052C,IntPtr.Zero,IntPtr.Zero,User32.SMTO.SMTO_NORMAL,1000,ref _);

            // Find the window we just created
            HWND wnd = HWND.NULL;
            EnumWindows(new EnumWindowsProc((tophandle, topparamhandle) =>
            {
                HWND p = FindWindowEx(tophandle,
                                            HWND.NULL,
                                            "SHELLDLL_DefView",
                                            null);

                if (p != HWND.NULL)
                {
                    // Gets the WorkerW Window after the current one.
                    wnd = FindWindowEx(HWND.NULL,
                                               tophandle,
                                               "WorkerW",
                                               null);
                }

                return true;
            }), IntPtr.Zero);

            return wnd;
        }
    }
}
