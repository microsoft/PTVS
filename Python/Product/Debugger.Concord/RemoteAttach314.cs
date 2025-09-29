// Python Tools for Visual Studio
// Safe attach helper for CPython 3.14+ using _Py_DebugOffsets (PEP 768)
// Simplified & refactored for readability (logic preserved).

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.PythonTools.Debugging.Shared.SafeAttach;
using Microsoft.PythonTools.Debugger.Concord.Proxies.Structs;
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.PythonTools.Debugger.Concord {
    public enum SafeAttachResult { Success, InvalidOffsets, LoaderNotFound, BadAddress, AccessDenied, DisabledByPolicy, WriteFail }
    public enum AttachFailSite { None, CachedValidation, OpenProcess, WriteScript, WritePending, ReadBreaker, WriteBreaker, SupportPointerNull, Validation, Unknown }

    public class SafeAttachOutcome {
        public SafeAttachResult Result { get; set; }
        public bool TruncatedPath { get; set; }
        public ulong TargetThreadStateAddress { get; set; }
        public uint? TargetThreadId { get; set; }
        public bool MainInterpreterThreadSelection { get; set; }
        public bool HeuristicThreadSelection { get; set; }
        public bool ExportFallbackThreadSelection { get; set; }
        public bool AlreadySet { get; set; }
        public bool VerifiedScriptPathWrite { get; set; }
        public bool VerifiedPendingCall { get; set; }
        public bool BreakerBitSet { get; set; }
        public AttachFailSite FailSite { get; set; }
    }

    public static class RemoteAttach314 {
        private const uint DEFAULT_EVAL_PLEASE_STOP_BIT = 0x1;
        private static readonly bool SimulatePartialWrite = Environment.GetEnvironmentVariable("PTVS_SIMULATE_PARTIAL_WRITE") == "1";
        private static int _partialWriteToggle = 0;

        #region Public Entry
        public static SafeAttachOutcome TryPerform(DkmProcess process, string loaderPath) {
            var outcome = new SafeAttachOutcome { Result = SafeAttachResult.WriteFail, FailSite = AttachFailSite.Unknown };
            try {
                var pyrt = process.GetPythonRuntimeInfo();
                // Legacy dynamic mask derivation removed; unified EvalBreakerHelper will derive at write time.

                // Cached reattach short-circuit
                if (pyrt.CachedAttachThreadState != 0 && pyrt.HasValidDebugOffsets && File.Exists(loaderPath)) {
                    if (RevalidateCachedThreadState(process, pyrt, loaderPath, out bool alreadySet, out ulong cached)) {
                        if (alreadySet) {
                            outcome.Result = SafeAttachResult.Success; outcome.TargetThreadStateAddress = cached; outcome.AlreadySet = true; outcome.FailSite = AttachFailSite.None; return outcome;
                        }
                    } else { outcome.FailSite = AttachFailSite.CachedValidation; }
                }

                if (!ValidateAndPrepare(pyrt, loaderPath, ref outcome)) { if (Verbose) Debug.WriteLine("[PTVS][SafeAttach314] Validation failed early"); return outcome; }

                var dbg = pyrt.DebugOffsets.DebuggerSupport;
                var selection = DetermineThreadState(process, pyrt, dbg.EvalBreaker, dbg.RemoteSupport, dbg.ScriptPath, dbg.ScriptPathSize);
                if (!selection.Success) { outcome.Result = SafeAttachResult.BadAddress; outcome.FailSite = AttachFailSite.Validation; return outcome; }
                outcome.TargetThreadStateAddress = selection.TState;
                outcome.MainInterpreterThreadSelection = selection.Main;
                outcome.ExportFallbackThreadSelection = selection.Export;
                outcome.HeuristicThreadSelection = selection.Heuristic;
                outcome.TargetThreadId = selection.ThreadId;

                ulong supportPtr = ReadPointer(process, selection.TState + dbg.RemoteSupport);
                if (supportPtr == 0) { outcome.Result = SafeAttachResult.BadAddress; outcome.FailSite = AttachFailSite.SupportPointerNull; return outcome; }

                if (!PrepareLoaderBuffer(loaderPath, dbg.ScriptPathSize, out var pathBuf, out bool truncated)) {
                    outcome.Result = SafeAttachResult.InvalidOffsets; outcome.FailSite = AttachFailSite.Validation; return outcome; }
                outcome.TruncatedPath = truncated;
                if (Verbose) Debug.WriteLine($"[PTVS][SafeAttach314] Loader buffer len={pathBuf.Length} truncated={truncated}");

                PerformWrites(process, pyrt, dbg, selection.TState, supportPtr, pathBuf, ref outcome);
                if (outcome.Result == SafeAttachResult.Success) { pyrt.CachedAttachThreadState = selection.TState; }
                return outcome;
            } catch (Exception ex) {
                Debug.WriteLine($"[PTVS][SafeAttach314] Exception: {ex.Message}");
                outcome.Result = SafeAttachResult.WriteFail; if (outcome.FailSite == AttachFailSite.Unknown) outcome.FailSite = AttachFailSite.Unknown; return outcome;
            }
        }
        #endregion

        #region Validation & Preparation
        private static bool ValidateAndPrepare(PythonRuntimeInfo pyrt, string loaderPath, ref SafeAttachOutcome outcome) {
            if (pyrt.DebugOffsets?.RemoteDebugDisabled == true) { outcome.Result = SafeAttachResult.DisabledByPolicy; outcome.FailSite = AttachFailSite.Validation; return false; }
            if (!pyrt.HasValidDebugOffsets) { outcome.Result = SafeAttachResult.InvalidOffsets; outcome.FailSite = AttachFailSite.Validation; return false; }
            if (!File.Exists(loaderPath)) { outcome.Result = SafeAttachResult.LoaderNotFound; outcome.FailSite = AttachFailSite.Validation; return false; }
            return true;
        }
        #endregion

        #region Thread State Selection
        private struct SelectionResult { public bool Success; public ulong TState; public bool Main; public bool Export; public bool Heuristic; public uint? ThreadId; }
        private static SelectionResult DetermineThreadState(DkmProcess process, PythonRuntimeInfo pyrt, ulong evalBreaker, ulong remoteSupportOff, ulong scriptPathOff, ulong scriptPathSize) {
            var res = new SelectionResult(); PyThreadState ts = null;
            try {
                var rt = pyrt.GetRuntimeState();
                var main = rt?.interpreters?.main.TryRead();
                if (main != null) {
                    ts = main.GetThreadStates(process).FirstOrDefault();
                    if (ts != null) { res.Main = true; res.TState = ts.Address; }
                }
            } catch { }
            if (res.TState == 0) { ts = PyThreadState.GetThreadStates(process).FirstOrDefault(); if (ts != null) { res.TState = ts.Address; } }
            if (res.TState == 0) {
                ulong exportTState = ResolveCurrentThreadStateViaExport(process);
                if (exportTState != 0) { res.TState = exportTState; res.Export = true; }
            }
            if (res.TState == 0) {
                ulong heur = HeuristicFindThreadStateBase(process, evalBreaker, remoteSupportOff, scriptPathOff, scriptPathSize);
                if (heur != 0) { res.TState = heur; res.Heuristic = true; }
            }
            res.Success = res.TState != 0;
            if (ts != null) { try { res.ThreadId = (uint)ts.thread_id.Read(); } catch { } }
            if (Verbose) Debug.WriteLine($"[PTVS][SafeAttach314] ThreadState selection main={res.Main} export={res.Export} heuristic={res.Heuristic} tstate=0x{res.TState:X}");
            return res;
        }
        #endregion

        #region Loader Path Buffer
        // Replaced by shared SafeAttachUtilities.TryPrepareLoaderBuffer (kept for binary compat if referenced elsewhere)
        public static bool PrepareLoaderBuffer(string loaderPath, ulong scriptPathSize, out byte[] buffer, out bool truncated) {
            return SafeAttachUtilities.TryPrepareLoaderBuffer(loaderPath, scriptPathSize, out buffer, out truncated);
        }
        #endregion

        #region Writes & Verification
        private static void PerformWrites(DkmProcess process, PythonRuntimeInfo pyrt, PyDebugOffsetsReader.DebuggerSupportOffsets dbg, ulong tstateBase, ulong supportPtr, byte[] pathBuf, ref SafeAttachOutcome outcome) {
            ulong scriptPathAddr = supportPtr + dbg.ScriptPath; ulong pendingCallAddr = supportPtr + dbg.PendingCall; ulong evalBreakerAddr = dbg.EvalBreaker;
            Debug.WriteLine($"[PTVS][SafeAttach314] Computed addresses: TState=0x{tstateBase:X} Support=0x{supportPtr:X} ScriptPath=0x{scriptPathAddr:X} PendingCall=0x{pendingCallAddr:X} EvalBreaker=0x{evalBreakerAddr:X}");
            var hProcess = Win32Interop.OpenProcess(Win32Interop.ProcessAccessFlags.VirtualMemoryWrite | Win32Interop.ProcessAccessFlags.VirtualMemoryOperation | Win32Interop.ProcessAccessFlags.QueryInformation | Win32Interop.ProcessAccessFlags.VirtualMemoryRead, false, (int)process.LivePart.Id);
            if (hProcess == IntPtr.Zero) { outcome.Result = SafeAttachResult.AccessDenied; outcome.FailSite = AttachFailSite.OpenProcess; return; }
            try {
                if (!WriteRemote(hProcess, scriptPathAddr, pathBuf)) { outcome.Result = SafeAttachResult.WriteFail; outcome.FailSite = AttachFailSite.WriteScript; return; }
                if (!WriteRemote(hProcess, pendingCallAddr, BitConverter.GetBytes(1))) { outcome.Result = SafeAttachResult.WriteFail; outcome.FailSite = AttachFailSite.WritePending; return; }
                if (!ReadRemoteUInt32(hProcess, evalBreakerAddr, out uint breakerValue)) { outcome.Result = SafeAttachResult.WriteFail; outcome.FailSite = AttachFailSite.ReadBreaker; return; }
                var sel = EvalBreakerHelper.DetermineMask(
                    readBreaker: () => true,
                    getValue: () => breakerValue,
                    candidateMasks: new uint[] { 0x1, 0x2, 0x4, 0x8 },
                    defaultMask: DEFAULT_EVAL_PLEASE_STOP_BIT);
                if (Verbose) Debug.WriteLine($"[PTVS][SafeAttach314] Stop-bit selection mask=0x{sel.Mask:X} source={sel.Source} alreadySet={sel.AlreadySet}");
                uint stopMask = sel.Mask;
                uint newBreaker = breakerValue | stopMask;
                if (newBreaker != breakerValue && !WriteRemote(hProcess, evalBreakerAddr, BitConverter.GetBytes(newBreaker))) { outcome.Result = SafeAttachResult.WriteFail; outcome.FailSite = AttachFailSite.WriteBreaker; return; }
                ConsolidateVerification(hProcess, scriptPathAddr, pathBuf.Length, pendingCallAddr, evalBreakerAddr, stopMask, ref outcome);
                outcome.Result = SafeAttachResult.Success; outcome.FailSite = AttachFailSite.None;
                if (Verbose) Debug.WriteLine($"[PTVS][SafeAttach314] SUCCESS tstate=0x{outcome.TargetThreadStateAddress:X} mask=0x{stopMask:X} src={sel.Source} truncated={outcome.TruncatedPath}");
                Debug.WriteLine($"[PTVS][SafeAttach314] Attach verification: script={outcome.VerifiedScriptPathWrite} pending={outcome.VerifiedPendingCall} breaker={outcome.BreakerBitSet} selection={(outcome.MainInterpreterThreadSelection ? "main" : outcome.ExportFallbackThreadSelection ? "export" : outcome.HeuristicThreadSelection ? "heuristic" : "enumerated")} alreadySet={outcome.AlreadySet} stopMask=0x{stopMask:X} maskSrc={sel.Source}");
            } finally { Win32Interop.CloseHandle(hProcess); }
        }

        private static void ConsolidateVerification(IntPtr hProcess, ulong scriptPathAddr, int pathLen, ulong pendingCallAddr, ulong evalBreakerAddr, uint stopMask, ref SafeAttachOutcome outcome) {
            int verify = Math.Min(8, Math.Max(0, pathLen - 1));
            if (verify > 0) { var buf = new byte[verify]; if (ReadRemoteBytes(hProcess, scriptPathAddr, buf)) outcome.VerifiedScriptPathWrite = true; }
            if (ReadRemoteUInt32(hProcess, pendingCallAddr, out uint pend) && pend == 1) outcome.VerifiedPendingCall = true;
            if (ReadRemoteUInt32(hProcess, evalBreakerAddr, out uint after) && (after & stopMask) != 0) outcome.BreakerBitSet = true;
        }
        #endregion

        #region Cached TState Validation
        private static bool RevalidateCachedThreadState(DkmProcess process, PythonRuntimeInfo pyrt, string loaderPath, out bool alreadySet, out ulong cachedTState) {
            alreadySet = false; cachedTState = pyrt.CachedAttachThreadState;
            try {
                var dbg = pyrt.DebugOffsets.DebuggerSupport;
                ulong supportPtr = ReadPointer(process, cachedTState + dbg.RemoteSupport); if (supportPtr == 0) return false;
                if (dbg.ScriptPathSize == 0 || dbg.ScriptPathSize > 1_000_000UL) return false;
                ulong scriptAddr = supportPtr + dbg.ScriptPath;
                int probe = (int)Math.Min(16UL, dbg.ScriptPathSize);
                var buf = new byte[probe]; process.ReadMemory(scriptAddr, DkmReadMemoryFlags.None, buf);
                var existing = Encoding.UTF8.GetString(buf).TrimEnd('\0'); var full = Path.GetFullPath(loaderPath);
                if (!string.IsNullOrEmpty(existing) && full.StartsWith(existing, StringComparison.OrdinalIgnoreCase) && string.Equals(existing, full, StringComparison.OrdinalIgnoreCase)) alreadySet = true;
                return true;
            } catch { return false; }
        }
        #endregion

        #region Native Helpers
        private static ulong ReadPointer(DkmProcess process, ulong address) { int ps = process.Is64Bit() ? 8 : 4; var buf = new byte[ps]; try { process.ReadMemory(address, DkmReadMemoryFlags.None, buf); return ps == 8 ? BitConverter.ToUInt64(buf, 0) : BitConverter.ToUInt32(buf, 0); } catch { return 0; } }
        private static ulong ResolveCurrentThreadStateViaExport(DkmProcess process) {
            try {
                var py = process.GetPythonRuntimeInfo()?.DLLs?.Python; if (py == null) return 0;
                ulong moduleBase = py.BaseAddress;
                int pointerSize = process.GetPointerSize();
                bool Read(ulong addr, byte[] buffer, int size) {
                    try { process.ReadMemory(addr, DkmReadMemoryFlags.None, buffer); return true; } catch { return false; }
                }
                return ThreadStateExportResolver.TryGetCurrentThreadState(Read, moduleBase, pointerSize);
            } catch { return 0; }
        }
        // Removed TryGetCurrentThreadStateFromExport (superseded by ResolveCurrentThreadStateViaExport + shared resolver)
        private static bool ReadRemoteBytes(IntPtr hProcess, ulong address, byte[] buffer) { IntPtr read; return Win32Interop.ReadProcessMemory(hProcess, ToIntPtr(address), buffer, (IntPtr)buffer.Length, out read) && read.ToInt32() == buffer.Length; }
        private static bool ReadRemoteUInt32(IntPtr hProcess, ulong address, out uint value) { value = 0; var buf = new byte[4]; IntPtr read; if (!Win32Interop.ReadProcessMemory(hProcess, ToIntPtr(address), buf, (IntPtr)4, out read) || read.ToInt32() != 4) return false; value = BitConverter.ToUInt32(buf, 0); return true; }
        private static ulong HeuristicFindThreadStateBase(DkmProcess process, ulong evalBreaker, ulong remoteSupportOff, ulong scriptPathOff, ulong scriptPathSize) { try { const int page = 0x1000; ulong start = (evalBreaker & ~(ulong)(page - 1)) - (ulong)(page * 32); ulong end = evalBreaker; if (start > evalBreaker) start = 0; var h = Win32Interop.OpenProcess(Win32Interop.ProcessAccessFlags.VirtualMemoryRead | Win32Interop.ProcessAccessFlags.QueryInformation, false, (int)process.LivePart.Id); if (h == IntPtr.Zero) return 0; try { for (ulong cand = end; cand >= start; cand -= 0x10) { if (!ReadPointerRaw(h, cand + remoteSupportOff, process.GetPointerSize(), out var rsp)) { if (cand < 0x10000) break; continue; } ulong sp = rsp.ToUInt64(); if (sp == 0 || sp > evalBreaker + 0x1000000) { if (cand < 0x10000) break; continue; } ulong scriptAddr = sp + scriptPathOff; var one = new byte[1]; IntPtr r; if (Win32Interop.ReadProcessMemory(h, ToIntPtr(scriptAddr), one, (IntPtr)1, out r) && r.ToInt32() == 1) return cand; if (cand < 0x10000) break; } } finally { Win32Interop.CloseHandle(h); } } catch (Exception ex) { Debug.WriteLine($"[PTVS][SafeAttach314] HeuristicFindThreadStateBase exception: {ex.Message}"); } return 0; }
        private static bool ReadPointerRaw(IntPtr h, ulong addr, int ps, out UIntPtr val) { val = UIntPtr.Zero; var buf = new byte[ps]; IntPtr read; if (!Win32Interop.ReadProcessMemory(h, ToIntPtr(addr), buf, (IntPtr)ps, out read) || read.ToInt32() != ps) return false; val = new UIntPtr(ps == 8 ? BitConverter.ToUInt64(buf, 0) : BitConverter.ToUInt32(buf, 0)); return true; }
        private static bool WriteRemote(IntPtr h, ulong addr, byte[] data) { if (addr == 0) return false; if (SimulatePartialWrite && data.Length > 8 && _partialWriteToggle == 0) { _partialWriteToggle = 1; var partial = new byte[data.Length / 2]; Buffer.BlockCopy(data, 0, partial, 0, partial.Length); int w; Win32Interop.WriteProcessMemory(h, ToIntPtr(addr), partial, (IntPtr)partial.Length, out w); return false; } int written; return Win32Interop.WriteProcessMemory(h, ToIntPtr(addr), data, (IntPtr)data.Length, out written) && written == data.Length; }
        private static IntPtr ToIntPtr(ulong a) { if (a > long.MaxValue) throw new OverflowException("Address too large for IntPtr"); return new IntPtr(unchecked((long)a)); }
        #endregion

        #region Verbose Logging
        private static bool Verbose => Environment.GetEnvironmentVariable("PTVS_SAFE_ATTACH_VERBOSE") == "1";
        #endregion
    }

    internal static class Win32Interop {
        [Flags] internal enum ProcessAccessFlags : uint { VirtualMemoryOperation = 0x0008, VirtualMemoryRead = 0x0010, VirtualMemoryWrite = 0x0020, QueryInformation = 0x0400, All = 0x001F0FFF }
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern IntPtr OpenProcess(ProcessAccessFlags desiredAccess, bool inheritHandle, int processId);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, IntPtr nSize, out int lpNumberOfBytesWritten);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, IntPtr nSize, out IntPtr lpNumberOfBytesRead);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool CloseHandle(IntPtr hObject);
    }
}
