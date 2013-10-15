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

using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Native;

namespace Microsoft.PythonTools.DkmDebugger {

    internal class PythonDLLs {
        private static readonly Regex pythonName = new Regex(@"^python(\d\d)(?:_d)?\.dll$");

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
            var m = pythonName.Match(moduleInstance.Name);
            if (!m.Success) {
                return PythonLanguageVersion.None;
            }

            var ver = m.Groups[1].Value;
            switch (ver) {
                case "27": return PythonLanguageVersion.V27;
                case "33": return PythonLanguageVersion.V33;
                case "34": return PythonLanguageVersion.V34;
                default: return PythonLanguageVersion.None;
            }
        }
    }

    internal class PythonRuntimeInfo : DkmDataItem {
        public PythonLanguageVersion LanguageVersion { get; set; }

        public PythonDLLs DLLs { get; private set; }

        public PythonRuntimeInfo() {
            DLLs = new PythonDLLs(this);
        }
    }

    internal static class PythonRuntimeInfoExtensions {
        public static PythonRuntimeInfo GetPythonRuntimeInfo(this DkmProcess process) {
            return process.GetOrCreateDataItem(() => new PythonRuntimeInfo());
        }
    }
}
