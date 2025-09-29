// Shared heuristic thread-state locator used primarily by Concord; optional for managed path (diagnostic opt-in)
// Strategy: Given eval_breaker absolute address and offsets to remote support + script path in the PyThreadState,
// scan backwards from eval_breaker page-aligned region to locate a plausible PyThreadState base.
using System;
using System.Diagnostics;

namespace Microsoft.PythonTools.Debugging.Shared.SafeAttach {
    internal static class HeuristicThreadStateLocator {
        /// <summary>
        /// Attempts to locate a PyThreadState base heuristically.
        /// Parameters:
        ///  evalBreakerAddr: absolute address of eval_breaker (must be non-zero)
        ///  remoteSupportOffset: offset inside PyThreadState of remote support struct
        ///  scriptPathOffset: offset inside remote support struct for script path buffer
        ///  scriptPathSize: size (bytes) of script buffer (bounds plausibility)
        ///  pointerSize: 4 or 8
        ///  read: delegate to read bytes; should return false on failure (no throwing)
        /// Returns 0 if not found.
        /// </summary>
        internal static ulong TryLocate(ulong evalBreakerAddr, ulong remoteSupportOffset, ulong scriptPathOffset, ulong scriptPathSize, int pointerSize, Func<ulong, byte[], bool> read) {
            try {
                if (evalBreakerAddr == 0 || remoteSupportOffset == 0 || scriptPathOffset == 0) return 0;
                if (scriptPathSize == 0 || scriptPathSize > 1_000_000UL) return 0; // sanity
                const int PAGE = 0x1000;
                // Start scanning up to 32 pages backwards from evalBreaker page.
                ulong pageBase = evalBreakerAddr & ~(ulong)(PAGE - 1);
                ulong start = pageBase > (ulong)(PAGE * 32) ? pageBase - (ulong)(PAGE * 32) : 0;
                ulong end = pageBase + (ulong)PAGE; // inclusive upper bound (add one page slack)
                byte[] tmpPtr = new byte[pointerSize];
                byte[] one = new byte[1];
                for (ulong cand = pageBase; cand >= start; cand -= 0x10) {
                    // Compute address of remote support pointer/struct inside candidate
                    ulong remoteSupportAddr = cand + remoteSupportOffset;
                    if (!read(remoteSupportAddr, tmpPtr)) { if (cand < 0x10000) break; continue; }
                    ulong supportPtr = pointerSize == 8 ? BitConverter.ToUInt64(tmpPtr, 0) : BitConverter.ToUInt32(tmpPtr, 0);
                    if (supportPtr == 0 || supportPtr > evalBreakerAddr + 0x1000000) { if (cand < 0x10000) break; continue; }
                    // Probe first byte of script path buffer (supportPtr + scriptPathOffset)
                    ulong scriptBufAddr = supportPtr + scriptPathOffset;
                    if (!read(scriptBufAddr, one)) { if (cand < 0x10000) break; continue; }
                    // Heuristic acceptance: script path first byte either zero (unused) or printable ASCII
                    byte b = one[0];
                    if (b == 0 || (b >= 0x20 && b < 0x7F)) {
                        Debug.WriteLine($"[PTVS][HeuristicThreadStateLocator] Candidate tstate=0x{cand:X} support=0x{supportPtr:X} firstChar=0x{b:X2}");
                        return cand;
                    }
                    if (cand < 0x10000) break; // avoid underflow for ulong loop
                }
            } catch (Exception ex) {
                Debug.WriteLine("[PTVS][HeuristicThreadStateLocator] Exception: " + ex.Message);
            }
            return 0;
        }
    }
}
