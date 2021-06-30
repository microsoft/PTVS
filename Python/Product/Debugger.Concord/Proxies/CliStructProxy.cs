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

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies {
    internal struct CliStructProxy<TStruct> : IWritableDataProxy<TStruct> {
        public DkmProcess Process { get; private set; }
        public ulong Address { get; private set; }

        public CliStructProxy(DkmProcess process, ulong address)
            : this() {
            Debug.Assert(process != null && address != 0);
            Process = process;
            Address = address;
        }

        public long ObjectSize {
            get { return Marshal.SizeOf(typeof(TStruct)); }
        }

        public unsafe TStruct Read() {
            var buf = new byte[ObjectSize];
            Process.ReadMemory(Address, DkmReadMemoryFlags.None, buf);
            fixed (byte* p = buf) {
                return (TStruct)Marshal.PtrToStructure((IntPtr)p, typeof(TStruct));
            }
        }

        object IValueStore.Read() {
            return Read();
        }

        public unsafe void Write(TStruct value) {
            var buf = new byte[ObjectSize];
            fixed (byte* p = buf) {
                Marshal.StructureToPtr(value, (IntPtr)p, false);
            }
            Process.WriteMemory(Address, buf);
        }

        void IWritableDataProxy.Write(object value) {
            Write((TStruct)value);
        }
    }
}
