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

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.PythonTools.Editor {
    [Export(typeof(ICommandHandler))]
    [ContentType(PythonCoreConstants.ContentType)]
    [Name(nameof(PythonCommentSelectionCommandHandler))]
    internal class PythonCommentSelectionCommandHandler : ICommandHandler<CommentSelectionCommandArgs>, ICommandHandler<UncommentSelectionCommandArgs> {
        public string DisplayName => nameof(PythonCommentSelectionCommandHandler);

        public CommandState GetCommandState(CommentSelectionCommandArgs args) => CommandState.Available;

        public CommandState GetCommandState(UncommentSelectionCommandArgs args) => CommandState.Available;

        public bool ExecuteCommand(CommentSelectionCommandArgs args, CommandExecutionContext executionContext) {
            return CommentHelper.CommentOrUncommentBlock(args.TextView, comment: true);
        }

        public bool ExecuteCommand(UncommentSelectionCommandArgs args, CommandExecutionContext executionContext) {
            return CommentHelper.CommentOrUncommentBlock(args.TextView, comment: false);
        }
    }
}
