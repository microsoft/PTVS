/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.DkmDebugger.Proxies;
using Microsoft.PythonTools.DkmDebugger.Proxies.Structs;
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.PythonTools.DkmDebugger {
    internal class PyObjectAllocator : DkmDataItem {
        private readonly DkmProcess _process;
        private readonly List<ulong> _blocks = new List<ulong>();

        public PyObjectAllocator(DkmProcess process) {
            _process = process;
        }

        private ulong Allocate(long size) {
            GC();
            ulong block = _process.AllocateVirtualMemory(0, (int)size, NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE, NativeMethods.PAGE_READWRITE);
            _blocks.Add(block);
            return block;
        }

        public TObject Allocate<TObject>(long extraBytes = 0)
            where TObject : PyObject {
            ulong ptr = Allocate(StructProxy.SizeOf<TObject>(_process) + extraBytes);
            var obj = DataProxy.Create<TObject>(_process, ptr, polymorphic: false);
            obj.ob_refcnt.Write(1);

            var pyType = PyObject.GetPyType<TObject>(_process);
            obj.ob_type.Write(pyType);

            return obj;
        }

        private void GC() {
            _blocks.RemoveAll(block => {
                var obj = new PyObject(_process, block);
                if (obj.ob_refcnt.Read() <= 1) {
                    _process.FreeVirtualMemory(block, 0, NativeMethods.MEM_RELEASE);
                    return true;
                } else {
                    return false;
                }
            });
        }
    }
}
