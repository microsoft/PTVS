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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Microsoft.Web.Editor.ContainedLanguage;
using Microsoft.Web.Editor.Controller;
using Microsoft.Web.Editor.Host;
using Microsoft.Web.Editor.Services;
using ITextViewCreationListener = Microsoft.VisualStudio.Text.Editor.ITextViewCreationListener;

namespace Microsoft.PythonTools.Django.Intellisense {
    [Export(typeof(ITextViewConnectionListener))]
    [Export(typeof(ITextViewCreationListener))]
    [ContentType(TemplateTagContentType.ContentTypeName)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    [Name("Django Template Text View Connection Listener")]
    [Order(Before = "default")]
    internal class TemplateTextViewConnectionListener : TextViewConnectionListener,
        ITextViewConnectionListener,
        ITextViewCreationListener
    {
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

        void ITextViewConnectionListener.SubjectBuffersConnected(ITextView textView, ConnectionReason reason, System.Collections.Generic.IReadOnlyCollection<ITextBuffer> subjectBuffers) {
            SubjectBuffersConnected((IWpfTextView)textView, reason, new System.Collections.ObjectModel.Collection<ITextBuffer>(subjectBuffers.ToArray()));
        }

        void ITextViewConnectionListener.SubjectBuffersDisconnected(ITextView textView, ConnectionReason reason, System.Collections.Generic.IReadOnlyCollection<ITextBuffer> subjectBuffers) {
            SubjectBuffersDisconnected((IWpfTextView)textView, reason, new System.Collections.ObjectModel.Collection<ITextBuffer>(subjectBuffers.ToArray()));
        }

        void ITextViewCreationListener.TextViewCreated(ITextView textView) {
            TextViewCreated((IWpfTextView)textView);
        }
    }
}