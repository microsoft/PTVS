// Shared cache validation helper for previously discovered PyThreadState pointer.
// Performs lightweight plausibility checks before reuse to reduce rediscovery cost.
using System;
using System.Diagnostics;

namespace Microsoft.PythonTools.Debugging.Shared.SafeAttach {
    internal static class ThreadStateCacheValidator {
        /// <summary>
        /// Validates a cached PyThreadState base by probing the remote support struct and script path buffer.
        /// Returns true if probes succeed; false forces rediscovery.
        /// read delegate should read 'size' bytes from address; returns false on failure.
        /// </summary>
        internal static bool Validate(ulong tstateBase, ParsedDebugOffsets parsed, int pointerSize, Func<ulong, byte[], bool> read) {
            try {
                if (tstateBase == 0) return false;
                // Offsets sanity
                if (parsed.RemoteSupport == 0 || parsed.PendingCall == 0 || parsed.ScriptPath == 0 || parsed.ScriptPathSize == 0) return false;
                if (parsed.ScriptPathSize > 1_000_000UL) return false;
                // Probe remote support pointer (indirect) or struct region.
                ulong remoteSupportAddr = tstateBase + parsed.RemoteSupport;
                byte[] rsProbe = new byte[Math.Max(pointerSize, 8)];
                if (!read(remoteSupportAddr, rsProbe)) return false;
                // Optionally read script path first byte via remote support + ScriptPath offset if remote support looks like pointer sized region.
                ulong supportPtrCandidate = pointerSize == 8 ? BitConverter.ToUInt64(rsProbe, 0) : BitConverter.ToUInt32(rsProbe, 0);
                if (supportPtrCandidate != 0 && supportPtrCandidate < 0x100000000000UL) {
                    ulong scriptBufAddr = supportPtrCandidate + parsed.ScriptPath;
                    byte[] first = new byte[1];
                    if (!read(scriptBufAddr, first)) return false;
                }
                return true;
            } catch (Exception ex) {
                Debug.WriteLine("[PTVS][ThreadStateCacheValidator] Validate exception: " + ex.Message);
                return false;
            }
        }
    }
}
