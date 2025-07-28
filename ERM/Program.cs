
using CommandLine;
using System.Diagnostics;
using System.Text.Json.Nodes;
using Vanara.Extensions;
using System.Runtime.InteropServices;
using static Vanara.PInvoke.ComCtl32;
using static Vanara.PInvoke.User32;
using Vanara.PInvoke;
using Shared;
using System.Text.Json;

namespace ERW
{
    public class Options
    {
        [Option("erwsetfile", Required = false)]
        public string FileLocation { get; set; } = string.Empty;
    }

   
    public static class ERWProgram
    {
        static ERWJson? jsonData; 

        public static void Main(string[] args)
        {
            TaskDialog.CreateDaddyDialog();
            Console.ReadLine();
            var ChildDiag = new TaskDialog("Test", "test", icon: TaskDialogIconExtended.TD_ERROR_ICON, buttons: new TASKDIALOG_BUTTON_FIXED[]
           {
                new ()
                {
                    pszButtonText =  Marshal.StringToHGlobalUni("OK"),
                    nButtonID = 1
                }
           });

            ChildDiag.WaitForHWND();
            ChildDiag.Move(500, 500).MoveRelative(0,0,RelativeAxis.Center, RelativeAxis.Center, RelativeAxis.Center, RelativeAxis.Center);
            Console.ReadLine();

            if (File.Exists("erw.json"))
            {
                jsonData = JsonSerializer.Deserialize<ERWJson>(File.ReadAllText("erw.json"));
            } else
            {
                return;
            }

            if (jsonData is null) return;

            


        }
    }
}