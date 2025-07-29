using ERW;
using NCalc.Domain;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;

namespace Shared
{
    public class Player : INotifyPropertyChanged
    {
        public ERWJson Config { get; set; }
        public event PropertyChangedEventHandler? PropertyChanged;

        private ERWTimestamp? last;
        private List<TaskDialog> opened = new List<TaskDialog>();
        private IEnumerator<ERWTimestamp>? enumerator;
        private int animIndex = 0;
        public bool Playing { get; set; } = false;

        public delegate void StopEvent();
        public event StopEvent OnStop;

        public Player(ERWJson config)
        {
            Config = config;
        }

        public void Play(TimeSpan AudioPosition)
        {
            enumerator = Config.Timestamps.OrderBy((t) => t.Timestamp).ToList().GetEnumerator();
            enumerator.MoveNext();
            last = enumerator.Current;
            while (last is not null && last.Timestamp < AudioPosition)
            {
                enumerator.MoveNext();
                last = enumerator.Current;
            }
            Playing = true;
        }

        public void Stop()
        {
            Playing = false;
            opened.ForEach((x) => x.Close());
            opened.Clear();


            if(OnStop is not null) OnStop.Invoke();
        }

        public void OnAudioPositionChanged(TimeSpan AudioPosition)
        {
            if (!Playing) return;
            if (last is null) return;
            if (enumerator == null) return;

            if (AudioPosition > last.Timestamp)
            {
                var actual = last;
                enumerator.MoveNext();
                last = enumerator.Current;


                var random = new Random();
                switch (actual.Action)
                {
                    case ERWActionEnum.ShowWindow:
                        {
                            var act = actual.Data as ERWShowWindow;
                            Task.Run(() =>
                            {
                                var wnd = Config.DefinedWindows.FirstOrDefault((d) => d.Identifier == act.WindowIdentifier, null);
                                if (wnd is null) return;
                                var task = new ERW.TaskDialog(wnd, act);
                                opened.Add(task);
                                task.WaitAsyncForHWND().ContinueWith((t) =>
                                {
                                    if (act.IsRelative)
                                    {
                                        task.MoveRelative(act.X, act.Y, act.XAxis, act.YAxis, act.SelfXAxis, act.SelfYAxis);
                                    }
                                    else
                                    {
                                        task.Move(act.X, act.Y);
                                    }
                                });

                            });
                            break;
                        }
                    case ERWActionEnum.ClearWindow:
                        {
                            var act = actual.Data as ERWClearWindow;
                            var toClose = string.IsNullOrEmpty(act.Group) ? opened : opened.Where(x => x.AssociatedAction?.Group == act.Group);

                            toClose.ToList().ForEach(x => x.Close());
                            opened = string.IsNullOrEmpty(act.Group) ? new List<TaskDialog>() : opened.Where(x => x.AssociatedAction?.Group != act.Group).ToList();
                            break;
                        }
                    case ERWActionEnum.SetPercentage:
                        {
                            var act = actual.Data as ERWSetPercentage;
                            var toClose = string.IsNullOrEmpty(act.Group) ? opened : opened.Where(x => x.AssociatedAction?.Group == act.Group);

                            toClose.ToList().ForEach(x => x.SetProgressBarPercentage(act.Percentage));
                            break;
                        }
                    case ERWActionEnum.SetVisibility:
                        {
                            var act = actual.Data as ERWSetVisibility;
                            var toClose = string.IsNullOrEmpty(act.Group) ? opened : opened.Where(x => x.AssociatedAction?.Group == act.Group);

                            toClose.ToList().ForEach(x => {
                                if (act.Visible) x.ShowCompletely(); else x.HideCompletely();
                            });
                            break;
                        }
                    case ERWActionEnum.GoTo:
                        {
                            var act = actual.Data as ERWGoTo;
                            var toClose = string.IsNullOrEmpty(act.Group) ? opened : opened.Where(x => x.AssociatedAction?.Group == act.Group);


                            toClose.ToList().ForEach(x =>
                            {
                                if (act.Random) x.Move((int)random.NextInt64(0, (int)SystemParameters.VirtualScreenWidth - x.Width), (int)random.NextInt64(0, (int)SystemParameters.VirtualScreenHeight - x.Height));
                                else x.Move(act.X, act.Y);
                            });
                            break;
                        }
                    case ERWActionEnum.Animate:
                        {
                            var act = actual.Data as ERWAnimate;
                            var toClose = string.IsNullOrEmpty(act.Group) ? opened : opened.Where(x => x.AssociatedAction?.Group == act.Group);

                            toClose.ToList().ForEach(x =>
                            {

                                int targetFPS = Math.Max(Math.Min(60, act.FPS), 1);
                                double targetFrameTime = 1000.0 / targetFPS;

                                Stopwatch stopwatch = new Stopwatch();
                                stopwatch.Start();
                                stopwatch.Restart();

                                long lastFrameTime = stopwatch.ElapsedMilliseconds;

                                var randomUnique = random.NextDouble();

                                var myIndex = animIndex++;

                                Task.Run(() =>
                                {
                                    while (stopwatch.ElapsedMilliseconds < act.Duration * 1000)
                                    {
                                        long currentTime = stopwatch.ElapsedMilliseconds;
                                        long elapsed = currentTime - lastFrameTime;

                                        if (elapsed >= targetFrameTime)
                                        {
                                            lastFrameTime = currentTime;

                                            int xp = x.X, yp = x.Y, perc = -1;

                                            var args = new object[]
                                            {
                                                AudioPosition.TotalMilliseconds / 1000.0,
                                                stopwatch.ElapsedMilliseconds/1000.0,
                                                randomUnique,
                                                myIndex
                                            };

                                            if (!string.IsNullOrEmpty(act.X))
                                                xp = Convert.ToInt32(new NCalc.Expression(string.Format(act.X, [.. args, random.NextDouble()])).Evaluate());
                                            if (!string.IsNullOrEmpty(act.Y))
                                                yp = Convert.ToInt32(new NCalc.Expression(string.Format(act.Y, [.. args, random.NextDouble()])).Evaluate());
                                            if (!string.IsNullOrEmpty(act.Percentage))
                                                perc = Convert.ToInt32(new NCalc.Expression(string.Format(act.Percentage, [.. args, random.NextDouble()])).Evaluate());

                                            if (xp != x.X || yp != x.Y) x.Move(xp, yp);
                                            if (perc != -1) x.SetProgressBarPercentage(perc);
                                        }
                                        else
                                        {
                                            int sleepTime = (int)(targetFrameTime - elapsed);
                                            if (sleepTime > 12)
                                                Thread.Sleep(sleepTime - 1);
                                            else
                                                Thread.Yield();
                                        }
                                    }
                                });
                            });
                            break;
                        }
                    case ERWActionEnum.Stop:
                        {
                            Stop();
                            break;
                        }
                }

            }
        }
    }
}
