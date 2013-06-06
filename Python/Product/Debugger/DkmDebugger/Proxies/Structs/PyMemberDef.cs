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

using System.Text;
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.PythonTools.DkmDebugger.Proxies.Structs {
    internal enum PyMemberDefType {
        T_SHORT = 0,
        T_INT = 1,
        T_LONG = 2,
        T_FLOAT = 3,
        T_DOUBLE = 4,
        T_STRING = 5,
        T_OBJECT = 6,
        T_CHAR = 7,
        T_BYTE = 8,
        T_UBYTE = 9,
        T_USHORT = 10,
        T_UINT = 11,
        T_ULONG = 12,
        T_STRING_INPLACE = 13,
        T_BOOL = 14,
        T_OBJECT_EX = 16,
        T_LONGLONG = 17,
        T_ULONGLONG = 18,
        T_PYSSIZET = 19,
        T_NONE = 20
    }

    internal class PyMemberDef : StructProxy {
        private class Fields {
            public StructField<PointerProxy<CStringProxy>> name;
            public StructField<Int32EnumProxy<PyMemberDefType>> type;
            public StructField<SSizeTProxy> offset;
        }

        private readonly Fields _fields;

        public PyMemberDef(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
        }

        public PointerProxy<CStringProxy> name {
            get { return GetFieldProxy(_fields.name); }
        }

        public Int32EnumProxy<PyMemberDefType> type {
            get { return GetFieldProxy(_fields.type); }
        }

        public SSizeTProxy offset {
            get { return GetFieldProxy(_fields.offset); }
        }
    }
}
