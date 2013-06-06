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
using System.Linq;
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.PythonTools.DkmDebugger.Proxies.Structs {
    internal class PyListObject : PyVarObject {
        private class Fields {
            public StructField<PointerProxy<ArrayProxy<PointerProxy<PyObject>>>> ob_item;
        }

        private readonly Fields _fields;

        public PyListObject(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
            CheckPyType<PyListObject>();
        }

        public PointerProxy<ArrayProxy<PointerProxy<PyObject>>> ob_item {
            get { return GetFieldProxy(_fields.ob_item); }
        }

        public IEnumerable<PointerProxy<PyObject>> ReadElements() {
            return ob_item.Read().Take((int)ob_size.Read());
        }

        protected override string Repr(Func<PyObject, string> repr) {
            return "[" + string.Join(", ", ReadElements().Take(MaxDebugChildren).Select(obj => repr(obj.Read()))) + "]";
        }

        public override IEnumerable<KeyValuePair<string, IValueStore>> GetDebugChildren() {
            int i = 0;
            foreach (var item in ReadElements()) {
                yield return new KeyValuePair<string, IValueStore>("[" + i + "]", item);
                ++i;
            }
        }
    }
}
