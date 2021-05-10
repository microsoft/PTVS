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
using System.Linq;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.PythonTools.Editor {
    [Export(typeof(ICommandHandler))]
    [ContentType(PythonCoreConstants.ContentType)]
    [Name(nameof(PythonReturnKeyCommandHandler))]
    internal class PythonReturnKeyCommandHandler : ICommandHandler<ReturnKeyCommandArgs> {
        public string DisplayName => nameof(PythonReturnKeyCommandHandler);

        public CommandState GetCommandState(ReturnKeyCommandArgs args) => CommandState.Unspecified;

        public bool ExecuteCommand(ReturnKeyCommandArgs args, CommandExecutionContext executionContext) {
            var pyPoint = args.TextView.GetPythonCaret();
            if (pyPoint != null) {
                // https://github.com/Microsoft/PTVS/issues/241
                // If the current line is a full line comment and we
                // are splitting the text, automatically insert the
                // comment marker on the new line.
                var line = pyPoint.Value.GetContainingLine();
                var lineText = pyPoint.Value.Snapshot.GetText(line.Start, pyPoint.Value - line.Start);
                int comment = lineText.IndexOf('#');
                if (comment >= 0 &&
                    pyPoint.Value < line.End &&
                    line.Start + comment < pyPoint.Value &&
                    string.IsNullOrWhiteSpace(lineText.Remove(comment))
                ) {
                    int extra = lineText.Skip(comment + 1).TakeWhile(char.IsWhiteSpace).Count() + 1;
                    using (var edit = line.Snapshot.TextBuffer.CreateEdit()) {
                        edit.Insert(
                            pyPoint.Value.Position,
                            args.TextView.Options.GetNewLineCharacter() + lineText.Substring(0, comment + extra)
                        );
                        edit.Apply();
                    }

                    return true;
                }
            }

            return false;
        }
    }
}
