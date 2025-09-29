// Shared safe attach utilities (loader path buffer prep, stop-bit mask helpers)
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Microsoft.PythonTools.Debugging.Shared.SafeAttach {
    internal static class SafeAttachUtilities {
        /// <summary>
        /// Builds a UTF8 null-terminated buffer for the loader path, respecting scriptPathSize (max buffer bytes including null).
        /// Returns false if size is invalid. Truncates (and reports) if necessary.
        /// </summary>
        internal static bool TryPrepareLoaderBuffer(string loaderPath, ulong scriptPathSize, out byte[] buffer, out bool truncated) {
            buffer = Array.Empty<byte>(); truncated = false;
            if (string.IsNullOrEmpty(loaderPath)) return false;
            if (scriptPathSize == 0 || scriptPathSize > 1_000_000UL) return false; // sanity bounds
            try {
                var full = Path.GetFullPath(loaderPath);
                var raw = Encoding.UTF8.GetBytes(full);
                int maxPayload = (int)Math.Min(int.MaxValue, scriptPathSize);
                if (maxPayload < 2) return false; // need at least 1 char + null
                int copyLen = Math.Min(raw.Length, maxPayload - 1);
                if (copyLen < raw.Length) truncated = true;
                buffer = new byte[copyLen + 1];
                if (copyLen > 0) Buffer.BlockCopy(raw, 0, buffer, 0, copyLen);
                return true;
            } catch (Exception ex) {
                Debug.WriteLine("[PTVS][SafeAttachUtilities] Loader buffer prep failed: " + ex.Message);
                return false;
            }
        }
    }
}
