// Shared parser for CPython 3.14+ _Py_DebugOffsets (PEP 768)
// Strict + extended scan: locate 5 consecutive qwords (eval_breaker, remote_support,
// pending_call, script_path, script_path_size) where size == 512.
// Extended (spec v2) may append interpreter walk offsets:
//   runtime_state, interpreters_head, interpreters_main, threads_head, threads_main, thread_next
// All are small ( < 0x20000 ) offsets within their respective owning structs.
using System;
using System.Diagnostics;
using System.Text;

namespace Microsoft.PythonTools.Debugging.Shared {
    public struct ParsedDebugOffsets {
        public uint Version;
        public byte Flags;
        public ulong EvalBreaker;
        public ulong RemoteSupport;
        public ulong PendingCall;
        public ulong ScriptPath;
        public ulong ScriptPathSize;
        public bool FreeThreaded;
        public bool RemoteDebugDisabled;
        // Optional interpreter walk offsets (zero if unavailable)
        public ulong RuntimeState;         // offset of _PyRuntimeState within PyRuntim section start (OR 0 if cookie already points at start)
        public ulong InterpretersHead;     // offset of interpreters.head field within _PyRuntimeState
        public ulong InterpretersMain;     // offset of interpreters.main field within _PyRuntimeState
        public ulong ThreadsHead;          // offset of tstate_head field within PyInterpreterState
        public ulong ThreadsMain;          // offset of threads_main field within PyInterpreterState (3.13+)
        public ulong ThreadNext;           // offset of next field within PyThreadState
        public bool HasInterpreterWalk => ThreadNext != 0 && (ThreadsHead != 0 || ThreadsMain != 0) && (InterpretersMain != 0 || InterpretersHead != 0);
    }

    public static class DebugOffsetsParser {
        public const string Cookie = "xdebugpy";
        public const uint MinSupportedVersion = 0x030E0000;
        private const byte FLAG_FREE_THREADED = 0x01;
        private const byte FLAG_REMOTE_DEBUG_DISABLED = 0x02;
        private const ulong EXPECTED_SCRIPT_PATH_SIZE = 512;

        public static bool TryParse(byte[] data, ulong baseAddress, int pointerSize, out ParsedDebugOffsets result, out string failure) {
            result = default; failure = string.Empty;
            if (data == null || data.Length < 64) { failure = "buffer too small"; return false; }
            if (pointerSize != 4 && pointerSize != 8) { failure = "invalid pointer size"; return false; }
            if (!HasCookie(data)) { failure = "cookie mismatch"; return false; }

            ulong verQ = ReadQword(data, 1);
            uint ver = (uint)verQ;
            if ((ver & 0xFFFF0000) < MinSupportedVersion) { failure = string.Format("unsupported version 0x{0:X8}", ver); return false; }
            ulong flagsQ = data.Length >= 24 ? ReadQword(data, 2) : 0UL;
            byte rawFlags = (byte)(flagsQ & 0xFF);
            bool dump = true;// EnvVarTrue("PTVS_SAFE_ATTACH_DUMP");
            if (dump) Debug.WriteLine(string.Format("[PTVS][OffsetsParser] ver=0x{0:X8} rawFlags=0x{1:X2}", ver, rawFlags));

            if (StrictPass(data, dump, out var strict, out _)) {
                result = BuildResult(ver, rawFlags, strict.eb, strict.rs, strict.pc, strict.sp, strict.sz);
                PopulateInterpreterOffsets(data, strict.index + 5, ref result, dump);
                return true;
            }
            if (ExtendedScan(data, dump, out var ext, out _)) {
                result = BuildResult(ver, rawFlags, ext.eb, ext.rs, ext.pc, ext.sp, ext.sz);
                PopulateInterpreterOffsets(data, ext.index + 5, ref result, dump);
                return true;
            }
            failure = "no valid layout";
            return false;
        }

        private struct Block { public int index; public ulong eb, rs, pc, sp, sz; }

        // Strict heuristic: scan entire slab (no artificial cap)
        private static bool StrictPass(byte[] data, bool dump, out Block block, out string reason) {
            block = default; reason = "not found";
            int qwords = data.Length / 8;
            for (int i = 2; i + 4 < qwords; i++) { // start after cookie+version
                if (i == 1) continue; // skip version qword
                ulong a = ReadQword(data, i);
                ulong b = ReadQword(data, i + 1);
                ulong c = ReadQword(data, i + 2);
                ulong d = ReadQword(data, i + 3);
                ulong e = ReadQword(data, i + 4);
                if (e != EXPECTED_SCRIPT_PATH_SIZE) continue;       // size must be 512
                if (!(a > 0 && a < 0x10000)) continue;              // eval_breaker offset
                if (!(b > 0 && b < 0x10000)) continue;              // remote_support offset
                if (!(c < 0x10000)) continue;                       // pending offset (may be 0/4)
                if (!(d > 0 && d < e)) continue;                    // script_path inside buffer
                if ((c % 4) != 0) continue;                         // int alignment
                if (!(d >= (c + 4))) continue;                      // script follows pending
                block = new Block { index = i, eb = a, rs = b, pc = c, sp = d, sz = e };
                if (dump) Debug.WriteLine($"[PTVS][OffsetsParser.Strict] blockIndex={i} byteOff=0x{(i * 8):X} eb=0x{a:X} rs=0x{b:X} pc=0x{c:X} sp=0x{d:X} size={e}");
                return true;
            }
            return false;
        }

