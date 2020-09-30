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
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.PythonTools.Editor.Formatting {
    [Export(typeof(IPythonFormatter))]
    internal sealed class PythonFormatterAutopep8 : PythonFormatter {
        public override string Identifier => "autopep8";

        public override string DisplayName => "autopep8";

        public override string PackageSpec => "autopep8";

        protected override string[] GetToolCommandArgs(string documentFilePath, Range range, string[] extraArgs) {
            var args = new List<string> { "-m", "autopep8", "--diff", documentFilePath };
            if (range != null) {
                args.Add("--line-range");
                args.Add((range.Start.Line + 1).ToString());
                args.Add((range.End.Line + 1).ToString());
            }
            return args.ToArray();
        }
    }
}
