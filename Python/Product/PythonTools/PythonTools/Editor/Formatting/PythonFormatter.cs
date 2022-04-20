﻿// Python Tools for Visual Studio
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
using System.Linq;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.PythonTools.Editor.Formatting {
    internal abstract class PythonFormatter: IPythonFormatter {
        public string Identifier { get; }
        public string DisplayName { get; }
        public string Package { get; }
        public bool CanFormatSelection { get; }

        protected PythonFormatter(string name, bool canFormatSelection) {
            DisplayName = Package = Identifier = name;
            CanFormatSelection = canFormatSelection;
        }

        public virtual async Task<TextEdit[]> FormatDocumentAsync(
            string interpreterExePath,
            string documentFilePath,
            string documentContents,
            Range range,
            string[] extraArgs
        ) {
            if (interpreterExePath == null) {
                throw new ArgumentNullException(nameof(interpreterExePath));
            }

            if (documentFilePath == null) {
                throw new ArgumentNullException(nameof(documentFilePath));
            }

            if (documentContents == null) {
                throw new ArgumentNullException(nameof(documentContents));
            }

            var diff = await RunToolAsync(interpreterExePath, documentFilePath, range, extraArgs);
            var edits = LspDiffTextEditFactory.GetEdits(documentContents, diff);
            return edits;
        }

        protected abstract string[] GetToolCommandArgs(string documentFilePath, Range range, string[] extraArgs);

        protected virtual async Task<string> RunToolAsync(string interpreterExePath, string documentFilePath, Range range, string[] extraArgs) {
            var output = ProcessOutput.RunHiddenAndCapture(
                interpreterExePath,
                System.Text.Encoding.UTF8,
                GetToolCommandArgs(documentFilePath, range, extraArgs)
            );

            await output;

            if (output.StandardErrorLines.Any(e => e.Contains("No module named"))) {
                throw new PythonFormatterModuleNotFoundException(
                    string.Join(Environment.NewLine, output.StandardErrorLines)
                );
            }

            if (output.StandardErrorLines.Any(e => e.Contains("ImportError"))) {
                throw new ApplicationException(
                    string.Join(Environment.NewLine, output.StandardErrorLines)
                );
            }

            if (output.ExitCode < 0) {
                throw new ApplicationException(
                    string.Join(Environment.NewLine, output.StandardErrorLines)
                );
            }

            return string.Join("\n", output.StandardOutputLines);
        }
    }
}
