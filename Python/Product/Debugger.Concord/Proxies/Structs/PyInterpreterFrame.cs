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
