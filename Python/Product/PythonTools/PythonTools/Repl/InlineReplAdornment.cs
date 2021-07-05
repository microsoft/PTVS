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

namespace Microsoft.PythonTools.Repl {
    [Export(typeof(IViewTaggerProvider))]
    [TagType(typeof(IntraTextAdornmentTag))]
    [ContentType(PredefinedInteractiveContentTypes.InteractiveContentTypeName)]
    internal class InlineReplAdornmentProvider : IViewTaggerProvider {
        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag {
            if (buffer == null || textView == null || typeof(T) != typeof(IntraTextAdornmentTag)) {
                return null;
            }

            return (ITagger<T>)textView.Properties.GetOrCreateSingletonProperty(
                typeof(InlineReplAdornmentManager),
                () => new InlineReplAdornmentManager(textView)
            );
        }

        internal static InlineReplAdornmentManager GetManager(ITextView view) {
            InlineReplAdornmentManager result;
            if (!view.Properties.TryGetProperty(typeof(InlineReplAdornmentManager), out result)) {
                return null;
            }
            return result;
        }
    }
}
