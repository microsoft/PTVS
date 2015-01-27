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
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

#if DEV14_OR_LATER
using Microsoft.Web.Editor.Completion;
#else
using Microsoft.Web.Editor.Intellisense;
#endif

namespace Microsoft.PythonTools.Django.Intellisense {
    internal class TemplateCompletionController : CompletionController {
        private readonly PythonToolsService _pyService;

        public TemplateCompletionController(
            PythonToolsService pyService,
            ITextView textView,
            IList<ITextBuffer> subjectBuffers,
            ICompletionBroker completionBroker,
            IQuickInfoBroker quickInfoBroker,
            ISignatureHelpBroker signatureBroker) :
            base(textView, subjectBuffers, completionBroker, quickInfoBroker, signatureBroker) {
            _pyService = pyService;
        }

        public override bool IsTriggerChar(char typedCharacter) {
            const string triggerChars = " |.";
            return _pyService.AdvancedOptions.AutoListMembers && !HasActiveCompletionSession && triggerChars.IndexOf(typedCharacter) >= 0;
        }

        public override bool IsCommitChar(char typedCharacter) {
            if (!HasActiveCompletionSession) {
                return false;
            }

            if (typedCharacter == '\n' || typedCharacter == '\t') {
                return true;
            }

            return _pyService.AdvancedOptions.CompletionCommittedBy.IndexOf(typedCharacter) > 0;
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