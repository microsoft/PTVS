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
using System.ComponentModel.Composition;
using System.IO;
using System.Reflection;

namespace Microsoft.PythonTools.Miniconda {
    [Export(typeof(ICondaLocator))]
    [ExportMetadata("Priority", 500)]
    public class MinicondaLocator : ICondaLocator {
        private static Lazy<string> _executablePath = new Lazy<string>(() =>
            GetFromAssembly(Assembly.GetExecutingAssembly(), @"Miniconda3-x64\Scripts\conda.exe")
        );

        public string CondaExecutablePath => _executablePath.Value;

        private static string GetFromAssembly(Assembly assembly, string filename) {
            string path = Path.Combine(
                Path.GetDirectoryName(assembly.Location),
                filename
            );
            if (File.Exists(path)) {
                return path;
            }
            return string.Empty;
        }
    }
}
