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
using System.Diagnostics;
using System.Text;
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.PythonTools.DkmDebugger.Proxies {
    /// <summary>
    /// A data proxy for a C-style null-terminated UTF-8 string.
    /// </summary>
    [DebuggerDisplay("& {Read()}")]
    internal struct CStringProxy : IDataProxy<string> {
        public DkmProcess Process { get; private set; }
        public ulong Address { get; private set; }

        public CStringProxy(DkmProcess process, ulong address)
            : this() {
            Debug.Assert(process != null && address != 0);
            Process = process;
            Address = address;
        }

        public long ObjectSize {
            get { return Read().Length; }
        }

        public unsafe string Read() {
            byte[] buf = Process.ReadMemoryString(Address, DkmReadMemoryFlags.None, 1, 0x10000);
            int count = buf.Length;
            if (count > 0 && buf[count - 1] == 0) {
                --count;
            }
            return Encoding.UTF8.GetString(buf, 0, count);
        }

        object IValueStore.Read() {
            return Read();
        }
    }
}
