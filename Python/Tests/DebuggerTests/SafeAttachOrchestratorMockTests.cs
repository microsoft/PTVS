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
            buf.AddRange(BitConverter.GetBytes(evalBreaker));
            buf.AddRange(BitConverter.GetBytes(remoteSupport));
            buf.AddRange(BitConverter.GetBytes(pendingCall));
            buf.AddRange(BitConverter.GetBytes(scriptPath));
            buf.AddRange(BitConverter.GetBytes(scriptPathSize));
            return Pad(buf, 256);
        }

        private MockProcess CreateHappyProcess(out ulong evalBreakerAddr, out ulong tstatePtr, out ulong remoteSupportStructAddr) {
            var proc = new MockProcess(4242);
            ulong pyBase = 0x10000000; int pySize = 0x200000; proc.AddModule("python314.dll", pyBase, pySize);
            var module = new byte[pySize];
            module[0] = (byte)'M'; module[1] = (byte)'Z'; int e_lfanew = 0x80; BitConverter.GetBytes(e_lfanew).CopyTo(module, 0x3C); module[e_lfanew] = (byte)'P'; module[e_lfanew + 1] = (byte)'E';
            BitConverter.GetBytes((ushort)1).CopyTo(module, e_lfanew + 24 + 92);
            int dataDirBase = e_lfanew + 24 + 96; uint exportRva = 0x300; BitConverter.GetBytes(exportRva).CopyTo(module, dataDirBase + 0); BitConverter.GetBytes((uint)0x40).CopyTo(module, dataDirBase + 4);
            uint namesRva = 0x400; uint ordRva = 0x420; uint funcsRva = 0x440; BitConverter.GetBytes((uint)1).CopyTo(module, exportRva + 24);
            BitConverter.GetBytes(funcsRva).CopyTo(module, exportRva + 28);
            BitConverter.GetBytes(namesRva).CopyTo(module, exportRva + 32);
            BitConverter.GetBytes(ordRva).CopyTo(module, exportRva + 36);
            uint nameStringRva = 0x460; BitConverter.GetBytes(nameStringRva).CopyTo(module, (int)namesRva);
            BitConverter.GetBytes((ushort)0).CopyTo(module, (int)ordRva);
            uint symbolRva = 0x480; BitConverter.GetBytes(symbolRva).CopyTo(module, (int)funcsRva);
            var nameBytes = Encoding.ASCII.GetBytes("_PyThreadState_Current\0"); Array.Copy(nameBytes, 0, module, nameStringRva, nameBytes.Length);

            tstatePtr = 0xDEADBEEFCAFEBABE;
            Array.Copy(BitConverter.GetBytes(tstatePtr), 0, module, symbolRva, IntPtr.Size);

            evalBreakerAddr = 0x20000010;
            ulong remoteSupportOff = 0x300; ulong pendingOff = 0x10; ulong scriptPathOff = 0x20; ulong scriptPathSize = 512;
            var blob = BuildOffsetsBlob64(1, evalBreakerAddr, remoteSupportOff, pendingOff, scriptPathOff, scriptPathSize);
            Array.Copy(blob, 0, module, 0x1000, blob.Length);
            proc.SetMemory(pyBase, module);

            // Allocate remote_support struct at separate address and write pointer at tstate+remoteSupportOff
            remoteSupportStructAddr = 0x30000000;
            proc.SetMemory(remoteSupportStructAddr, new byte[scriptPathOff + scriptPathSize]);
            // Set pending flag storage
            proc.SetMemory(remoteSupportStructAddr + pendingOff, new byte[1]);
            // Provide memory for tstate remote support pointer (just the pointer value)
            proc.SetMemory(tstatePtr + remoteSupportOff, BitConverter.GetBytes(remoteSupportStructAddr));
            // Eval breaker memory
            proc.SetMemory(evalBreakerAddr, new byte[IntPtr.Size]);
            return proc;
        }

        [TestMethod]
        public void Orchestrator_Success_Path() {
            var proc = CreateHappyProcess(out var evalBreaker, out var tstate, out var supportStruct);
            Environment.SetEnvironmentVariable("PTVS_SAFE_ATTACH_MANAGED_WRITE", "1");
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
