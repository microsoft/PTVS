// Shared safe attach abstractions (Phase 2A skeleton)
// Provides common enums / DTOs so managed-only + Concord paths can emit uniform telemetry and share logic.
using System;

namespace Microsoft.PythonTools.Debugging.Shared.SafeAttach {
    /// <summary>
    /// Unified reasons safe attach may fail (superset for managed + native paths).
    /// </summary>
    public enum SafeAttachFailureSite {
        None = 0,
        FeatureDisabled,
        OpenProcess,
        ModuleEnumeration,
        NotPythonProcess,
        VersionGate,              // version < 3.14
        OffsetsAddressResolution, // symbol / section / export resolution failed
        OffsetsRead,              // could not read memory for offsets
        OffsetsParse,             // parse (cookie / structure) failed
        PolicyDisabled,           // RemoteDebugDisabled flag set
        ThreadStateDiscovery,     // failed to locate PyThreadState
        ScriptBufferWrite,        // failed writing script path
        PendingFlagWrite,         // failed writing pending flag
        EvalBreakerWrite,         // failed setting stop bit
        TimeoutAwaitConnect,      // target did not initiate connection
        Unknown                   // unspecified or unexpected
    }

    /// <summary>
    /// Central constants for safe attach telemetry / tests.
    /// </summary>
    internal static class SafeAttachConstants {
        // PEP / proposal typical buffer size (current finalized expectation)
        public const uint TypicalScriptPathSize = 512; // Used by tests to build truncation scenarios
    }

    /// <summary>
    /// Result of a safe attach attempt.
    /// </summary>
    public struct SafeAttachResult {
        public bool Success;              // true only if memory writes completed
        public SafeAttachFailureSite FailureSite;
        public int MajorVersion;
        public int MinorVersion;
        public uint RawVersion;
        public bool RemoteDebugDisabledFlag; // runtime policy flag
        public bool FreeThreadedFlag;
        public bool TruncatedScript;
        public bool ReusedThreadState;
        public bool PolicyDisabledEarly;  // true if failure = PolicyDisabled pre-writes
        public bool ExportBypassed;       // true if 3.14+ path skipped legacy export lookup
        public string Message;

        public static SafeAttachResult Ok(uint rawVersion, bool freeThreaded, bool disabled, bool reused, bool truncated, bool exportBypassed=false) {
            Decode(rawVersion, out int maj, out int min);
            return new SafeAttachResult {
                Success = true,
                FailureSite = SafeAttachFailureSite.None,
                RawVersion = rawVersion,
                MajorVersion = maj,
                MinorVersion = min,
                FreeThreadedFlag = freeThreaded,
                RemoteDebugDisabledFlag = disabled,
                ReusedThreadState = reused,
                TruncatedScript = truncated,
                PolicyDisabledEarly = false,
                ExportBypassed = exportBypassed
            };
        }

        public static SafeAttachResult Fail(SafeAttachFailureSite site, string msg = null, uint rawVersion=0, bool disabled=false, bool freeThreaded=false, bool exportBypassed=false) {
            Decode(rawVersion, out int maj, out int min);
            return new SafeAttachResult {
                Success = false,
                FailureSite = site,
                Message = msg,
                RawVersion = rawVersion,
                MajorVersion = maj,
                MinorVersion = min,
                RemoteDebugDisabledFlag = disabled,
                FreeThreadedFlag = freeThreaded,
                PolicyDisabledEarly = (site == SafeAttachFailureSite.PolicyDisabled),
                ExportBypassed = exportBypassed
            };
        }

        private static void Decode(uint raw, out int maj, out int min) {
            if (raw == 0) { maj = 0; min = 0; return; }
            maj = (int)((raw >> 24) & 0xFF);
            min = (int)((raw >> 16) & 0xFF);
        }
    }
}
