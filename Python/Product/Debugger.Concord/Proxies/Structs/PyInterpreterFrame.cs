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

        // CPython 3.14 _PyStackRef tag bits (Py_TAG_BITS). Object pointers are always at least
        // 8-byte aligned, so the low bits are free to carry the reference tag; strip them to get
        // the real PyObject* back.
        private const ulong StackRefTagMask = 0x3;

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

                var executable = GetFieldProxy(_fields.f_executable);
                if (Process.GetPythonRuntimeInfo().LanguageVersion >= PythonLanguageVersion.V314) {
                    // In 3.14, f_executable is a _PyStackRef rather than a plain PyObject*. Its low
                    // bits are a tag (Py_TAG_BITS == 3 for the default build; set for deferred/
                    // immortal references such as frozen-module code objects), so strip them to
                    // recover the PyCodeObject pointer. This mirrors CPython's own out-of-process
                    // reader (CLEAR_PTR_TAG in _remote_debugging_module.c).
                    executable = executable.WithTagMask(StackRefTagMask);
                }
                return executable;
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
            if (instrPtr == 0) {
                return 0;
            }

            int firstLineNo = code.co_firstlineno.Read();

            // Byte offset into the bytecode (addrq passed to PyCode_Addr2Line). On a freshly
            // created 3.11/3.12 frame, prev_instr points one _Py_CODEUNIT before the first
            // instruction, so the offset is negative. CPython's PyCode_Addr2Line maps a
            // negative offset to co_firstlineno; handle it here (before reading the line
            // table) so we don't fall back to the func-eval this whole path avoids.
            long addrq = (long)instrPtr - (long)codeStart;
            if (addrq < 0) {
                return firstLineNo;
            }

            byte[] lineTable = code.co_linetable.TryRead()?.ToBytes();
            if (lineTable == null || lineTable.Length == 0) {
                return 0;
            }

            int line = PyLineTable.Addr2Line(lineTable, firstLineNo, (int)addrq);
            return line > 0 ? line : 0;
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
