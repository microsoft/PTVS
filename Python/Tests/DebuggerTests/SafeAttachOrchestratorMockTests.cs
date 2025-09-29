using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.PythonTools.Debugger.ManagedSafeAttach; // ensure assembly reference via Attacher project reference
using Microsoft.PythonTools.Debugging.Shared.SafeAttach;
using Microsoft.PythonTools.Debugging.Shared; // DebugOffsetsParser

namespace DebuggerTests {
    [TestClass]
    public class SafeAttachOrchestratorMockTests {
        private class MockProcess : ISafeAttachProcess {
            public int Pid { get; }
            public IntPtr Handle { get; } = (IntPtr)0x1234;
            private readonly List<(ulong baseAddr, byte[] data)> _blocks = new List<(ulong, byte[])>();
            private readonly List<(string name, IntPtr baseAddr, int size)> _modules = new List<(string, IntPtr, int)>();
            public bool EnumerateModules(Func<string, IntPtr, int, bool> onModule) { foreach (var m in _modules) { if (onModule(m.name, m.baseAddr, m.size)) return true; } return false; }
            public bool Read(ulong address, byte[] buffer, int size) {
                foreach (var (baseAddr, data) in _blocks) {
                    ulong end = baseAddr + (ulong)data.Length;
                    if (address >= baseAddr && (ulong)address + (ulong)size <= end) {
                        Array.Copy(data, (long)(address - baseAddr), buffer, 0, size);
                        return true;
                    }
                }
                return false;
            }
            public bool Write(ulong address, byte[] buffer, int size) {
                for (int i = 0; i < _blocks.Count; i++) {
                    var (baseAddr, data) = _blocks[i];
                    ulong end = baseAddr + (ulong)data.Length;
                    if (address >= baseAddr && (ulong)address + (ulong)size <= end) {
                        Array.Copy(buffer, 0, data, (long)(address - baseAddr), size);
                        return true;
                    }
                }
                var copy = new byte[size]; Array.Copy(buffer, copy, size); _blocks.Add((address, copy)); return true;
            }
            public MockProcess(int pid) { Pid = pid; }
            public void AddModule(string name, ulong baseAddr, int size) => _modules.Add((name, (IntPtr)(long)baseAddr, size));
            public void SetMemory(ulong addr, byte[] data) => _blocks.Add((addr, data));
        }

        private static byte[] Pad(List<byte> list, int size) { while (list.Count < size) list.Add(0); return list.ToArray(); }

        private static byte[] BuildOffsetsBlob64(int skipPointers, ulong evalBreaker, ulong remoteSupport, ulong pendingCall, ulong scriptPath, ulong scriptPathSize, uint version = 0x030E0000, byte flags = 0) {
            var cookie = Encoding.ASCII.GetBytes("xdebugpy");
            var buf = new List<byte>(256);
            buf.AddRange(cookie);
            while (buf.Count < 8) buf.Add(0);
            buf.AddRange(BitConverter.GetBytes(version));
            buf.Add(flags);
            while ((buf.Count % 8) != 0) buf.Add(0);
            for (int i = 0; i < skipPointers; i++) buf.AddRange(BitConverter.GetBytes((ulong)0));
            buf.AddRange(BitConverter.GetBytes(evalBreaker));
            buf.AddRange(BitConverter.GetBytes(remoteSupport));
            buf.AddRange(BitConverter.GetBytes(pendingCall));
            buf.AddRange(BitConverter.GetBytes(scriptPath));
            buf.AddRange(BitConverter.GetBytes(scriptPathSize));
            return Pad(buf, 256);
        }

        private static byte[] BuildOffsetsBlobMixed32(int skipPointers, ulong evalBreaker, uint remoteSupport, uint pendingCall, uint scriptPath, uint scriptPathSize, uint version = 0x030E0000, byte flags = 0) {
            var cookie = Encoding.ASCII.GetBytes("xdebugpy");
            var buf = new List<byte>(256);
            buf.AddRange(cookie);
            while (buf.Count < 8) buf.Add(0);
            buf.AddRange(BitConverter.GetBytes(version));
            buf.Add(flags);
            while ((buf.Count % 8) != 0) buf.Add(0);
            for (int i = 0; i < skipPointers; i++) buf.AddRange(BitConverter.GetBytes((ulong)0));
            // Always write full qwords so the parser (which reads 8-byte units) interprets values correctly.
            buf.AddRange(BitConverter.GetBytes((ulong)evalBreaker));
            buf.AddRange(BitConverter.GetBytes((ulong)remoteSupport));
            buf.AddRange(BitConverter.GetBytes((ulong)pendingCall));
            buf.AddRange(BitConverter.GetBytes((ulong)scriptPath));
            buf.AddRange(BitConverter.GetBytes((ulong)scriptPathSize));
            return Pad(buf, 256);
        }

