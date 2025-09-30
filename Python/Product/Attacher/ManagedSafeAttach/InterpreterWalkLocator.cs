using System;
using System.Diagnostics;
using Microsoft.PythonTools.Debugging.Shared; // for ParsedDebugOffsets
using Microsoft.PythonTools.Debugging.Shared.SafeAttach;

namespace Microsoft.PythonTools.Debugger.ManagedSafeAttach {
    internal static class InterpreterWalkLocator {
        /// <summary>
        /// Infer interpreter / thread-walk offsets when the runtime slab did not populate them.
        /// 
        /// WHAT:
        ///   Treat the _Py_DebugOffsets slab as a sequence of qwords and slide a 6-qword window; interpret each window as:
        ///     [runtime_state, interpreters.head, interpreters.main, threads_head, threads_main, thread_next].
        ///   Filter quickly using size/alignment predicates, then validate by performing a minimal live walk that must reach a
        ///   structurally valid PyThreadState (validated via <see cref="ThreadStateValidator"/>).
        /// 
        /// WHY:
        ///   Some builds may omit or alter interpreter-walk metadata. Deterministic inference avoids fragile symbol / export
        ///   dependencies and replaces broad heuristics with a bounded, data-driven scan + validation.
        /// 
        /// HOW (success path):
        ///   1. Candidate offsets pass simple bounds ("Small" & alignment) checks.
        ///   2. Read an interpreter pointer (prefer interpreters.main else interpreters.head).
        ///   3. Try threads_main; if missing, iterate threads_head -> next chain (bounded) validating each PyThreadState.
        ///   4. On first validated tstate: store offsets into <paramref name="off"/> and return true (early exit).
        /// 
        /// SAFETY:
        ///   - Any failed read or pointer outside user range rejects the candidate.
        ///   - Bounded list traversal (limit 256) prevents pathological loops.
        ///   - No writes performed during inference.
        /// 
        /// LIMITATIONS / ASSUMPTIONS:
        ///   - Offsets are assumed small (< 0x20000) relative to the slab base.
        ///   - Only one valid sextuple is expected; first win short?circuits.
        ///   - Does not attempt disambiguation if multiple plausible patterns appear.
        /// 
        /// RETURNS:
        ///   True if offsets inferred (updates <paramref name="off"/>), otherwise false with reason and candidate count.
        /// </summary>
        public static bool TryInferOffsets(ISafeAttachProcess proc, ulong pyRuntimeAddr, byte[] slab, int ptrSize, ref ParsedDebugOffsets off, out string whyNot, out int candidateCount) {
            whyNot = "no candidate validated"; candidateCount = 0;
            int qwords = slab.Length / 8;
            var offLocal = off;
            bool ReadPtr(ulong addr, out ulong val) { var tmp = new byte[ptrSize]; if (!proc.Read(addr, tmp, tmp.Length)) { val = 0; return false; } val = ptrSize == 8 ? BitConverter.ToUInt64(tmp, 0) : BitConverter.ToUInt32(tmp, 0); return true; }
            bool IsUserPtr(ulong p) => p >= 0x10000 && p < 0x00008000_00000000UL;
            bool Small(ulong v) => v < 0x20000;
            bool ValidateTstate(ulong ts) => ThreadStateValidator.Validate(ts, offLocal, proc, verbose:false);
            bool ValidateSet(ulong rt, ulong ih, ulong im, ulong th, ulong tm, ulong tn) {
                ulong rtBase = pyRuntimeAddr + rt;
                ulong interp = 0;
                if (im != 0 && ReadPtr(rtBase + im, out interp) && IsUserPtr(interp)) { }
                else if (ih != 0 && ReadPtr(rtBase + ih, out interp) && IsUserPtr(interp)) { }
                else return false;
                if (tm != 0 && ReadPtr(interp + tm, out var tmain) && IsUserPtr(tmain) && ValidateTstate(tmain)) return true;
                if (th != 0 && tn != 0 && ReadPtr(interp + th, out var head) && IsUserPtr(head)) {
                    int limit = 256; ulong cur = head; while (cur != 0 && IsUserPtr(cur) && limit-- > 0) { if (ValidateTstate(cur)) return true; if (!ReadPtr(cur + tn, out cur)) break; }
                }
                return false;
            }
            for (int i = 0; i + 5 < qwords; i++) {
                ulong rt = BitConverter.ToUInt64(slab, (i + 0) * 8);
                ulong ih = BitConverter.ToUInt64(slab, (i + 1) * 8);
                ulong im = BitConverter.ToUInt64(slab, (i + 2) * 8);
                ulong th = BitConverter.ToUInt64(slab, (i + 3) * 8);
                ulong tm = BitConverter.ToUInt64(slab, (i + 4) * 8);
                ulong tn = BitConverter.ToUInt64(slab, (i + 5) * 8);
                candidateCount++;
                if (!((rt == 0 || Small(rt)) && Small(ih) && Small(im) && Small(th | tm) && Small(tn))) continue;
                if (tn == 0 || (tn % (ulong)ptrSize) != 0) continue;
                if (ValidateSet(rt, ih, im, th, tm, tn)) { offLocal.RuntimeState = rt; offLocal.InterpretersHead = ih; offLocal.InterpretersMain = im; offLocal.ThreadsHead = th; offLocal.ThreadsMain = tm; offLocal.ThreadNext = tn; off = offLocal; return true; }
            }
            return false;
        }

