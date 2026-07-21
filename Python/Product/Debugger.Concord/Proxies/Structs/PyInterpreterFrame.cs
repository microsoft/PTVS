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
using Microsoft.PythonTools.Common.Parsing;
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs {
    // This was added in commit https://github.com/python/cpython/commit/ae0a2b756255629140efcbe57fc2e714f0267aa3
    [StructProxy(StructName = "_PyInterpreterFrame", MinVersion = PythonLanguageVersion.V311)]
    internal class PyInterpreterFrame : StructProxy {
        internal class Fields {
            public StructField<PointerProxy<PyDictObject>> f_globals;
            public StructField<PointerProxy<PyDictObject>> f_builtins;
            public StructField<PointerProxy<PyDictObject>> f_locals;
            [FieldProxy(MaxVersion = PythonLanguageVersion.V312)]
            public StructField<PointerProxy<PyCodeObject>> f_code;
            [FieldProxy(MinVersion = PythonLanguageVersion.V313)]
            public StructField<PointerProxy<PyCodeObject>> f_executable;
            public StructField<PointerProxy<PyFrameObject>> frame_obj;
            public StructField<PointerProxy<PyInterpreterFrame>> previous;
            public StructField<ArrayProxy<PointerProxy<PyObject>>> localsplus;
            public StructField<CharProxy> owner;
            // Pointer to the current bytecode instruction. Renamed from prev_instr to
            // instr_ptr in 3.13 (and it now points at the current instruction rather
            // than the previous one), but the offset math for the line table is identical.
            [FieldProxy(MaxVersion = PythonLanguageVersion.V312)]
            public StructField<PointerProxy> prev_instr;
            [FieldProxy(MinVersion = PythonLanguageVersion.V313)]
            public StructField<PointerProxy> instr_ptr;
        }

        private const int FRAME_OWNED_BY_THREAD = 0;
        private const int FRAME_OWNED_BY_GENERATOR = 1;
        private const int FRAME_OWNED_BY_FRAME_OBJECT = 2;
        private const int FRAME_OWNED_BY_CSTACK = 3;

        private readonly Fields _fields;

        public PyInterpreterFrame(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
        }

        public PointerProxy<PyCodeObject> f_code {
            get {
                // In 3.13, the f_code was renamed to f_executable
                if (_fields.f_code.Process != null) {
                    return GetFieldProxy(_fields.f_code);
                }
                return GetFieldProxy(_fields.f_executable);
            }
        }

        public PointerProxy<PyDictObject> f_globals {
            get { return GetFieldProxy(_fields.f_globals); }
        }

        public PointerProxy<PyDictObject> f_locals {
            get { return GetFieldProxy(_fields.f_locals); }
        }

        public ArrayProxy<PointerProxy<PyObject>> f_localsplus {
            get { return GetFieldProxy(_fields.localsplus); }
        }

        public PointerProxy<PyFrameObject> frame_obj {
            get { return GetFieldProxy(_fields.frame_obj); }
        }

        public PointerProxy<PyInterpreterFrame> previous => GetFieldProxy(_fields.previous);

        /// <summary>
        /// Computes the current source line number for this frame without executing any
        /// code in the debuggee. This mirrors CPython's <c>_PyInterpreterFrame_GetLine</c>
        /// (<c>PyCode_Addr2Line</c>): it finds the current bytecode offset from the frame's
        /// instruction pointer and decodes the code object's line table (PEP 626).
        /// Returns 0 if the line number cannot be determined (caller should fall back).
        /// </summary>
        public int ComputeLineNumber() {
            if (Process.GetPythonRuntimeInfo().LanguageVersion < PythonLanguageVersion.V311) {
                return 0;
            }

            var code = f_code.TryRead() as PyCodeObject311;
            if (code == null) {
                return 0;
            }

            // _PyCode_CODE(co) == &co->co_code_adaptive[0] - the start of the bytecode.
            ulong codeStart = code.co_code_adaptive.Address;

            // The current instruction pointer: prev_instr (3.11/3.12) or instr_ptr (3.13+).
            ulong instrPtr = _fields.instr_ptr.Process != null
                ? GetFieldProxy(_fields.instr_ptr).Read()
                : GetFieldProxy(_fields.prev_instr).Read();
            if (instrPtr == 0 || instrPtr < codeStart) {
                return 0;
            }

            // Byte offset into the bytecode (addrq passed to PyCode_Addr2Line).
            int addrq = (int)(instrPtr - codeStart);

            byte[] lineTable = code.co_linetable.TryRead()?.ToBytes();
            if (lineTable == null || lineTable.Length == 0) {
                return 0;
            }

            int line = Addr2Line(lineTable, code.co_firstlineno.Read(), addrq);
            return line > 0 ? line : 0;
        }

        // Managed port of CPython's PyCode_Addr2Line (Objects/codeobject.c). Only the
        // forward scan is needed since addrq is always resolved from a fresh range.
        private static int Addr2Line(byte[] lineTable, int firstLineNo, int addrq) {
            if (addrq < 0) {
                return firstLineNo;
            }

            int loNext = 0;
            int limit = lineTable.Length;
            int arEnd = 0;
            int computedLine = firstLineNo;
            int arLine = -1;

            while (arEnd <= addrq) {
                if (loNext >= limit) {
                    return -1;
                }

                byte first = lineTable[loNext];
                computedLine += GetLineDelta(lineTable, loNext);
                arLine = IsNoLineMarker(first) ? -1 : computedLine;
                arEnd += ((first & 7) + 1) * sizeof(ushort); // sizeof(_Py_CODEUNIT) == 2

                // Skip to the next entry; only the first byte of an entry has bit 7 set.
                do {
                    loNext++;
                } while (loNext < limit && (lineTable[loNext] & 128) == 0);
            }

            return arLine;
        }

        private static bool IsNoLineMarker(byte b) {
            return (b >> 3) == 0x1f;
        }

        private static int GetLineDelta(byte[] lineTable, int index) {
            int code = (lineTable[index] >> 3) & 15;
            switch (code) {
                case 15: // PY_CODE_LOCATION_INFO_NONE
                    return 0;
                case 13: // PY_CODE_LOCATION_INFO_NO_COLUMNS
                case 14: // PY_CODE_LOCATION_INFO_LONG
                    int p = index + 1;
                    return ReadSignedVarint(lineTable, ref p);
                case 10: // PY_CODE_LOCATION_INFO_ONE_LINE0
                    return 0;
                case 11: // PY_CODE_LOCATION_INFO_ONE_LINE1
                    return 1;
                case 12: // PY_CODE_LOCATION_INFO_ONE_LINE2
                    return 2;
                default: // short forms (same line)
                    return 0;
            }
        }

        private static int ReadVarint(byte[] data, ref int p) {
            uint read = data[p++];
            uint val = read & 63;
            int shift = 0;
            while ((read & 64) != 0 && p < data.Length) {
                read = data[p++];
                shift += 6;
                val |= (read & 63) << shift;
            }
            return (int)val;
        }

        private static int ReadSignedVarint(byte[] data, ref int p) {
            uint uval = (uint)ReadVarint(data, ref p);
            return ((uval & 1) != 0) ? -(int)(uval >> 1) : (int)(uval >> 1);
        }

        private bool OwnedByThread() {
            if (Process.GetPythonRuntimeInfo().LanguageVersion <= PythonLanguageVersion.V310) {
                return true;
            }
            var owner = (GetFieldProxy(_fields.owner) as IValueStore).Read();
            var charOwner = owner.ToString()[0];
            return (int)charOwner  == FRAME_OWNED_BY_THREAD;
        }

        public PointerProxy<PyFrameObject> FindBackFrame() {
            // Trace.WriteLine("Searching for back frame ...");
            var frame = previous.TryRead();
            while (frame != null && !frame.OwnedByThread()) {
                frame = frame.previous.TryRead();
            }

            // Make sure this frame_obj is pointing to this frame. Since the f_back 
            // of a PyFrameObject can be null even if it exists, the PyFrameObject we find
            // through the list of PyInterpreterFrames may not have been created. We need
            // to make sure it points to the PyInterpreterFrame we found so that subsequent calls
            // to get things like its f_locals will work.
            if (frame != null && !frame.frame_obj.IsNull) {
                var obj = frame.frame_obj.Read() as PyFrameObject311;
                obj.f_frame.Write(frame);
                return frame.frame_obj;
            }
            return default(PointerProxy<PyFrameObject>);
        }
    }
}
