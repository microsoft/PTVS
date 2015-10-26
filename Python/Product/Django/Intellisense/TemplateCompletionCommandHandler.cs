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

using Microsoft.VisualStudio.Text.Editor;
using Microsoft.Web.Editor.Completion;
using Microsoft.Web.Editor.Services;

namespace Microsoft.PythonTools.Django.Intellisense {
    internal class TemplateCompletionCommandHandler : CompletionCommandHandler {
        public TemplateCompletionCommandHandler(ITextView textView)
            : base(textView) {
        }

        public override CompletionController CompletionController {
            get {
                return ServiceManager.GetService<TemplateCompletionController>(TextView);
            }
        }
    }
}
