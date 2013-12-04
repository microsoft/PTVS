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

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.Web.Editor;

namespace Microsoft.PythonTools.Django.Intellisense {
    internal class TemplateTypingCommandHandler : TypingCommandHandler {
        private readonly IEditorOperations _editorOperations;
        private readonly IEditorOptions _editorOptions;

        public TemplateTypingCommandHandler(
            ITextView textView,
            ITextBuffer textBuffer,
            IEditorOptions editorOptions,
            IEditorOperations editorOperations)
            : base(textView, textBuffer)
        {
            _editorOperations = editorOperations;
            _editorOptions = editorOptions;
        }

        protected override Web.Editor.Intellisense.CompletionController CompletionController {
            get {
                return ServiceManager.GetService<TemplateCompletionController>(TextView);
            }
        }
    }

}

#endif