        private MockProcess CreateHappyProcess(out ulong evalBreakerOff, out ulong tstatePtr) {
            var proc = new MockProcess(4242);
            ulong pyBase = 0x10000000; int pySize = 0x200000; proc.AddModule("python314.dll", pyBase, pySize);
            var module = new byte[pySize];
            // DOS stub
            module[0] = (byte)'M'; module[1] = (byte)'Z';
            int e_lfanew = 0x80; BitConverter.GetBytes(e_lfanew).CopyTo(module, 0x3C);
            // PE signature
            module[e_lfanew + 0] = (byte)'P'; module[e_lfanew + 1] = (byte)'E'; module[e_lfanew + 2] = 0; module[e_lfanew + 3] = 0;
            int coff = e_lfanew + 4;
            // numberOfSections = 1
            BitConverter.GetBytes((ushort)1).CopyTo(module, coff + 2);
            // sizeOfOptionalHeader = 0 to keep section table immediately after COFF header
            BitConverter.GetBytes((ushort)0).CopyTo(module, coff + 16);
            int sectionTable = coff + 20; // opt header size 0
            // Section 0: name "PyRuntim" (8 bytes)
            var nameBytes = Encoding.ASCII.GetBytes("PyRuntim"); Array.Copy(nameBytes, 0, module, sectionTable, nameBytes.Length);
            uint virtualSize = 0x4000; BitConverter.GetBytes(virtualSize).CopyTo(module, sectionTable + 8);
            uint virtualAddress = 0x1000; BitConverter.GetBytes(virtualAddress).CopyTo(module, sectionTable + 12);
            uint rawSize = 0x4000; BitConverter.GetBytes(rawSize).CopyTo(module, sectionTable + 16);
            // Build _Py_DebugOffsets slab inside section at RVA 0x1000
            evalBreakerOff = 0x128;
            ulong remoteSupportOff = 0x200;
            ulong pendingOff = 0x0;
            ulong scriptPathOff = 0x4;
            ulong scriptPathSize = 512;
            var blob = BuildOffsetsBlob64(0, evalBreakerOff, remoteSupportOff, pendingOff, scriptPathOff, scriptPathSize);
            Array.Copy(blob, 0, module, (int)virtualAddress, blob.Length);
            proc.SetMemory(pyBase, module);

            // Allocate a plausible tstate region
            tstatePtr = 0x0000000123456000UL;
            int tstateSize = (int)(remoteSupportOff + scriptPathOff + scriptPathSize + 0x200);
            var tstateMem = new byte[tstateSize];
            proc.SetMemory(tstatePtr, tstateMem);
            return proc;
        }

        [TestMethod]
        public void Orchestrator_Success_Path() {
            var proc = CreateHappyProcess(out var evalBreakerOff, out var tstate);
            Environment.SetEnvironmentVariable("PTVS_SAFE_ATTACH_MANAGED_WRITE", "1");
            // Force orchestrator to use our known tstate value
            Environment.SetEnvironmentVariable("PTVS_SAFE_ATTACH_MANAGED_TEST_TSTATE", tstate.ToString("X"));
            var res = SafeAttachOrchestrator.TryManagedSafeAttach(proc);
            Assert.IsTrue(res.Success, $"Expected success but failed: site={res.FailureSite} msg={res.Message}");
            Assert.AreEqual(0x03, res.MajorVersion);
            Assert.AreEqual(0x0E, res.MinorVersion);
            Assert.AreEqual(true, res.Success);
        }

        private static ulong GetRemoteSupportScriptSize(MockProcess proc, ulong tstate) => 512;

        [TestMethod]
        public void OffsetsParser_64_NoSkip() {
            var blob = BuildOffsetsBlob64(0, 0x1234, 0x300, 0x10, 0x20, 512);
            Assert.IsTrue(DebugOffsetsParser.TryParse(blob, 0x10000000, 8, out var parsed, out var fail), fail);
            Assert.AreEqual((ulong)512, parsed.ScriptPathSize);
            Assert.AreEqual((ulong)0x300, parsed.RemoteSupport);
            Assert.AreEqual((ulong)0x10, parsed.PendingCall);
        }

        [TestMethod]
        public void OffsetsParser_64_SkipOne() {
            var blob = BuildOffsetsBlob64(1, 0x5555, 0x280, 0x18, 0x40, 512);
            Assert.IsTrue(DebugOffsetsParser.TryParse(blob, 0x10000000, 8, out var parsed, out var fail), fail);
            Assert.AreEqual((ulong)512, parsed.ScriptPathSize);
            Assert.AreEqual((ulong)0x280, parsed.RemoteSupport);
        }

        [TestMethod]
        public void OffsetsParser_64_SkipTwo() {
            var blob = BuildOffsetsBlob64(2, 0x7777, 0x350, 0x08, 0x30, 512);
            Assert.IsTrue(DebugOffsetsParser.TryParse(blob, 0x10000000, 8, out var parsed, out var fail), fail);
            Assert.AreEqual((ulong)512, parsed.ScriptPathSize);
            Assert.AreEqual((ulong)0x350, parsed.RemoteSupport);
        }

        [TestMethod]
        public void OffsetsParser_Mixed32_NoSkip() {
            var blob = BuildOffsetsBlobMixed32(0, 0x9999, 0x310, 0x14, 0x24, 512);
            Assert.IsTrue(DebugOffsetsParser.TryParse(blob, 0x10000000, 8, out var parsed, out var fail), fail);
            Assert.AreEqual((ulong)512, parsed.ScriptPathSize);
            Assert.AreEqual((ulong)0x310, parsed.RemoteSupport);
        }

        [TestMethod]
        public void OffsetsParser_Fails_On_WrongSize() {
            var blob = BuildOffsetsBlob64(0, 0x1234, 0x300, 0x10, 0x20, 1024);
            Assert.IsFalse(DebugOffsetsParser.TryParse(blob, 0x10000000, 8, out var parsed, out var fail));
            Assert.AreEqual("no valid layout", fail);
        }
    }
}
