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
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.PythonTools.Interpreter {
    sealed class CondaActivationProcessTree : IDisposable {
        private const uint ProcessSetQuota = 0x0100;
        private const uint ProcessTerminate = 0x0001;
        private const uint ProcessQueryLimitedInformation = 0x1000;
        private const uint Synchronize = 0x00100000;
        private const uint Th32csSnapProcess = 0x00000002;
        private const uint WaitObject0 = 0x00000000;
        private const uint WaitTimeout = 0x00000102;
        private const uint WaitFailed = 0xffffffff;
        private const int ErrorAccessDenied = 5;
        private const int ErrorNoMoreFiles = 18;
        private const int ErrorBadLength = 24;
        private const int ErrorInvalidParameter = 87;
        private const int MaxSnapshotAttempts = 3;
        private const int MaxTerminationPasses = 32;
        private const int TerminationPollMilliseconds = 25;

        private readonly Dictionary<int, TrackedProcess> _processes;
        private readonly TrackedProcess _root;
        private SafeKernelHandle _job;
        private int? _jobAssignmentError;
        private bool _disposed;

        private CondaActivationProcessTree(TrackedProcess root) {
            _root = root;
            _processes = new Dictionary<int, TrackedProcess> {
                { root.ProcessId, root }
            };
        }

        public static CondaActivationProcessTree TryCreate(ProcessOutput process) {
            if (process == null || Environment.OSVersion.Platform != PlatformID.Win32NT) {
                return null;
            }

            CondaActivationProcessTree processTree = null;
            SafeKernelHandle handle = null;
            try {
                var processId = process.ProcessId;
                if (!processId.HasValue || process.ExitCode.HasValue) {
                    return null;
                }

                int openError;
                handle = OpenTrackedProcess(processId.Value, true, out openError);
                if (handle == null) {
                    TraceProcessFailure("open", processId.Value, openError);
                    return null;
                }

                long creationTime;
                if (!TryGetProcessCreationTime(handle, out creationTime)) {
                    var error = Marshal.GetLastWin32Error();
                    handle.Dispose();
                    TraceProcessFailure("query", processId.Value, error);
                    return null;
                }

                if (process.ExitCode.HasValue) {
                    handle.Dispose();
                    handle = null;
                    return null;
                }

                processTree = new CondaActivationProcessTree(new TrackedProcess(processId.Value, creationTime, 0, handle));
                handle = null;

                // The job contains descendants created after assignment. The initial
                // snapshot holds safe handles to children that may have started first.
                processTree.TryAssignJob();
                processTree.DiscoverDescendants();
                return processTree;
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                processTree?.Dispose();
                handle?.Dispose();
                Trace.TraceWarning("Unable to track the Conda activation process tree: {0}", ex.Message);
                return null;
            }
        }

        public void Terminate(ProcessOutput process, TimeSpan timeout) {
            if (_disposed) {
                throw new ObjectDisposedException(nameof(CondaActivationProcessTree));
            }

            var stopwatch = Stopwatch.StartNew();
            try {
                DiscoverDescendants();
                TryTerminateJob();
                TryTerminateManagedRoot(process);
                TerminateTrackedProcesses();

                var stablePasses = 0;
                for (var pass = 0; pass < MaxTerminationPasses && stopwatch.Elapsed < timeout; pass++) {
                    var added = DiscoverDescendants();
                    TerminateTrackedProcesses();

                    if (added == 0 && _processes.Values.All(p => p.HasExited)) {
                        stablePasses += 1;
                        if (stablePasses >= 2) {
                            break;
                        }
                    } else {
                        stablePasses = 0;
                    }

                    var remaining = timeout - stopwatch.Elapsed;
                    if (remaining > TimeSpan.Zero) {
                        Thread.Sleep(Math.Min(TerminationPollMilliseconds, (int)Math.Ceiling(remaining.TotalMilliseconds)));
                    }
                }

                var remainingProcesses = _processes.Values.Where(p => !p.HasExited).Select(p => p.ProcessId).ToArray();
                if (remainingProcesses.Length > 0) {
                    Trace.TraceWarning(
                        "Conda activation process-tree cleanup reached its {0} ms bound with {1} tracked process(es) still running.",
                        timeout.TotalMilliseconds,
                        remainingProcesses.Length
                    );
                }
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                Trace.TraceWarning("Conda activation process-tree cleanup failed: {0}", ex.Message);
                TryTerminateManagedRoot(process);
                foreach (var trackedProcess in _processes.Values.ToArray()) {
                    try {
                        trackedProcess.TryTerminate();
                    } catch (Exception terminationException) when (!terminationException.IsCriticalException()) {
                        Trace.TraceWarning(
                            "Unable to terminate a tracked Conda activation process during fallback cleanup: {0}",
                            terminationException.Message
                        );
                    }
                }
            }
        }

        private void TryAssignJob() {
            _job = CreateJobObject(IntPtr.Zero, null);
            if (_job == null || _job.IsInvalid) {
                _jobAssignmentError = Marshal.GetLastWin32Error();
                _job?.Dispose();
                _job = null;
                return;
            }

            if (!AssignProcessToJobObject(_job, _root.Handle)) {
                _jobAssignmentError = Marshal.GetLastWin32Error();
                _job.Dispose();
                _job = null;
            }
        }

        private void TryTerminateJob() {
            if (_job == null) {
                if (_jobAssignmentError.HasValue) {
                    Trace.TraceWarning(
                        "Conda activation process could not be assigned to a cleanup job. Win32 error: {0}. Falling back to validated process-tree enumeration.",
                        _jobAssignmentError.Value
                    );
                }
                return;
            }

            if (!TerminateJobObject(_job, 1)) {
                var error = Marshal.GetLastWin32Error();
                if (!_root.HasExited) {
                    TraceProcessFailure("terminate job for", _root.ProcessId, error);
                }
            }
        }

        private static void TryTerminateManagedRoot(ProcessOutput process) {
            try {
                process?.Kill();
            } catch (InvalidOperationException) {
            } catch (Win32Exception ex) {
                if (ex.NativeErrorCode != ErrorAccessDenied) {
                    TraceProcessFailure("terminate", process?.ProcessId ?? 0, ex.NativeErrorCode);
                }
            }
        }

        private void TerminateTrackedProcesses() {
            foreach (var process in _processes.Values.OrderBy(p => p.Depth).ToArray()) {
                process.TryTerminate();
            }
        }

        private int DiscoverDescendants() {
            long snapshotTime;
            var childrenByParent = CaptureProcessSnapshot(out snapshotTime);
            if (childrenByParent == null) {
                return 0;
            }

            var added = 0;
            var pending = new Queue<TrackedProcess>(_processes.Values);
            var inspected = new HashSet<int>();

            while (pending.Count > 0) {
                var parent = pending.Dequeue();
                if (!inspected.Add(parent.ProcessId)) {
                    continue;
                }

                long latestChildCreationTime;
                if (!parent.TryGetLatestChildCreationTime(snapshotTime, out latestChildCreationTime)) {
                    continue;
                }

                List<int> childProcessIds;
                if (!childrenByParent.TryGetValue(parent.ProcessId, out childProcessIds)) {
                    continue;
                }

                foreach (var childProcessId in childProcessIds) {
                    TrackedProcess existing;
                    if (_processes.TryGetValue(childProcessId, out existing)) {
                        pending.Enqueue(existing);
                        continue;
                    }

                    TrackedProcess child;
                    if (TryOpenDescendant(childProcessId, parent, snapshotTime, latestChildCreationTime, out child)) {
                        _processes.Add(childProcessId, child);
                        pending.Enqueue(child);
                        added += 1;
                    }
                }
            }

            return added;
        }

        private static bool TryOpenDescendant(
            int processId,
            TrackedProcess parent,
            long snapshotTime,
            long latestChildCreationTime,
            out TrackedProcess process
        ) {
            process = null;

            int openError;
            var handle = OpenTrackedProcess(processId, false, out openError);
            if (handle == null) {
                if (openError != ErrorInvalidParameter) {
                    TraceProcessFailure("open descendant", processId, openError);
                }
                return false;
            }

            long creationTime;
            if (!TryGetProcessCreationTime(handle, out creationTime)) {
                var error = Marshal.GetLastWin32Error();
                handle.Dispose();
                if (error != ErrorInvalidParameter) {
                    TraceProcessFailure("query descendant", processId, error);
                }
                return false;
            }

            if (creationTime < parent.CreationTime || creationTime > snapshotTime || creationTime > latestChildCreationTime) {
                handle.Dispose();
                return false;
            }

            // The handle pins this exact process instance. Comparing creation times
            // prevents a PID reused after the snapshot or parent exit from being killed.
            process = new TrackedProcess(processId, creationTime, parent.Depth + 1, handle);
            return true;
        }

        private static SafeKernelHandle OpenTrackedProcess(int processId, bool allowJobAssignment, out int error) {
            var access = ProcessTerminate | ProcessQueryLimitedInformation | Synchronize;
            if (allowJobAssignment) {
                access |= ProcessSetQuota;
            }

            var handle = OpenProcess(access, false, processId);
            if (handle != null && !handle.IsInvalid) {
                error = 0;
                return handle;
            }

            error = Marshal.GetLastWin32Error();
            handle?.Dispose();

            if (allowJobAssignment && error == ErrorAccessDenied) {
                handle = OpenProcess(ProcessTerminate | ProcessQueryLimitedInformation | Synchronize, false, processId);
                if (handle != null && !handle.IsInvalid) {
                    error = ErrorAccessDenied;
                    return handle;
                }

                error = Marshal.GetLastWin32Error();
                handle?.Dispose();
            }

            return null;
        }

        private static Dictionary<int, List<int>> CaptureProcessSnapshot(out long snapshotTime) {
            snapshotTime = 0;
            SafeKernelHandle snapshot = null;
            var error = 0;

            for (var attempt = 0; attempt < MaxSnapshotAttempts; attempt++) {
                snapshot = CreateToolhelp32Snapshot(Th32csSnapProcess, 0);
                if (snapshot != null && !snapshot.IsInvalid) {
                    break;
                }

                error = Marshal.GetLastWin32Error();
                snapshot?.Dispose();
                snapshot = null;
                if (error != ErrorBadLength) {
                    break;
                }
            }

            if (snapshot == null) {
                Trace.TraceWarning("Unable to enumerate the Conda activation process tree. Win32 error: {0}.", error);
                return null;
            }

            using (snapshot) {
                NativeFileTime currentTime;
                GetSystemTimeAsFileTime(out currentTime);
                snapshotTime = currentTime.ToInt64();

                var childrenByParent = new Dictionary<int, List<int>>();
                var entry = new ProcessEntry32 {
                    Size = (uint)Marshal.SizeOf(typeof(ProcessEntry32))
                };

                if (!Process32First(snapshot, ref entry)) {
                    error = Marshal.GetLastWin32Error();
                    if (error != ErrorNoMoreFiles) {
                        Trace.TraceWarning("Unable to read the Conda activation process snapshot. Win32 error: {0}.", error);
                    }
                    return childrenByParent;
                }

                do {
                    if (entry.ProcessId <= int.MaxValue && entry.ParentProcessId <= int.MaxValue) {
                        var processId = (int)entry.ProcessId;
                        var parentProcessId = (int)entry.ParentProcessId;

                        List<int> children;
                        if (!childrenByParent.TryGetValue(parentProcessId, out children)) {
                            children = new List<int>();
                            childrenByParent.Add(parentProcessId, children);
                        }
                        children.Add(processId);
                    }
                } while (Process32Next(snapshot, ref entry));

                error = Marshal.GetLastWin32Error();
                if (error != ErrorNoMoreFiles) {
                    Trace.TraceWarning("Conda activation process enumeration ended early. Win32 error: {0}.", error);
                }

                return childrenByParent;
            }
        }

        private static bool TryGetProcessCreationTime(SafeKernelHandle handle, out long creationTime) {
            NativeFileTime created;
            NativeFileTime exited;
            NativeFileTime kernel;
            NativeFileTime user;
            if (GetProcessTimes(handle, out created, out exited, out kernel, out user)) {
                creationTime = created.ToInt64();
                return true;
            }

            creationTime = 0;
            return false;
        }

        private static void TraceProcessFailure(string operation, int processId, int error) {
            if (error == ErrorInvalidParameter) {
                return;
            }

            Trace.TraceWarning(
                "Unable to {0} Conda activation process {1}. Win32 error: {2}.",
                operation,
                processId,
                error
            );
        }

        public void Dispose() {
            if (_disposed) {
                return;
            }

            _disposed = true;
            _job?.Dispose();
            foreach (var process in _processes.Values) {
                process.Dispose();
            }
        }

        private sealed class TrackedProcess : IDisposable {
            public TrackedProcess(int processId, long creationTime, int depth, SafeKernelHandle handle) {
                ProcessId = processId;
                CreationTime = creationTime;
                Depth = depth;
                Handle = handle;
            }

            public int ProcessId { get; }

            public long CreationTime { get; }

            public int Depth { get; }

            public SafeKernelHandle Handle { get; }

            public bool HasExited {
                get {
                    var result = WaitForSingleObject(Handle, 0);
                    if (result == WaitObject0) {
                        return true;
                    }
                    if (result == WaitFailed) {
                        TraceProcessFailure("wait for", ProcessId, Marshal.GetLastWin32Error());
                    }
                    return false;
                }
            }

            public bool TryGetLatestChildCreationTime(long snapshotTime, out long latestChildCreationTime) {
                var result = WaitForSingleObject(Handle, 0);
                if (result == WaitTimeout) {
                    latestChildCreationTime = snapshotTime;
                    return true;
                }

                if (result == WaitObject0) {
                    NativeFileTime created;
                    NativeFileTime exited;
                    NativeFileTime kernel;
                    NativeFileTime user;
                    if (GetProcessTimes(Handle, out created, out exited, out kernel, out user)) {
                        var exitTime = exited.ToInt64();
                        if (exitTime > 0) {
                            latestChildCreationTime = Math.Min(snapshotTime, exitTime);
                            return true;
                        }
                    }
                } else if (result == WaitFailed) {
                    TraceProcessFailure("wait for", ProcessId, Marshal.GetLastWin32Error());
                }

                latestChildCreationTime = 0;
                return false;
            }

            public void TryTerminate() {
                if (HasExited) {
                    return;
                }

                if (!TerminateProcess(Handle, 1)) {
                    var error = Marshal.GetLastWin32Error();
                    if (error != ErrorAccessDenied && !HasExited) {
                        TraceProcessFailure("terminate", ProcessId, error);
                    }
                }
            }

            public void Dispose() {
                Handle.Dispose();
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeFileTime {
            public uint LowDateTime;
            public uint HighDateTime;

            public long ToInt64() {
                return ((long)HighDateTime << 32) | LowDateTime;
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ProcessEntry32 {
            public uint Size;
            public uint Usage;
            public uint ProcessId;
            public IntPtr DefaultHeapId;
            public uint ModuleId;
            public uint Threads;
            public uint ParentProcessId;
            public int BasePriority;
            public uint Flags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string ExecutableFile;
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
        private static extern bool GetProcessTimes(
            SafeKernelHandle process,
            out NativeFileTime creationTime,
            out NativeFileTime exitTime,
            out NativeFileTime kernelTime,
            out NativeFileTime userTime
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TerminateProcess(SafeKernelHandle process, uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(SafeKernelHandle handle, uint milliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeKernelHandle CreateToolhelp32Snapshot(uint flags, uint processId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32First(SafeKernelHandle snapshot, ref ProcessEntry32 entry);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32Next(SafeKernelHandle snapshot, ref ProcessEntry32 entry);

        [DllImport("kernel32.dll")]
        private static extern void GetSystemTimeAsFileTime(out NativeFileTime systemTime);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern SafeKernelHandle CreateJobObject(IntPtr jobAttributes, string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AssignProcessToJobObject(SafeKernelHandle job, SafeKernelHandle process);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TerminateJobObject(SafeKernelHandle job, uint exitCode);
    }
}
