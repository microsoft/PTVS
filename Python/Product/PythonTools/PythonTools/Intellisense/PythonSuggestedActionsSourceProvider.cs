using Microsoft.VisualStudio.Language.Intellisense;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.PythonTools.Intellisense {
#if DEV14_OR_LATER
    [Export(typeof(ISuggestedActionsSourceProvider))]
    [Name("Python Suggested Actions")]
    [ContentType(PythonCoreConstants.ContentType)]
    class PythonSuggestedActionsSourceProvider : ISuggestedActionsSourceProvider {
        [Import(typeof(SVsServiceProvider))]
        internal IServiceProvider _provider = null;

        public ISuggestedActionsSource CreateSuggestedActionsSource(
            ITextView textView,
            ITextBuffer textBuffer
        ) {
            if (textView == null && textBuffer == null) {
                return null;
            }
            return new PythonSuggestedActionsSource(_provider, textView, textBuffer);
        }
    }
#endif
}
