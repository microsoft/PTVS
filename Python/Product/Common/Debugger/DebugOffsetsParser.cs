// Shared parser for CPython 3.14+ _Py_DebugOffsets (PEP 768)
// Minimal, Dkm-agnostic, used by both Concord (native) and managed-only attacher safe attach paths.
// Copyright (c) Microsoft
using System;
using System.Text;

namespace Microsoft.PythonTools.Debugging.Shared {
    /// <summary>
    /// Parsed result for _Py_DebugOffsets.
    /// Raw values (offsets / addresses) are intentionally left uninterpreted; caller can adjust based on context.
    /// </summary>
    public struct ParsedDebugOffsets {
        public uint Version;             // full version field
        public byte Flags;               // PEP-defined flags byte
        public ulong EvalBreaker;        // Absolute or relative (caller may normalize)
        public ulong RemoteSupport;      // Offset inside PyThreadState
        public ulong PendingCall;        // Offset inside remote_support struct
        public ulong ScriptPath;         // Offset inside remote_support struct
        public ulong ScriptPathSize;     // Size (bytes) of script path buffer
        public bool FreeThreaded;        // Flag interpreted
        public bool RemoteDebugDisabled; // Flag interpreted
    }

    /// <summary>
    /// Shared parser for the structured layout of _Py_DebugOffsets introduced by PEP 768.
    /// </summary>
    public static class DebugOffsetsParser {
        public const string Cookie = "xdebugpy";               // ASCII cookie at start
        public const uint MinSupportedVersion = 0x030E0000;     // 3.14.0 (mask major/minor)
        private const byte FLAG_FREE_THREADED = 0x01;           // Tentative mapping
        private const byte FLAG_REMOTE_DEBUG_DISABLED = 0x02;   // Tentative mapping

        /// <summary>
        /// Attempts to parse a memory buffer as _Py_DebugOffsets.
        /// </summary>
        /// <param name="data">Raw bytes starting at symbol address. Should supply at least header + 5 pointers (>=64 bytes recommended).</param>
        /// <param name="baseAddress">Base address of the CPython module (used to optionally normalize eval_breaker).</param>
        /// <param name="pointerSize">Process pointer size (4 or 8).</param>
        public static bool TryParse(byte[] data, ulong baseAddress, int pointerSize, out ParsedDebugOffsets result, out string failure) {
            result = default;
            failure = string.Empty;
            if (data == null || data.Length < 32) { failure = "buffer too small"; return false; }
            if (pointerSize != 4 && pointerSize != 8) { failure = "invalid pointer size"; return false; }
            if (!HasCookie(data)) { failure = "cookie mismatch"; return false; }
            if (data.Length < 12) { failure = "missing version"; return false; }
            uint version = BitConverter.ToUInt32(data, 8);
            uint masked = version & 0xFFFF0000; // major/minor
            if (masked < MinSupportedVersion) { failure = $"unsupported version 0x{version:X8}"; return false; }
            int flagsOffset = 12;
            byte flags = data.Length > flagsOffset ? data[flagsOffset] : (byte)0;
            int cursor = flagsOffset + 1;
            while ((cursor % pointerSize) != 0 && cursor < data.Length) cursor++;
            if (cursor + pointerSize > data.Length) { failure = "missing sizeof_struct"; return false; }
            ulong sizeofStruct = ReadPtr(data, cursor, pointerSize);
            cursor += pointerSize;
            ulong minExpected = (ulong)(Cookie.Length + 4 + 1 + (pointerSize - ((Cookie.Length + 5) % pointerSize)) + pointerSize + (5 * pointerSize));
            if (sizeofStruct != 0 && sizeofStruct < minExpected) { failure = "sizeof_struct too small"; return false; }
            if (cursor + 5 * pointerSize > data.Length) { failure = "insufficient data for pointer block"; return false; }
            ulong evalBreakerRaw = ReadPtr(data, cursor, pointerSize); cursor += pointerSize;
            ulong remoteSupport = ReadPtr(data, cursor, pointerSize); cursor += pointerSize;
            ulong pendingCall = ReadPtr(data, cursor, pointerSize); cursor += pointerSize;
            ulong scriptPath = ReadPtr(data, cursor, pointerSize); cursor += pointerSize;
            ulong scriptPathSize = ReadPtr(data, cursor, pointerSize); cursor += pointerSize;
            if (remoteSupport == 0 || pendingCall == 0 || scriptPath == 0) { failure = "zero offset(s)"; return false; }
            if (scriptPathSize == 0 || scriptPathSize > 1_000_000UL) { failure = "invalid script_path_size"; return false; }
            ulong evalBreaker = evalBreakerRaw;
            if (evalBreakerRaw < baseAddress || evalBreakerRaw < 0x0100_0000UL) { evalBreaker = baseAddress + evalBreakerRaw; }
            result = new ParsedDebugOffsets {
                Version = version,
                Flags = flags,
                EvalBreaker = evalBreaker,
                RemoteSupport = remoteSupport,
                PendingCall = pendingCall,
                ScriptPath = scriptPath,
                ScriptPathSize = scriptPathSize,
                FreeThreaded = (flags & FLAG_FREE_THREADED) != 0,
                RemoteDebugDisabled = (flags & FLAG_REMOTE_DEBUG_DISABLED) != 0
            };
            return true;
        }

        private static bool HasCookie(byte[] data) {
            if (data.Length < Cookie.Length) return false;
            for (int i = 0; i < Cookie.Length; i++) { if (data[i] != (byte)Cookie[i]) return false; }
            return true;
        }

        private static ulong ReadPtr(byte[] data, int offset, int ps) => ps == 8 ? BitConverter.ToUInt64(data, offset) : BitConverter.ToUInt32(data, offset);
        /// <summary>
        /// Utility for hex dumping small buffers (diagnostics / optional logging).
        /// </summary>
        public static string Hex(byte[] data, int count = 64) {
            if (data == null) return string.Empty;
            int len = Math.Min(count, data.Length);
            var sb = new StringBuilder(len * 3);
            for (int i = 0; i < len; i++) { if (i > 0) sb.Append(' '); sb.Append(data[i].ToString("X2")); }
            return sb.ToString();
        }
    }
}
