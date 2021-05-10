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
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.PythonTools.Editor {
    [Export(typeof(ICommandHandler))]
    [ContentType(PythonCoreConstants.ContentType)]
    [Name(nameof(PythonInsertSnippetCommandHandler))]
    internal class PythonInsertSnippetCommandHandler :
        ICommandHandler<InsertSnippetCommandArgs>,
        ICommandHandler<SurroundWithCommandArgs>,
        ICommandHandler<TabKeyCommandArgs>,
        ICommandHandler<BackTabKeyCommandArgs>,
        ICommandHandler<ReturnKeyCommandArgs>,
        ICommandHandler<EscapeKeyCommandArgs> {

        private readonly PythonSnippetManager _snippetManager;

        [ImportingConstructor]
        public PythonInsertSnippetCommandHandler([Import] PythonSnippetManager snippetManager) {
            _snippetManager = snippetManager ?? throw new ArgumentNullException(nameof(snippetManager));
        }

        public string DisplayName => nameof(PythonInsertSnippetCommandHandler);

        public CommandState GetCommandState(InsertSnippetCommandArgs args) => CommandState.Available;

        public CommandState GetCommandState(SurroundWithCommandArgs args) => CommandState.Available;

        public CommandState GetCommandState(TabKeyCommandArgs args) => CommandState.Unspecified;

        public CommandState GetCommandState(BackTabKeyCommandArgs args) => CommandState.Unspecified;

        public CommandState GetCommandState(ReturnKeyCommandArgs args) => CommandState.Unspecified;

        public CommandState GetCommandState(EscapeKeyCommandArgs args) => CommandState.Unspecified;

        public bool ExecuteCommand(InsertSnippetCommandArgs args, CommandExecutionContext executionContext) {
            return _snippetManager.ShowInsertionUI(args.TextView, isSurroundsWith: false);
        }

        public bool ExecuteCommand(SurroundWithCommandArgs args, CommandExecutionContext executionContext) {
            return _snippetManager.ShowInsertionUI(args.TextView, isSurroundsWith: true);
        }

        public bool ExecuteCommand(TabKeyCommandArgs args, CommandExecutionContext executionContext) {
            if (_snippetManager.IsInSession(args.TextView)) {
                return _snippetManager.MoveToNextField(args.TextView);
            } else {
                return _snippetManager.TryTriggerExpansion(args.TextView);
            }
        }

        public bool ExecuteCommand(BackTabKeyCommandArgs args, CommandExecutionContext executionContext) {
            if (_snippetManager.IsInSession(args.TextView)) {
                return _snippetManager.MoveToPreviousField(args.TextView);
            }

            return false;
        }

        public bool ExecuteCommand(ReturnKeyCommandArgs args, CommandExecutionContext executionContext) {
            if (_snippetManager.IsInSession(args.TextView)) {
                return _snippetManager.EndSession(args.TextView, leaveCaret: false);
            }

            return false;
        }

        public bool ExecuteCommand(EscapeKeyCommandArgs args, CommandExecutionContext executionContext) {
            if (_snippetManager.IsInSession(args.TextView)) {
                return _snippetManager.EndSession(args.TextView, leaveCaret: true);
            }

            return false;
        }
    }
}