        // Extended scan: still offsets-only; prefer canonical pending (0/4) and script_path == pending+4
        private static bool ExtendedScan(byte[] data, bool dump, out Block block, out string reason) {
            block = default; reason = "not found";
            int qwords = data.Length / 8; Block best = default; int bestScore = -1;
            for (int i = 2; i + 4 < qwords; i++) {
                if (i == 1) continue;
                ulong a = ReadQword(data, i);
                ulong b = ReadQword(data, i + 1);
                ulong c = ReadQword(data, i + 2);
                ulong d = ReadQword(data, i + 3);
                ulong e = ReadQword(data, i + 4);
                if (e != EXPECTED_SCRIPT_PATH_SIZE) continue;
                if (!(a > 0 && a < 0x10000)) continue;              // enforce small eval_breaker offset
                if (!(b > 0 && b < 0x10000)) continue;              // remote_support offset
                if (!(c < 0x10000)) continue;                       // pending offset
                if (!(d > 0 && d < e)) continue;                    // script_path inside buffer
                if ((c % 4) != 0) continue;
                if (!(d >= (c + 4))) continue;
                int score = 0;
                if (c == 0 || c == 4) score += 30;                  // canonical pending
                if (d == c + 4) score += 30;                        // canonical script_path
                score += (int)(0x10000 - Math.Min(a, 0x10000UL)) / 256;
                score += (int)(0x10000 - Math.Min(b, 0x10000UL)) / 256;
                score += (int)(0x0200 - Math.Min(d, 0x200UL));
                if (score > bestScore) { bestScore = score; best = new Block { index = i, eb = a, rs = b, pc = c, sp = d, sz = e }; }
            }
            if (bestScore >= 0) {
                block = best; if (dump) Debug.WriteLine($"[PTVS][OffsetsParser.Extended] blockIndex={best.index} byteOff=0x{(best.index * 8):X} eb=0x{best.eb:X} rs=0x{best.rs:X} pc=0x{best.pc:X} sp=0x{best.sp:X} size={best.sz}");
                return true;
            }
            return false;
        }

        private static ParsedDebugOffsets BuildResult(uint ver, byte rawFlags, ulong eb, ulong rs, ulong pc, ulong sp, ulong sz) => new ParsedDebugOffsets {
            Version = ver,
            Flags = rawFlags,
            EvalBreaker = eb,
            RemoteSupport = rs,
            PendingCall = pc,
            ScriptPath = sp,
            ScriptPathSize = sz,
            FreeThreaded = (rawFlags & FLAG_FREE_THREADED) != 0,
            RemoteDebugDisabled = (rawFlags & FLAG_REMOTE_DEBUG_DISABLED) != 0
        };

        private static void PopulateInterpreterOffsets(byte[] data, int startQword, ref ParsedDebugOffsets r, bool dump) {
            // If at least 6 more qwords remain treat them as optional interpreter offsets if they look sane.
            int qwords = data.Length / 8;
            if (startQword + 6 > qwords) {
                if (dump) Debug.WriteLine($"[PTVS][OffsetsParser.Interpreter] Not enough qwords for walk metadata (need 6, have {qwords - startQword}) startIndex={startQword}");
                return;
            }
            ulong rt = ReadQword(data, startQword + 0);
            ulong ih = ReadQword(data, startQword + 1);
            ulong im = ReadQword(data, startQword + 2);
            ulong th = ReadQword(data, startQword + 3);
            ulong tm = ReadQword(data, startQword + 4);
            ulong tn = ReadQword(data, startQword + 5);
            // Sanity: all small offsets (except runtime which may be 0 meaning 'section base')
            bool small(ulong v) => v < 0x20000;
            bool condRt = (rt == 0 || small(rt));
            bool condIh = small(ih);
            bool condIm = small(im);
            bool condThTm = small(th | tm); // either may be zero
            bool condTn = small(tn) && tn != 0;
            bool accept = condRt && condIh && condIm && condThTm && condTn;
            if (accept) {
                r.RuntimeState = rt; r.InterpretersHead = ih; r.InterpretersMain = im; r.ThreadsHead = th; r.ThreadsMain = tm; r.ThreadNext = tn;
                if (dump) Debug.WriteLine($"[PTVS][OffsetsParser.Interpreter] rt=0x{rt:X} ih=0x{ih:X} im=0x{im:X} th=0x{th:X} tm=0x{tm:X} tn=0x{tn:X}");
            } else if (dump) {
                Debug.WriteLine($"[PTVS][OffsetsParser.Interpreter] Rejected candidate walk metadata rt=0x{rt:X} ih=0x{ih:X} im=0x{im:X} th=0x{th:X} tm=0x{tm:X} tn=0x{tn:X} condRt={condRt} condIh={condIh} condIm={condIm} condThTm={condThTm} condTn={condTn}");
            }
        }

        private static bool HasCookie(byte[] data) { if (data.Length < Cookie.Length) return false; for (int i = 0; i < Cookie.Length; i++) if (data[i] != (byte)Cookie[i]) return false; return true; }
        private static ulong ReadQword(byte[] data, int index) { int off = index * 8; return (off + 8 <= data.Length) ? BitConverter.ToUInt64(data, off) : 0UL; }
        private static bool EnvVarTrue(string name) => string.Equals(Environment.GetEnvironmentVariable(name), "1", StringComparison.Ordinal);
        public static string Hex(byte[] data, int count = 64) { if (data == null) return string.Empty; int len = Math.Min(count, data.Length); var sb = new StringBuilder(len * 3); for (int i = 0; i < len; i++) { if (i > 0) sb.Append(' '); sb.Append(data[i].ToString("X2")); } return sb.ToString(); }
    }
}
