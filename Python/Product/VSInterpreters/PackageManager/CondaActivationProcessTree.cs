// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for the specific language governing
// permissions and limitations under the License.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.PythonTools.Interpreter {
    sealed class CondaActivationProcessTree : IDisposable {
        private const uint ProcessSetQuota = 0x0100;
        private const uint ProcessTerminate = 0x0001;
        private const uint ProcessQueryLimitedInformation = 0x1000;
        private const uint Synchronize = 0x00100000;
        private const uint WaitObject0 = 0x00000000;
        private const uint WaitTimeout = 0x00000102;
        private const int ErrorAccessDenied = 5;
        private const int JobObjectBasicAccountingInformation = 1;
        private const int JobObjectExtendedLimitInformation = 9;
        private const uint JobObjectLimitKillOnJobClose = 0x00002000;
        private const int TerminationPollMilliseconds = 25;

        private const string GatedActivationBatch =
            "@echo off\r\n" +
            ":wait_for_ptvs\r\n" +
            "if not exist \"%~1\" goto wait_for_ptvs\r\n" +
            "del /q \"%~1\" >nul 2>&1\r\n" +
            "if \"%~3\"==\"\" (\r\n" +
            "    \"%~2\" & python.exe -c \"%~4\"\r\n" +
            ") else (\r\n" +
            "    \"%~2\" \"%~3\" & python.exe -c \"%~4\"\r\n" +
            ")\r\n" +
            "exit /b %errorlevel%\r\n";

        private readonly ProcessOutput _process;
        private readonly SafeKernelHandle _rootProcess;
        private readonly SafeKernelHandle _job;
        private readonly string _temporaryDirectory;
        private readonly string _gatePath;
        private bool _released;
        private bool _terminationRequested;
        private bool _disposed;

        private CondaActivationProcessTree(
            ProcessOutput process,
            SafeKernelHandle rootProcess,
            SafeKernelHandle job,
            string temporaryDirectory,
            string gatePath
        ) {
            _process = process;
            _rootProcess = rootProcess;
            _job = job;
            _temporaryDirectory = temporaryDirectory;
            _gatePath = gatePath;
        }

        public ProcessOutput Process => _process;

        public static CondaActivationProcessTree TryStart(string activateBat, string prefixPath, string printEnvironmentCode) {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) {
                return null;
            }

            SafeKernelHandle job = null;
            SafeKernelHandle rootProcess = null;
            ProcessOutput process = null;
            string temporaryDirectory = null;

            try {
                job = CreateJobObject(IntPtr.Zero, null);
                if (job == null || job.IsInvalid) {
                    TraceWin32Failure("create a cleanup job for Conda activation", Marshal.GetLastWin32Error());
                    job?.Dispose();
                    return null;
                }

                var limits = new JobObjectExtendedLimit {
                    BasicLimitInformation = new JobObjectBasicLimit {
                        LimitFlags = JobObjectLimitKillOnJobClose
                    }
                };
                if (!SetInformationJobObject(
                    job,
                    JobObjectExtendedLimitInformation,
                    ref limits,
                    Marshal.SizeOf(typeof(JobObjectExtendedLimit))
                )) {
                    TraceWin32Failure("configure the Conda activation cleanup job", Marshal.GetLastWin32Error());
                    job.Dispose();
                    return null;
                }

                temporaryDirectory = Path.Combine(Path.GetTempPath(), "ptvs-conda-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(temporaryDirectory);

                var wrapperPath = Path.Combine(temporaryDirectory, "activate.cmd");
                var gatePath = Path.Combine(temporaryDirectory, "ready");
                File.WriteAllText(wrapperPath, GatedActivationBatch, Encoding.ASCII);

                process = ProcessOutput.RunHiddenAndCapture(
                    wrapperPath,
                    EscapeForBatchExpansion(gatePath),
                    EscapeForBatchExpansion(activateBat),
                    EscapeForBatchExpansion(prefixPath ?? string.Empty),
                    EscapeForBatchExpansion(printEnvironmentCode)
                );

                var processId = process.ProcessId;
                if (!processId.HasValue || process.ExitCode.HasValue) {
                    Trace.TraceWarning("Unable to start the gated Conda activation process.");
                    process.Dispose();
                    job.Dispose();
                    DeleteTemporaryDirectory(temporaryDirectory);
                    return null;
                }

                rootProcess = OpenProcess(
                    ProcessTerminate | ProcessQueryLimitedInformation | ProcessSetQuota | Synchronize,
                    false,
                    processId.Value
                );
                if (rootProcess == null || rootProcess.IsInvalid) {
                    TraceWin32Failure("open the gated Conda activation process", Marshal.GetLastWin32Error());
                    rootProcess?.Dispose();
                    TerminateFailedStart(process, null);
                    process.Dispose();
                    job.Dispose();
                    DeleteTemporaryDirectory(temporaryDirectory);
                    return null;
                }

                if (process.ExitCode.HasValue) {
                    rootProcess.Dispose();
                    process.Dispose();
                    job.Dispose();
                    DeleteTemporaryDirectory(temporaryDirectory);
                    return null;
                }

                if (!AssignProcessToJobObject(job, rootProcess)) {
                    TraceWin32Failure("assign the gated Conda activation process to its cleanup job", Marshal.GetLastWin32Error());
                    TerminateFailedStart(process, rootProcess);
                    rootProcess.Dispose();
                    process.Dispose();
                    job.Dispose();
                    DeleteTemporaryDirectory(temporaryDirectory);
                    return null;
                }

                return new CondaActivationProcessTree(process, rootProcess, job, temporaryDirectory, gatePath);
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                Trace.TraceWarning("Unable to prepare gated Conda activation: {0}", ex.Message);
                TerminateFailedStart(process, rootProcess);
                rootProcess?.Dispose();
                process?.Dispose();
                job?.Dispose();
                DeleteTemporaryDirectory(temporaryDirectory);
                return null;
            }
        }

        public bool TryRelease() {
            if (_disposed) {
                throw new ObjectDisposedException(nameof(CondaActivationProcessTree));
            }
            if (_released) {
                return true;
            }

            try {
                File.WriteAllText(_gatePath, string.Empty);
                _released = true;
                return true;
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                Trace.TraceWarning("Unable to release the gated Conda activation process: {0}", ex.Message);
                Terminate(TimeSpan.FromSeconds(2));
                return false;
            }
        }

        public void Terminate(TimeSpan timeout) {
            if (_disposed) {
                throw new ObjectDisposedException(nameof(CondaActivationProcessTree));
            }
            if (_terminationRequested) {
                WaitForJobExit(timeout);
                return;
            }

            _terminationRequested = true;
            if (!TerminateJobObject(_job, 1)) {
                var error = Marshal.GetLastWin32Error();
                uint activeProcesses;
                if (!TryGetActiveProcessCount(out activeProcesses) || activeProcesses > 0) {
                    if (error != ErrorAccessDenied) {
                        TraceWin32Failure("terminate the Conda activation cleanup job", error);
                    }
                    TryTerminateManagedRoot(_process);
                    TryTerminateNativeRoot(_rootProcess);
                }
            }

            WaitForJobExit(timeout);
        }

        private void WaitForJobExit(TimeSpan timeout) {
            var stopwatch = Stopwatch.StartNew();
            uint activeProcesses;

            while (stopwatch.Elapsed < timeout) {
                if (!TryGetActiveProcessCount(out activeProcesses)) {
                    var waitRemaining = timeout - stopwatch.Elapsed;
                    if (waitRemaining > TimeSpan.Zero) {
                        WaitForSingleObject(_rootProcess, (uint)Math.Ceiling(waitRemaining.TotalMilliseconds));
                    }
                    return;
                }

                if (activeProcesses == 0) {
                    return;
                }

                var remaining = timeout - stopwatch.Elapsed;
                if (remaining > TimeSpan.Zero) {
                    Thread.Sleep(Math.Min(TerminationPollMilliseconds, (int)Math.Ceiling(remaining.TotalMilliseconds)));
                }
            }

            if (TryGetActiveProcessCount(out activeProcesses) && activeProcesses > 0) {
                Trace.TraceWarning(
                    "Conda activation process-tree cleanup reached its {0} ms bound with {1} process(es) still running.",
                    timeout.TotalMilliseconds,
                    activeProcesses
                );
            }
        }

        private bool TryGetActiveProcessCount(out uint activeProcesses) {
            JobObjectBasicAccounting accounting;
            if (QueryInformationJobObject(
                _job,
                JobObjectBasicAccountingInformation,
                out accounting,
                Marshal.SizeOf(typeof(JobObjectBasicAccounting)),
                IntPtr.Zero
            )) {
                activeProcesses = accounting.ActiveProcesses;
                return true;
            }

            activeProcesses = 0;
            TraceWin32Failure("query the Conda activation cleanup job", Marshal.GetLastWin32Error());
            return false;
        }

        private static void TerminateFailedStart(ProcessOutput process, SafeKernelHandle rootProcess) {
            TryTerminateManagedRoot(process);
            TryTerminateNativeRoot(rootProcess);
            if (rootProcess != null && !rootProcess.IsInvalid) {
                WaitForSingleObject(rootProcess, 1000);
            }
        }

        private static void TryTerminateManagedRoot(ProcessOutput process) {
            try {
                process?.Kill();
            } catch (InvalidOperationException) {
            } catch (Win32Exception ex) {
                if (ex.NativeErrorCode != ErrorAccessDenied) {
                    TraceWin32Failure("terminate the Conda activation wrapper", ex.NativeErrorCode);
                }
            }
        }

        private static void TryTerminateNativeRoot(SafeKernelHandle rootProcess) {
            if (rootProcess == null || rootProcess.IsInvalid || WaitForSingleObject(rootProcess, 0) == WaitObject0) {
                return;
            }

            if (!TerminateProcess(rootProcess, 1)) {
                var error = Marshal.GetLastWin32Error();
                if (error != ErrorAccessDenied && WaitForSingleObject(rootProcess, 0) != WaitObject0) {
                    TraceWin32Failure("terminate the Conda activation wrapper", error);
                }
            }
        }

        private static void TraceWin32Failure(string operation, int error) {
            Trace.TraceWarning("Unable to {0}. Win32 error: {1}.", operation, error);
        }

        private static string EscapeForBatchExpansion(string value) {
            return value.Replace("^", "^^").Replace("&", "^&");
        }

        private static void DeleteTemporaryDirectory(string path) {
            if (string.IsNullOrEmpty(path)) {
                return;
            }

            try {
                Directory.Delete(path, true);
            } catch (IOException) {
            } catch (UnauthorizedAccessException) {
            }
        }

        public void Dispose() {
            if (_disposed) {
                return;
            }

            if (WaitForSingleObject(_rootProcess, 0) == WaitTimeout) {
                Terminate(TimeSpan.FromSeconds(2));
            }

            _disposed = true;
            _process.Dispose();
            _rootProcess.Dispose();
            _job.Dispose();
            DeleteTemporaryDirectory(_temporaryDirectory);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectBasicAccounting {
            public long TotalUserTime;
            public long TotalKernelTime;
            public long ThisPeriodTotalUserTime;
            public long ThisPeriodTotalKernelTime;
            public uint TotalPageFaultCount;
            public uint TotalProcesses;
            public uint ActiveProcesses;
            public uint TotalTerminatedProcesses;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectBasicLimit {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IoCounters {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectExtendedLimit {
            public JobObjectBasicLimit BasicLimitInformation;
            public IoCounters IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        private sealed class SafeKernelHandle : SafeHandleZeroOrMinusOneIsInvalid {
            public SafeKernelHandle() : base(true) {
            }

            protected override bool ReleaseHandle() {
                return CloseHandle(handle);
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeKernelHandle OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TerminateProcess(SafeKernelHandle process, uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(SafeKernelHandle handle, uint milliseconds);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern SafeKernelHandle CreateJobObject(IntPtr jobAttributes, string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AssignProcessToJobObject(SafeKernelHandle job, SafeKernelHandle process);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TerminateJobObject(SafeKernelHandle job, uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetInformationJobObject(
            SafeKernelHandle job,
            int informationClass,
            ref JobObjectExtendedLimit jobObjectInformation,
            int jobObjectInformationLength
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryInformationJobObject(
            SafeKernelHandle job,
            int informationClass,
            out JobObjectBasicAccounting jobObjectInformation,
            int jobObjectInformationLength,
            IntPtr returnLength
        );
    }
}
