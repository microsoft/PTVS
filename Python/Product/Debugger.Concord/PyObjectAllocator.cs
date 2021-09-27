// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using Microsoft.PythonTools.Debugger.Concord.Proxies;
using Microsoft.PythonTools.Debugger.Concord.Proxies.Structs;

namespace Microsoft.PythonTools.Debugger.Concord
{
    internal class PyObjectAllocator : DkmDataItem
    {
        // Layout of this struct must always remain in sync with DebuggerHelper/trace.cpp.
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        private struct ObjectToRelease
        {
            public ulong pyObject;
            public ulong next;
        }

        private readonly DkmProcess _process;
        private readonly List<ulong> _blocks = new List<ulong>();
        private readonly UInt64Proxy _objectsToRelease;

        public PyObjectAllocator(DkmProcess process)
        {
            _process = process;
            var pyrtInfo = process.GetPythonRuntimeInfo();

            _objectsToRelease = pyrtInfo.DLLs.DebuggerHelper.GetExportedStaticVariable<UInt64Proxy>("objectsToRelease");
        }

        private ulong Allocate(long size)
        {
            GC();
            ulong block = _process.AllocateVirtualMemory(0, (int)size, NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE, NativeMethods.PAGE_READWRITE);
            _blocks.Add(block);
            return block;
        }

        public TObject Allocate<TObject>(long extraBytes = 0)
            where TObject : PyObject
        {
            ulong ptr = Allocate(StructProxy.SizeOf<TObject>(_process) + extraBytes);
            var obj = DataProxy.Create<TObject>(_process, ptr, polymorphic: false);
            obj.ob_refcnt.Write(1);

            var pyType = PyObject.GetPyType<TObject>(_process);
            obj.ob_type.Write(pyType);

            return obj;
        }

        public unsafe void QueueForDecRef(PyObject obj)
        {
            byte[] buf = new byte[sizeof(ObjectToRelease)];
            fixed (byte* p = buf)
            {
                var otr = (ObjectToRelease*)p;
                otr->pyObject = obj.Address;
                otr->next = _objectsToRelease.Read();
            }

            ulong otrPtr = _process.AllocateVirtualMemory(0, sizeof(ObjectToRelease), NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE, NativeMethods.PAGE_READWRITE);
            _process.WriteMemory(otrPtr, buf);
            _objectsToRelease.Write(otrPtr);
        }

        private void GC()
        {
            _blocks.RemoveAll(block =>
            {
                var obj = new PyObject(_process, block);
                if (obj.ob_refcnt.Read() <= 1)
                {
                    _process.FreeVirtualMemory(block, 0, NativeMethods.MEM_RELEASE);
                    return true;
                }
                else
                {
                    return false;
                }
            });
        }
    }
}
