// Python Tools for Visual Studio
// Telemetry data object for safe attach operations (Python 3.14+)
using Microsoft.PythonTools.Logging;

namespace Microsoft.PythonTools.Logging {
    internal sealed class SafeAttachInfo : PythonToolsLoggerData {
        public bool Success { get; set; }
        public string Reason { get; set; } // offsets_missing | size_insufficient | write_fail | breaker_update_fail | disabled | not_applicable
        public string Version { get; set; } // CPython version hex (e.g. 0x030E0000) if known
        public bool TruncatedPath { get; set; }
    }
}
