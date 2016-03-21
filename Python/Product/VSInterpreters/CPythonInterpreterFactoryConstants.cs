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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Reflection;

namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// Provides constants used to identify interpreters that are detected from
    /// the CPython registry settings.
    /// 
    /// This class used by Microsoft.PythonTools.dll to register the
    /// interpreters.
    /// </summary>
    public static class CPythonInterpreterFactoryConstants {
        public const string ConsoleExecutable = "python.exe";
        public const string WindowsExecutable = "pythonw.exe";
        public const string LibrarySubPath = "lib";
        public const string PathEnvironmentVariableName = "PYTHONPATH";

        public const string Description32 = "Python";
        public const string Description64 = "Python 64-bit";

        public static string GetIntepreterId(string vendor, ProcessorArchitecture? arch, string key) {
            string archStr;
            switch (arch) {
                case ProcessorArchitecture.Amd64: archStr = "x64"; break;
                case ProcessorArchitecture.X86: archStr = "x86"; break;
                default: archStr = "unknown"; break;
            }

            return String.Join(
                "|", 
                CPythonInterpreterFactoryProvider.FactoryProviderName,
                vendor,
                key,
                archStr
            );
        }
    }
}
