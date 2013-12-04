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

using Microsoft.Html.Core;
using Microsoft.Web.Core;

namespace Microsoft.PythonTools.Django.TemplateParsing {
    /// <summary>
    /// A collection of <see cref="TemplateArtifact"/> objects for a given text input.
    /// </summary>
    internal class TemplateArtifactCollection : ArtifactCollection, ISensitiveFragmentSeparatorsInfo {
        private static readonly string[] _separatorPairs = new[] { "{{", "}}", "{%", "%}", "{#", "#}" };

        public TemplateArtifactCollection()
            : base(new TemplateArtifactProcessor()) {
            LeftSeparator = RightSeparator = "";
        }

        protected override ISensitiveFragmentSeparatorsInfo SeparatorInfo {
            get {
                return this;
            }
        }

        public string LeftSeparator { get; private set; }

        public string RightSeparator { get; private set; }

        public override bool IsDestructiveChange(int start, int oldLength, int newLength, ITextProvider oldText, ITextProvider newText) {
            // ArtifactCollection knows how to detect destructive changes, but it does so based on a single pair of separators that
            // it obtains from SeparatorsInfo. So we iterate over all possible separator pairs here and invoke the base implementation
            // until it detects the change as destructive, or we run out of separators to check.
            try {
                for (int i = 0; i < _separatorPairs.Length; i += 2) {
                    LeftSeparator = _separatorPairs[i];
                    RightSeparator = _separatorPairs[i + 1];
                    if (base.IsDestructiveChange(start, oldLength, newLength, oldText, newText)) {
                        return true;
                    }
                }
            } finally {
                LeftSeparator = RightSeparator = "";
            }

            // Due to a bug in SensitiveFragmentCollection, it does not properly treat { typed before an existing separator
            // like {{ or {% as destructive, so handle that case ourselves.
            var index = GetItemAtPosition(start);
            if (index >= 0) {
                string newStr = newText.GetText(new TextRange(start, newLength));
                if (newStr.EndsWith("{")) {
                    return true;
                }
            }

            return false;
        }
    }
}

#endif