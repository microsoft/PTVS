using System;
using System.Diagnostics;

namespace Microsoft.PythonTools.Debugger.ManagedSafeAttach {
    /// <summary>
    /// Thin convenience wrapper over ISafeAttachProcess providing typed pointer-size aware reads.
    /// Avoids Span to keep compatibility with .NET Framework 4.7.2 without extra packages.
    /// </summary>
    internal sealed class ProcessMemory {
        private readonly ISafeAttachProcess _proc;
        private readonly int _ptrSize;
        public ProcessMemory(ISafeAttachProcess proc) { _proc = proc; _ptrSize = IntPtr.Size; }
        public int PointerSize => _ptrSize;
        public bool TryRead(ulong address, byte[] buffer) => _proc.Read(address, buffer, buffer.Length);
        public bool TryReadU32(ulong address, out uint value) {
            var buf = new byte[4]; value = 0; if (!_proc.Read(address, buf, 4)) return false; value = BitConverter.ToUInt32(buf, 0); return true;
        }
        public bool TryReadPointer(ulong address, out ulong value) {
            var buf = new byte[_ptrSize]; value = 0; if (!_proc.Read(address, buf, buf.Length)) return false; value = _ptrSize == 8 ? BitConverter.ToUInt64(buf, 0) : BitConverter.ToUInt32(buf, 0); return true;
        }
        public bool TryWrite(ulong address, byte[] data) => _proc.Write(address, data, data.Length);
    }
}
