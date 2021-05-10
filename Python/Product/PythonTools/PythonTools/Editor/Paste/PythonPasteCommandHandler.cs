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
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.PythonTools.Editor {
    [Export(typeof(ICommandHandler))]
    [ContentType(PythonCoreConstants.ContentType)]
    [Name(nameof(PythonPasteCommandHandler))]
    internal class PythonPasteCommandHandler : ICommandHandler<PasteCommandArgs> {
        private readonly IServiceProvider _site;
        private readonly IEditorOperationsFactoryService _editOperationsFactory;
        private readonly ITextBufferUndoManagerProvider _undoManagerFactory;

        [ImportingConstructor]
        public PythonPasteCommandHandler(
            [Import(typeof(SVsServiceProvider))] IServiceProvider site,
            [Import] IEditorOperationsFactoryService editOperationsFactory,
            [Import] ITextBufferUndoManagerProvider undoManagerFactory
        ) {
            _site = site;
            _editOperationsFactory = editOperationsFactory;
            _undoManagerFactory = undoManagerFactory;
        }

        public string DisplayName => nameof(PythonPasteCommandHandler);

        public CommandState GetCommandState(PasteCommandArgs args) => CommandState.Unspecified;

        public bool ExecuteCommand(PasteCommandArgs args, CommandExecutionContext executionContext) {
            if (_site.GetPythonToolsService().FormattingOptions.PasteRemovesReplPrompts) {
                var beforePaste = args.TextView.TextSnapshot;
                if (_editOperationsFactory.GetEditorOperations(args.TextView).Paste()) {
                    var afterPaste = args.TextView.TextSnapshot;
                    var um = _undoManagerFactory.GetTextBufferUndoManager(afterPaste.TextBuffer);
                    using (var undo = um.TextBufferUndoHistory.CreateTransaction(Strings.RemoveReplPrompts)) {
                        if (ReplPromptHelpers.RemovePastedPrompts(beforePaste, afterPaste)) {
                            undo.Complete();
                        }
                    }
                    return true;
                }
            }

            return false;
        }
    }
}
