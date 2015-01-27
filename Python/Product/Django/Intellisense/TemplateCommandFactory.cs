/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

#if DEV12_OR_LATER

using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

#if DEV14_OR_LATER
using Microsoft.Web.Editor.Controller;
#else
using Microsoft.Web.Editor;
#endif

namespace Microsoft.PythonTools.Django.Intellisense {
    [Export(typeof(ICommandFactory))]
    [ContentType(TemplateTagContentType.ContentTypeName)]
    internal class TemplateCommandFactory : ICommandFactory {
        [Import(typeof(IEditorOptionsFactoryService))]
        IEditorOptionsFactoryService _editorOptionsFactory = null;

        [Import(typeof(IEditorOperationsFactoryService))]
        IEditorOperationsFactoryService _editorOperationsFactory = null;

        public TemplateCommandFactory() {
        }

        public IEnumerable<ICommand> GetCommands(ITextView textView, ITextBuffer textBuffer) {
            return new ICommand[] {
                new TemplateTypingCommandHandler(
                    textView, textBuffer,
                    _editorOptionsFactory.GetOptions(textView),
                    _editorOperationsFactory.GetEditorOperations(textView)),
                new TemplateCompletionCommandHandler(textView)
            };
        }
    }
}

#endif