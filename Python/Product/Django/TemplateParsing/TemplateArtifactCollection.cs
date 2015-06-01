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

using System;
using System.Collections.Generic;
using System.Linq;

#if DEV14_OR_LATER
using Microsoft.Html.Core.Artifacts;
using Microsoft.Html.Core.Parser.Def;
using Microsoft.Web.Core.Text;
#else
using Microsoft.Html.Core;
using Microsoft.Web.Core;
#endif

namespace Microsoft.PythonTools.Django.TemplateParsing {
    /// <summary>
    /// A collection of <see cref="TemplateArtifact"/> objects for a given text input.
    /// </summary>
    internal class TemplateArtifactCollection : ArtifactCollection {
        private class SeparatorsInfo : ISensitiveFragmentSeparatorsInfo {
            public string LeftSeparator { get; set; }
            public string RightSeparator { get; set; }
        }

        private static readonly SeparatorsInfo[] _separatorInfos = new[] {
            new SeparatorsInfo { LeftSeparator = "{{", RightSeparator = "}}" },
            new SeparatorsInfo { LeftSeparator = "{%", RightSeparator = "%}" },
            new SeparatorsInfo { LeftSeparator = "{#", RightSeparator = "#}" },
        };

        public TemplateArtifactCollection()
            : base(new TemplateArtifactProcessor()) {
            LeftSeparator = RightSeparator = "";
        }

        public string LeftSeparator { get; private set; }

        public string RightSeparator { get; private set; }

        public override bool IsDestructiveChange(int start, int oldLength, int newLength, ITextProvider oldText, ITextProvider newText) {
            // Get list of items overlapping the change. Note that items haven't been
            // shifted yet and hence their positions match the old text snapshot.
           var itemsInRange = ItemsInRange(new TextRange(start, oldLength));

            // Is crosses item boundaries, it is destructive
           if (itemsInRange.Count > 1 || (itemsInRange.Count == 1 && (!itemsInRange[0].Contains(start) || !itemsInRange[0].Contains(start + oldLength)))) {
               return true;
           }

           foreach (var separatorInfo in _separatorInfos) {
               if (IsDestructiveChangeForSeparator(separatorInfo, itemsInRange, start, oldLength, newLength, oldText, newText)) {
                    return true;
                }
            }

            return false;
        }

        private bool IsDestructiveChangeForSeparator(
            ISensitiveFragmentSeparatorsInfo separatorInfo,
            IEnumerable<IArtifact> itemsInRange,
            int start,
            int oldLength,
            int newLength,
            ITextProvider oldText,
            ITextProvider newText
        ) {
            if (separatorInfo == null || (separatorInfo.LeftSeparator.Length == 0 && separatorInfo.RightSeparator.Length == 0)) {
                return false;
            }

            // Find out if one of the existing fragments contains position 
            // and if change damages fragment start or end separators

            string leftSeparator = separatorInfo.LeftSeparator;
            string rightSeparator = separatorInfo.RightSeparator;

            var firstTwoItems = itemsInRange.Take(2).ToList();
            var item = firstTwoItems.FirstOrDefault();

            // If no items are affected, change is unsafe only if new region contains left side separators.
            if (item == null) {
                // Simple optimization for whitespace insertion
                if (oldLength == 0 && string.IsNullOrWhiteSpace(newText.GetText(new TextRange(start, newLength)))) {
                    return false;
                }

                // Take into account that user could have deleted space between existing 
                // { and % or added { to the existing % so extend search range accordingly.
                int fragmentStart = Math.Max(0, start - leftSeparator.Length + 1);
                int fragmentEnd = Math.Min(newText.Length, start + newLength + leftSeparator.Length - 1);
                return newText.IndexOf(leftSeparator, TextRange.FromBounds(fragmentStart, fragmentEnd), true) >= 0;
            }

            // Is change completely inside an existing item?
            if (firstTwoItems.Count == 1 && (item.Contains(start) && item.Contains(start + oldLength))) {
                // Check that change does not affect item left separator
                if (TextRange.Contains(item.Start, leftSeparator.Length, start)) {
                    return true;
                }

                // Check that change does not affect item right separator. Note that we should not be using 
                // TextRange.Intersect since in case oldLength is zero (like when user is typing right before %} or }})
                // TextRange.Intersect will determine that zero-length range intersects with the right separator
                // which is incorrect. Typing at position 10 does not change separator at position 10. Similarly,
                // deleting text right before %} or }} does not make change destructive.

                var htmlToken = item as IHtmlToken;
                if (htmlToken == null || htmlToken.IsWellFormed) {
                    int rightSeparatorStart = item.End - rightSeparator.Length;
                    if (start + oldLength > rightSeparatorStart) {
                        if (TextRange.Intersect(rightSeparatorStart, rightSeparator.Length, start, oldLength)) {
                            return true;
                        }
                    }
                }

                // Touching left separator is destructive too, like when changing {{ to {{@
                // Check that change does not affect item left separator (whitespace is fine)
                if (item.Start + leftSeparator.Length == start) {
                    if (oldLength == 0) {
                        string text = newText.GetText(new TextRange(start, newLength));
                        if (String.IsNullOrWhiteSpace(text)) {
                            return false;
                        }
                    }

                    return true;
                }

                int fragmentStart = item.Start + separatorInfo.LeftSeparator.Length;
                fragmentStart = Math.Max(fragmentStart, start - separatorInfo.RightSeparator.Length + 1);
                int changeLength = newLength - oldLength;
                int fragmentEnd = item.End + changeLength;
                fragmentEnd = Math.Min(fragmentEnd, start + newLength + separatorInfo.RightSeparator.Length - 1);

                if (newText.IndexOf(separatorInfo.RightSeparator, TextRange.FromBounds(fragmentStart, fragmentEnd), true) >= 0) {
                    return true;
                }

                return false;
            }

            return true;
        }
    }
}

#endif