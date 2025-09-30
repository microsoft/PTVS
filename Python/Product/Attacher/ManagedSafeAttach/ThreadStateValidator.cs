using System;
using System.Diagnostics;
using Microsoft.PythonTools.Debugging.Shared; // for ParsedDebugOffsets
using Microsoft.PythonTools.Debugging.Shared.SafeAttach;

namespace Microsoft.PythonTools.Debugger.ManagedSafeAttach {
    internal static class ThreadStateValidator {
        public static bool Validate(ulong tstatePtr, ParsedDebugOffsets parsed, ISafeAttachProcess proc, bool verbose) {
            if (tstatePtr == 0) return false;
            if ((tstatePtr & (ulong)(IntPtr.Size - 1)) != 0) { if (verbose) Debug.WriteLine("[PTVS][ManagedSafeAttach] Reject candidate (align)"); return false; }
#if X64 || AMD64
            if (tstatePtr >= 0x0000800000000000UL) { if (verbose) Debug.WriteLine("[PTVS][ManagedSafeAttach] Reject candidate (canonical)"); return false; }
#endif
            // eval_breaker 4 bytes
            var br = new byte[4];
            if (!proc.Read(tstatePtr + parsed.EvalBreaker, br, br.Length)) { if (verbose) Debug.WriteLine("[PTVS][ManagedSafeAttach] Reject candidate (eval_breaker read fail)"); return false; }
            // pending flag
            ulong supportBase = tstatePtr + parsed.RemoteSupport;
            var pend = new byte[4];
            if (!proc.Read(supportBase + parsed.PendingCall, pend, pend.Length)) { if (verbose) Debug.WriteLine("[PTVS][ManagedSafeAttach] Reject candidate (pending read fail)"); return false; }
            // script buffer prefix
            var buf = new byte[16];
            if (!proc.Read(supportBase + parsed.ScriptPath, buf, buf.Length)) { if (verbose) Debug.WriteLine("[PTVS][ManagedSafeAttach] Reject candidate (script buffer read fail)"); return false; }
            return true;
        }
    }
}
