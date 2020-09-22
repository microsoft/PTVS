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
#if DJANGO_HTML_EDITOR
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.WebTools.Languages.Shared.Editor.Completion;

namespace Microsoft.PythonTools.Django.Intellisense {
    internal class TemplateCompletionController : CompletionController {
        private readonly PythonToolsService _pyService;

        public TemplateCompletionController(
            PythonToolsService pyService,
            ITextView textView,
            IList<ITextBuffer> subjectBuffers,
            ICompletionBroker completionBroker,
            IAsyncQuickInfoBroker quickInfoBroker,
            ISignatureHelpBroker signatureBroker) :
            base(textView, subjectBuffers, completionBroker, quickInfoBroker, signatureBroker) {
            _pyService = pyService ?? throw new ArgumentNullException(nameof(pyService));
        }

        public override bool IsTriggerChar(char typedCharacter) {
            const string triggerChars = " |.";
            return !HasActiveCompletionSession && triggerChars.IndexOf(typedCharacter) >= 0;
        }

        public override bool IsCommitChar(char typedCharacter) {
            if (!HasActiveCompletionSession) {
                return false;
            }

            if (typedCharacter == '\n' || typedCharacter == '\t') {
                return true;
            }

            return false;
        }

        protected override bool IsRetriggerChar(ICompletionSession session, char typedCharacter) {
            if (typedCharacter == ' ') {
                return true;
            }

            return base.IsRetriggerChar(session, typedCharacter);
        }
    }
}
#endif
