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
using Microsoft.PythonTools.Infrastructure;

namespace Microsoft.PythonTools.Interpreter {
    class PackageManagerUIRedirector : Redirector {
        private readonly IPackageManagerUI _ui;

        public static Redirector Get(IPackageManagerUI ui) {
            if (ui != null) {
                return new PackageManagerUIRedirector(ui);
            }
            return null;
        }

        private PackageManagerUIRedirector(IPackageManagerUI ui) {
            _ui = ui;
        }

        public override void WriteErrorLine(string line) {
            _ui.OnErrorTextReceived(line + Environment.NewLine);
        }

        public override void WriteLine(string line) {
            _ui.OnOutputTextReceived(line + Environment.NewLine);
        }
    }
}
