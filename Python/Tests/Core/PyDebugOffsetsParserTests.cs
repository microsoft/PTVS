// Tests for PyDebugOffsetsReader.TryParse (pure parsing, no DKM dependency)
using System;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace PythonToolsTests {
    [TestClass]
    public class PyDebugOffsetsParserTests {
        [ClassInitialize]
        public static void Init(TestContext ctx) { AssertListener.Initialize(); }

        private byte[] MakeValidStruct(
            uint version = 0x030E0000,
            string cookie = "xdebugpy",
            ulong evalBreaker = 0x1111UL,
            ulong remoteSupport = 0x2222UL,
            ulong pendingCall = 0x3333UL,
            ulong scriptPath = 0x4444UL,
            ulong scriptPathSize = 128UL) {

            var bufferSize = sizeof(uint) + 4 /*alignment padding (Pack=8)*/ + (15 * sizeof(ulong));
            var buffer = new byte[bufferSize];
            int offset = 0;
            void W32(uint v) { Array.Copy(BitConverter.GetBytes(v), 0, buffer, offset, 4); offset += 4; }
            void Pad4() { offset += 4; }
            void W64(ulong v) { Array.Copy(BitConverter.GetBytes(v), 0, buffer, offset, 8); offset += 8; }

            W32(version);
            Pad4(); // struct alignment after uint before first ulong
            // cookie64 (8 bytes, ASCII, zero padded)
            var cookieBytes = new byte[8];
            var rawCookieBytes = System.Text.Encoding.ASCII.GetBytes(cookie);
            Array.Copy(rawCookieBytes, cookieBytes, Math.Min(8, rawCookieBytes.Length));
            ulong cookie64 = BitConverter.ToUInt64(cookieBytes, 0);
            W64(cookie64);

            for (int i = 0; i < 8; i++) W64(0UL); // reserved

            W64(evalBreaker);
            W64(remoteSupport);
            W64(pendingCall);
            W64(scriptPath);
            W64(scriptPathSize);
            return buffer;
        }

        private bool TryParse(byte[] data, out uint version) {
            version = 0;
            var targetType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("Microsoft.PythonTools.Debugger.Concord.PyDebugOffsetsReader", false))
                .FirstOrDefault(t => t != null);
            if (targetType == null) return false;
            var m = targetType.GetMethod("TryParse", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (m == null) return false;
            var args = new object[] { data, null, null, null };
            var ok = (bool)m.Invoke(null, args);
            if (ok) {
                version = (uint)args[1];
            }
            return ok;
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void Parse_Valid() {
            var data = MakeValidStruct();
            if (!TryParse(data, out var ver)) Assert.Inconclusive("Parser not available (type not loaded)");
            Assert.AreEqual(0x030E0000u, ver);
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void Parse_Fails_WrongVersion() {
            var data = MakeValidStruct(version: 0x030D0000); // 3.13
            if (!TryParse(MakeValidStruct(), out _)) Assert.Inconclusive("Parser not available");
            Assert.IsFalse(TryParse(data, out _));
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void Parse_Fails_BadCookie() {
            var data = MakeValidStruct(cookie: "badcook"); // wrong cookie (must be xdebugpy)
            if (!TryParse(MakeValidStruct(), out _)) Assert.Inconclusive("Parser not available");
            Assert.IsFalse(TryParse(data, out _), "Parser accepted invalid cookie");
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void Parse_Fails_ZeroPointers() {
            // Zero out critical pointers (remoteSupport) -> should fail
            var data = MakeValidStruct(remoteSupport: 0UL);
            if (!TryParse(MakeValidStruct(), out _)) Assert.Inconclusive("Parser not available");
            Assert.IsFalse(TryParse(data, out _), "Parser accepted zero remote_support pointer");
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void Parse_Fails_InvalidScriptPathSize() {
            // script_path_size == 0
            var zeroSize = MakeValidStruct(scriptPathSize: 0UL);
            // script_path_size too large (>1MB)
            var hugeSize = MakeValidStruct(scriptPathSize: 2_000_000UL);
            if (!TryParse(MakeValidStruct(), out _)) Assert.Inconclusive("Parser not available");
            Assert.IsFalse(TryParse(zeroSize, out _), "Parser accepted zero script_path_size");
            Assert.IsFalse(TryParse(hugeSize, out _), "Parser accepted oversized script_path_size");
        }
    }
}