        /// <summary>
        /// Locate a valid PyThreadState using (parsed or inferred) walk offsets.
        /// 
        /// WHAT:
        ///   Uses runtime_state + interpreter offsets to obtain a PyInterpreterState, then returns a validated threads_main
        ///   or walks the tstate linked list (threads_head -> next) until a candidate passes ThreadStateValidator.
        /// 
        /// WHY:
        ///   Provides a deterministic, low-cost alternative to heuristic scans or export/TLS lookups, minimizing false positives
        ///   and avoiding unnecessary memory writes.
        /// 
        /// HOW:
        ///   1. Prefer interpreters.main; fallback to interpreters.head.
        ///   2. Attempt direct threads_main validation.
        ///   3. If not available, iterate list from threads_head using thread_next (bounded) and validate each.
        /// 
        /// RETURNS:
        ///   First validated PyThreadState address, or 0 if none found.
        /// </summary>
        public static ulong FindThreadState(ISafeAttachProcess proc, ulong pyRuntimeAddr, ParsedDebugOffsets off, int ptrSize, Func<ulong, bool> validate) {
            bool ReadPtr(ulong addr, out ulong val) { var tmp = new byte[ptrSize]; if (!proc.Read(addr, tmp, tmp.Length)) { val = 0; return false; } val = ptrSize == 8 ? BitConverter.ToUInt64(tmp, 0) : BitConverter.ToUInt32(tmp, 0); return true; }
            bool IsUserPtr(ulong p) => p >= 0x10000 && p < 0x00008000_00000000UL;
            ulong rtBase = pyRuntimeAddr + off.RuntimeState;
            if (off.InterpretersMain != 0 && ReadPtr(rtBase + off.InterpretersMain, out var interp) && IsUserPtr(interp)) {
                if (off.ThreadsMain != 0 && ReadPtr(interp + off.ThreadsMain, out var tm) && IsUserPtr(tm) && validate(tm)) return tm;
                if (off.ThreadsHead != 0 && off.ThreadNext != 0 && ReadPtr(interp + off.ThreadsHead, out var head) && IsUserPtr(head)) {
                    for (ulong ts = head; ts != 0 && IsUserPtr(ts); ) { if (validate(ts)) return ts; if (!ReadPtr(ts + off.ThreadNext, out ts)) break; }
                }
            }
            if (off.InterpretersHead != 0 && ReadPtr(rtBase + off.InterpretersHead, out var headInterp) && IsUserPtr(headInterp)) {
                if (off.ThreadsMain != 0 && ReadPtr(headInterp + off.ThreadsMain, out var tm2) && IsUserPtr(tm2) && validate(tm2)) return tm2;
                if (off.ThreadsHead != 0 && off.ThreadNext != 0 && ReadPtr(headInterp + off.ThreadsHead, out var headTs) && IsUserPtr(headTs)) {
                    for (ulong ts = headTs; ts != 0 && IsUserPtr(ts); ) { if (validate(ts)) return ts; if (!ReadPtr(ts + off.ThreadNext, out ts)) break; }
                }
            }
            return 0;
        }
    }
}
