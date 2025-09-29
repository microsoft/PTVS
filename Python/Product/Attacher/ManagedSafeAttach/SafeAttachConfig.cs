using System;
using System.Diagnostics;

namespace Microsoft.PythonTools.Debugger.ManagedSafeAttach {
    internal sealed class SafeAttachConfig {
        public bool AllowExport { get; }
        public bool AllowExportFallback { get; }
        public bool DisableCache { get; }
        public bool ForceHeuristic { get; }
        public bool ForceHeuristic2 { get; }
        public bool HeuristicDisabled { get; }
        public bool WriteEnabled { get; }
        public bool Verbose { get; }
        public string ForcedTStateHex { get; }

        private static bool IsTrue(string name) => string.Equals(Environment.GetEnvironmentVariable(name), "1", StringComparison.Ordinal);

        public SafeAttachConfig(bool defaultVerbose) {
            AllowExport = IsTrue("PTVS_SAFE_ATTACH_ALLOW_EXPORT");
            AllowExportFallback = IsTrue("PTVS_SAFE_ATTACH_ALLOW_EXPORT_FALLBACK");
            DisableCache = IsTrue("PTVS_SAFE_ATTACH_MANAGED_NO_CACHE");
            ForceHeuristic = IsTrue("PTVS_SAFE_ATTACH_FORCE_HEURISTIC");
            ForceHeuristic2 = IsTrue("PTVS_SAFE_ATTACH_FORCE_HEURISTIC2");
            HeuristicDisabled = IsTrue("PTVS_SAFE_ATTACH_MANAGED_HEURISTIC_DISABLE");
            // Writes are enabled unless explicit global disable variable set OR explicit 0
            WriteEnabled = IsTrue("PTVS_SAFE_ATTACH_MANAGED_WRITE") || !IsTrue("PTVS_SAFE_ATTACH_MANAGED_DISABLE");
            ForcedTStateHex = Environment.GetEnvironmentVariable("PTVS_SAFE_ATTACH_MANAGED_TEST_TSTATE");
            Verbose = defaultVerbose || IsTrue("PTVS_SAFE_ATTACH_VERBOSE");
        }
    }
}
