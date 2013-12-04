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
using Microsoft.VisualStudio.Utilities;
using Microsoft.Web.Editor;
using Microsoft.Web.Editor.ContainedLanguage;

namespace Microsoft.PythonTools.Django.Intellisense {
    [Export(typeof(IWpfTextViewConnectionListener))]
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType(TemplateTagContentType.ContentTypeName)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    [Name("Django Template Text View Connection Listener")]
    [Order(Before = "default")]
    internal class TemplateTextViewConnectionListener : TextViewConnectionListener {
        protected override void OnTextViewConnected(ITextView textView, ITextBuffer textBuffer) {
            var mainController = ServiceManager.GetService<TemplateMainController>(textView) ??
                new TemplateMainController(textView, textBuffer);

            if (textBuffer != textView.TextBuffer) {
                var containedLanguageHost = ContainedLanguageHost.GetHost(textView, textBuffer);
                if (containedLanguageHost != null) {
                    object nextFilter = containedLanguageHost.SetContainedCommandTarget(textView, mainController);
                    mainController.ChainedController = WebEditor.TranslateCommandTarget(textView, nextFilter);
                }
            }

            base.OnTextViewConnected(textView, textBuffer);
        }

        protected override void OnTextViewDisconnected(ITextView textView, ITextBuffer textBuffer) {
            if (textBuffer != textView.TextBuffer) {
                var containedLanguageHost = ContainedLanguageHost.GetHost(textView, textBuffer);
                if (containedLanguageHost != null) {
                    containedLanguageHost.RemoveContainedCommandTarget(textView);
                }
            }

            base.OnTextViewDisconnected(textView, textBuffer);
        }
    }
}

#endif