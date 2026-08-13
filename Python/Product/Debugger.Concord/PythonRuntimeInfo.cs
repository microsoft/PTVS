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

using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.PythonTools.Common.Parsing;
using Microsoft.PythonTools.Debugger.Concord.Proxies.Structs;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Native;

namespace Microsoft.PythonTools.Debugger.Concord {

    internal class PythonDLLs {
        private static readonly Regex pythonName = new Regex(@"^python(3\d+)(t)?(?:_d)?\.dll$");

        public static readonly string[] DebuggerHelperNames = {
            "Microsoft.PythonTools.Debugger.Helper.x86.dll",
            "Microsoft.PythonTools.Debugger.Helper.x64.dll",
        };

        public static readonly string[] CTypesNames = {
            "_ctypes.pyd", "_ctypes_d.pyd"
        };

        private readonly PythonRuntimeInfo _pyrtInfo;
        private DkmNativeModuleInstance _python;

        public PythonDLLs(PythonRuntimeInfo pyrtInfo) {
            _pyrtInfo = pyrtInfo;
        }

        public DkmNativeModuleInstance Python {
            get {
                return _python;
            }
            set {
                _python = value;
                if (value != null) {
                    _pyrtInfo.LanguageVersion = GetPythonLanguageVersion(value);
                    Debug.Assert(_pyrtInfo.LanguageVersion != PythonLanguageVersion.None);
                }
            }
        }

        public DkmNativeModuleInstance DebuggerHelper { get; set; }

        public DkmNativeModuleInstance CTypes { get; set; }

        public static PythonLanguageVersion GetPythonLanguageVersion(DkmNativeModuleInstance moduleInstance) {
            return GetPythonLanguageVersion(moduleInstance.Name);
        }

        internal static PythonLanguageVersion GetPythonLanguageVersion(string moduleName) {
            var m = pythonName.Match(moduleName);
            if (!m.Success) {
                return PythonLanguageVersion.None;
            }

            var ver = m.Groups[1].Value;
            PythonLanguageVersion version;
            switch (ver) {
                case "27": version = PythonLanguageVersion.V27; break;
                case "33": version = PythonLanguageVersion.V33; break;
                case "34": version = PythonLanguageVersion.V34; break;
                case "35": version = PythonLanguageVersion.V35; break;
                case "36": version = PythonLanguageVersion.V36; break;
                case "37": version = PythonLanguageVersion.V37; break;
                case "38": version = PythonLanguageVersion.V38; break;
                case "39": version = PythonLanguageVersion.V39; break;
                case "310": version = PythonLanguageVersion.V310; break;
                case "311": version = PythonLanguageVersion.V311; break;
                case "312": version = PythonLanguageVersion.V312; break;
                case "313": version = PythonLanguageVersion.V313; break;
                case "314": version = PythonLanguageVersion.V314; break;
                case "315": version = PythonLanguageVersion.V315; break;
                default: return PythonLanguageVersion.None;
            }

            return !m.Groups[2].Success || version >= PythonLanguageVersion.V313
                ? version
                : PythonLanguageVersion.None;
        }
    }

    internal class PythonRuntimeInfo : DkmDataItem {
        private bool _debugOffsetsProbed;
        private Proxies.Structs.PyDebugOffsets _debugOffsets;
        private bool _offsetProviderProbed;
        private Proxies.Structs.IStructFieldOffsetProvider _offsetProvider;

        public PythonLanguageVersion LanguageVersion { get; set; }

        public PythonDLLs DLLs { get; private set; }

        public PythonRuntimeInfo() {
            DLLs = new PythonDLLs(this);
        }

        public PyRuntimeState GetRuntimeState() {
            if (LanguageVersion < PythonLanguageVersion.V37) {
                return null;
            }
            return DLLs.Python.GetStaticVariable<PyRuntimeState>("_PyRuntime");
        }

        /// <summary>
        /// The self-describing <c>_Py_DebugOffsets</c> table exposed by CPython 3.14+ at the start of
        /// <c>_PyRuntime</c>, or null when the interpreter does not provide one (or it fails to validate).
        /// Read lazily once and cached, since it never changes for the lifetime of the process.
        /// </summary>
        public Proxies.Structs.PyDebugOffsets DebugOffsets {
            get {
                if (!_debugOffsetsProbed) {
                    _debugOffsetsProbed = true;
                    _debugOffsets = Proxies.Structs.PyDebugOffsets.TryRead(DLLs.Python?.Process, DLLs.Python);
                }
                return _debugOffsets;
            }
        }

        /// <summary>
        /// Offset source that <see cref="Proxies.StructProxy"/> consults before falling back to the
        /// interpreter PDB. Non-null only for CPython versions whose <c>_Py_DebugOffsets</c> layout this
        /// reader understands (3.14 and 3.15), where the table authoritatively describes the (potentially
        /// free-threaded-shifted) layout of the mixed-mode hot-path structs. Older interpreters, and any
        /// newer version this reader hasn't been taught yet, return null and resolve every field via the
        /// PDB exactly as before, so this cannot regress them.
        /// </summary>
        public Proxies.Structs.IStructFieldOffsetProvider StructFieldOffsetProvider {
            get {
                if (!_offsetProviderProbed) {
                    _offsetProviderProbed = true;
                    var offsets = DebugOffsets;
                    if (offsets != null && offsets.IsSupported) {
                        _offsetProvider = new Proxies.Structs.DebugOffsetsFieldProvider(offsets);
                    }
                }
                return _offsetProvider;
            }
        }
    }

    internal static class PythonRuntimeInfoExtensions {
        public static PythonRuntimeInfo GetPythonRuntimeInfo(this DkmProcess process) {
            return process.GetOrCreateDataItem(() => new PythonRuntimeInfo());
        }
    }
}
