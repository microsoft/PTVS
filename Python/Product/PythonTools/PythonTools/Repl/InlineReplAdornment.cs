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

using System.ComponentModel.Composition;
using System.Windows;
#if DEV14_OR_LATER
using Microsoft.VisualStudio.InteractiveWindow;
#else
using Microsoft.VisualStudio.Repl;
#endif
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.PythonTools.Repl {
    [Export(typeof(IViewTaggerProvider))]
    [TagType(typeof(IntraTextAdornmentTag))]
#if DEV14_OR_LATER
    [ContentType(PredefinedInteractiveContentTypes.InteractiveContentTypeName)]
#else
    [ContentType(ReplConstants.ReplContentTypeName)]
#endif
    internal class InlineReplAdornmentProvider : IViewTaggerProvider {
        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag {
            if (buffer == null || textView == null || typeof(T) != typeof(IntraTextAdornmentTag)) {
                return null;
            }

            return (ITagger<T>)textView.Properties.GetOrCreateSingletonProperty<InlineReplAdornmentManager>(
                typeof(InlineReplAdornmentManager),
                () => new InlineReplAdornmentManager(textView)
                );
        }

        internal static InlineReplAdornmentManager GetManager(ITextView view) {
            InlineReplAdornmentManager result;
            if (!view.Properties.TryGetProperty<InlineReplAdornmentManager>(typeof(InlineReplAdornmentManager), out result)) {
                return null;
            }
            return result;
        }
    }
}
