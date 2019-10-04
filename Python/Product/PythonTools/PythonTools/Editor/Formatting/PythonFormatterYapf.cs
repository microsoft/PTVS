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

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.PythonTools.Editor.Formatting {
    [Export(typeof(IPythonFormatter))]
    internal class PythonFormatterYapf : PythonFormatter {
        public override string Identifier => "yapf";

        public override string DisplayName => "yapf";

        public override string PackageSpec => "yapf";

        protected override string[] GetToolCommandArgs(string documentFilePath, Range range, string[] extraArgs) {
            var args = new List<string>() { "-m", "yapf", "--diff", documentFilePath };
            if (range != null) {
                args.Add("--lines");
                args.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}-{1}",
                        range.Start.Line + 1,
                        range.End.Line + 1
                    )
                );
            }
            return args.ToArray();
        }
    }
}
