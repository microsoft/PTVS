// Shared helper for determining or selecting the eval breaker stop bit mask.
// Unifies logic between managed SafeAttachOrchestrator and Concord RemoteAttach314 paths.
using System;
using System.Diagnostics;

namespace Microsoft.PythonTools.Debugging.Shared.SafeAttach {
    internal enum StopBitMaskSource { DynamicExistingBit, HeuristicCandidate, Default }

    internal struct StopBitSelection {
        public uint Mask;            // mask chosen
        public StopBitMaskSource Source; // how it was derived
        public bool AlreadySet;      // whether mask already present in current breaker value
        public bool ReadFailed;      // true if initial read failed
    }

    internal static class EvalBreakerHelper {
        /// <summary>
        /// Determines an appropriate stop-bit mask.
        /// Strategy:
        /// 1. Try to read current eval breaker value. If non-zero pick lowest set bit (dynamic source).
        /// 2. Else iterate candidate masks returning first mask whose bit is clear (heuristic source).
        /// 3. Fallback to default mask.
        /// Returns selection metadata for diagnostics / telemetry.
        /// </summary>
        internal static StopBitSelection DetermineMask(Func<bool> readBreaker, Func<ulong> getValue, uint[] candidateMasks, uint defaultMask) {
            var sel = new StopBitSelection { Mask = defaultMask, Source = StopBitMaskSource.Default };
            try {
                ulong value = 0;
                bool readOk = false;
                if (readBreaker != null) {
                    try { readOk = readBreaker(); if (readOk) value = getValue(); } catch { readOk = false; }
                }
                if (readOk) {
                    if (value != 0) {
                        // lowest set bit
                        ulong lsb = value & (~value + 1UL); // value & -value (two's complement)
                        if (lsb != 0 && lsb <= 0x80000000UL) {
                            sel.Mask = (uint)lsb;
                            sel.Source = StopBitMaskSource.DynamicExistingBit;
                            sel.AlreadySet = true; // by definition existing bit is set
                            return sel;
                        }
                    }
                    sel.ReadFailed = false;
                } else {
                    sel.ReadFailed = true;
                }
                // Heuristic selection: choose first candidate whose bit is not set (or first if read failed)
                if (candidateMasks != null && candidateMasks.Length > 0) {
                    foreach (var c in candidateMasks) {
                        if (!readOk || (value & c) == 0) {
                            sel.Mask = c;
                            sel.Source = StopBitMaskSource.HeuristicCandidate;
                            sel.AlreadySet = readOk && (value & c) != 0; // should be false when selected via condition
                            return sel;
                        }
                    }
                    // All candidates already set (rare) – fallback to first
                    sel.Mask = candidateMasks[0];
                    sel.Source = StopBitMaskSource.HeuristicCandidate;
                    sel.AlreadySet = readOk && (value & candidateMasks[0]) != 0;
                    return sel;
                }
                // Fallback default (already assigned)
                return sel;
            } catch (Exception ex) {
                Debug.WriteLine("[PTVS][EvalBreakerHelper] DetermineMask exception: " + ex.Message);
                return sel; // default
            }
        }
    }
}
