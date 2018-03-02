using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ExternalProfilerDriver
{
    class ProcessAsyncRunner
    {
        static public Task<Process> Run(string execfname,
            string args = null,
            CancellationToken cancellationToken = default(CancellationToken),
            IProgress<ProcessProgressDataload> progress = null
            )
        {
            TaskCompletionSource<Process> taskCS = new TaskCompletionSource<Process>();

            bool stdoutToProgress = (progress != null);

            ProcessStartInfo psi = new ProcessStartInfo(execfname)
            {
                UseShellExecute = false,
                Arguments = args,
                CreateNoWindow = false,
                RedirectStandardOutput = stdoutToProgress
            };
            psi.EnvironmentVariables["AMPLXE_EXPERIMENTAL"] = "time-cl";

            Process process = new Process
            {
                StartInfo = psi,
                EnableRaisingEvents = true
            };

            process.Exited += (sender, localEventArgs) =>
            {
                taskCS.SetResult(process);
            };

            if (progress != null)
            {
                process.OutputDataReceived += (sender, localEventArgs) =>
                {
                    ProcessProgressDataload e = new ProcessProgressDataload { Message = localEventArgs.Data };
                    progress.Report(e);
                };
            }
            if (cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            process.Start();

            if (progress != null)
            {
                process.BeginOutputReadLine();
            }
            cancellationToken.Register(() => {
                process.CloseMainWindow();
                cancellationToken.ThrowIfCancellationRequested();
            });

            return taskCS.Task;
        }

        static public void RunWrapper(string exe, string args)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            var progress = new Progress<ProcessProgressDataload>();
            progress.ProgressChanged += (s, e) => {
                //Console.SetCursorPosition(Console.CursorTop + 2, 0);
                Console.WriteLine($"From process: {e.Message}");
            };

            try
            {
                Task<Process> t = ProcessAsyncRunner.Run(exe, args, cts.Token, progress);

                bool processRunning = true;
                t.ContinueWith((p) => { processRunning = false; });

                int count = 0;
                while (processRunning)
                {
#if false
                    Console.SetCursorPosition(Console.CursorTop, 25);
                    Console.Write($"{count}, ");
                    Console.SetCursorPosition(Console.CursorTop + 1, 0);
                    count++;
#endif
                }
                Console.WriteLine("\nDone with the task!");
                t.Wait();
            }
            catch (OperationCanceledException ce)
            {
                Console.WriteLine($"Operation was cancelled with message: {ce.Message}");
            }
            finally
            {
                Console.WriteLine("Operation completed successfully");
            }
        }
    }

    class ProcessProgressDataload
    {
        public string Message { get; set; }
    }
}
