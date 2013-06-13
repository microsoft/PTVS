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

using Microsoft.VisualStudio.Debugger;

namespace Microsoft.PythonTools.DkmDebugger.Proxies.Structs {
    internal class PyCellObject : PyObject {
        public class Fields {
            public StructField<PointerProxy<PyObject>> ob_ref;
        }

        private readonly Fields _fields;

        public PyCellObject(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
            CheckPyType<PyCellObject>();
        }

        public PointerProxy<PyObject> ob_ref {
            get { return GetFieldProxy(_fields.ob_ref); }
        }

        public override void Repr(ReprBuilder builder) {
            builder.AppendFormat("<cell at {0:PTR}: ", Address);

            var obj = ob_ref.TryRead();
            if (obj != null) {
                builder.AppendFormat("{0} object at {1:PTR}>", obj.ob_type.Read().tp_name.Read().ToString(), obj.Address);
            } else {
                builder.Append("empty>");
            }
        }
    }
}
