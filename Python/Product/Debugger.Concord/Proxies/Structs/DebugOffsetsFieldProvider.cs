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
using System.Collections.Generic;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs {
    /// <summary>
    /// Supplies the byte offset of a struct field by some means other than the interpreter's PDB.
    /// <see cref="StructProxy"/> consults this before falling back to DIA/PDB symbol lookup, letting
    /// us source authoritative offsets from CPython's self-describing tables when available.
    /// </summary>
    internal interface IStructFieldOffsetProvider {
        /// <summary>
        /// Returns true and sets <paramref name="offset"/> if this provider knows the offset of
        /// <paramref name="fieldName"/> within <paramref name="structName"/>; otherwise returns false
        /// so the caller falls back to its default (PDB) resolution.
        /// </summary>
        bool TryGetFieldOffset(string structName, string fieldName, out long offset);
    }

    /// <summary>
    /// Resolves a curated set of hot-path struct fields from CPython 3.14's <c>_Py_DebugOffsets</c>
    /// table (see <see cref="PyDebugOffsets"/>) instead of the PDB. Coverage is intentionally limited
    /// to the frame / code-object / thread-state fields that the mixed-mode stack walk and in-process
    /// line-number computation depend on; every other field still resolves via the PDB. This is what
    /// automatically tracks the free-threaded build's shifted object layouts.
    ///
    /// The map keys are CPython struct/field names (matching <see cref="StructProxy"/>'s field names
    /// and each proxy's <c>StructName</c>); the values are the corresponding
    /// <see cref="PyDebugOffsets"/> (group, field) entries.
    /// </summary>
    internal sealed class DebugOffsetsFieldProvider : IStructFieldOffsetProvider {
        private static readonly Dictionary<string, KeyValuePair<string, string>> Map =
            new Dictionary<string, KeyValuePair<string, string>>(StringComparer.Ordinal) {
                // _PyInterpreterFrame (StructName "_PyInterpreterFrame").
                { "_PyInterpreterFrame.previous", Entry("interpreter_frame", "previous") },
                { "_PyInterpreterFrame.f_executable", Entry("interpreter_frame", "executable") },
                { "_PyInterpreterFrame.instr_ptr", Entry("interpreter_frame", "instr_ptr") },
                { "_PyInterpreterFrame.localsplus", Entry("interpreter_frame", "localsplus") },
                { "_PyInterpreterFrame.owner", Entry("interpreter_frame", "owner") },

                // PyCodeObject (StructName "PyCodeObject").
                { "PyCodeObject.co_filename", Entry("code_object", "filename") },
                { "PyCodeObject.co_name", Entry("code_object", "name") },
                { "PyCodeObject.co_firstlineno", Entry("code_object", "firstlineno") },
                { "PyCodeObject.co_localsplusnames", Entry("code_object", "localsplusnames") },
                { "PyCodeObject.co_localspluskinds", Entry("code_object", "localspluskinds") },
                { "PyCodeObject.co_code_adaptive", Entry("code_object", "co_code_adaptive") },
                { "PyCodeObject.co_linetable", Entry("code_object", "linetable") },

                // PyThreadState (StructName "_ts").
                { "_ts.next", Entry("thread_state", "next") },
                { "_ts.interp", Entry("thread_state", "interp") },
                { "_ts.thread_id", Entry("thread_state", "thread_id") },
                { "_ts.current_frame", Entry("thread_state", "current_frame") },
            };

        private readonly PyDebugOffsets _offsets;

        public DebugOffsetsFieldProvider(PyDebugOffsets offsets) {
            _offsets = offsets ?? throw new ArgumentNullException(nameof(offsets));
        }

        public bool TryGetFieldOffset(string structName, string fieldName, out long offset) {
            offset = 0;
            if (structName == null || fieldName == null) {
                return false;
            }

            KeyValuePair<string, string> entry;
            if (!Map.TryGetValue(structName + "." + fieldName, out entry)) {
                return false;
            }

            offset = (long)_offsets.Offset(entry.Key, entry.Value);
            return true;
        }

        private static KeyValuePair<string, string> Entry(string group, string field) {
            return new KeyValuePair<string, string>(group, field);
        }
    }
}
