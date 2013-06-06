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

using System.Collections.Generic;
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.PythonTools.DkmDebugger.Proxies.Structs {
    internal class PyInterpreterState : StructProxy {
        private class Fields {
            public StructField<PointerProxy<PyInterpreterState>> next;
            public StructField<PointerProxy<PyThreadState>> tstate_head;
            public StructField<PointerProxy<PyDictObject>> modules;
        }

        private readonly Fields _fields;

        public PyInterpreterState(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
        }

        public PointerProxy<PyInterpreterState> next {
            get { return GetFieldProxy(_fields.next); }
        }

        public PointerProxy<PyThreadState> tstate_head {
            get { return GetFieldProxy(_fields.tstate_head); }
        }

        public PointerProxy<PyDictObject> modules {
            get { return GetFieldProxy(_fields.modules); }
        }

        private class InterpHeadHolder : DkmDataItem {
            public readonly PointerProxy<PyInterpreterState> Proxy;

            public InterpHeadHolder(DkmProcess process) {
                Proxy = process.GetPythonRuntimeInfo().DLLs.Python.GetStaticVariable<PointerProxy<PyInterpreterState>>("interp_head");
            }
        }

        public static PointerProxy<PyInterpreterState> interp_head(DkmProcess process) {
            return process.GetOrCreateDataItem(() => new InterpHeadHolder(process)).Proxy;
        }

        public static IEnumerable<PyInterpreterState> GetInterpreterStates(DkmProcess process) {
            for (var interp = interp_head(process).TryRead(); interp != null; interp = interp.next.TryRead()) {
                yield return interp;
            }
        }

        public IEnumerable<PyThreadState> GetThreadStates() {
            for (var tstate = tstate_head.TryRead(); tstate != null; tstate = tstate.next.TryRead()) {
                yield return tstate;
            }
        }
    }
}
