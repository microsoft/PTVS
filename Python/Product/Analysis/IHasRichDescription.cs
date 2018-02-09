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

using System.Collections.Generic;

namespace Microsoft.PythonTools.Analysis {
    public interface IHasRichDescription {
        /// <summary>
        /// Returns a sequence of Kind,Text pairs that make up the description.
        /// </summary>
        /// <returns></returns>
        IEnumerable<KeyValuePair<string, string>> GetRichDescription();
    }

    public static class WellKnownRichDescriptionKinds {
        public const string Name = "name";
        public const string Type = "type";
        public const string Misc = "misc";
        public const string Comma = "comma";
        public const string Parameter = "param";
        public const string EndOfDeclaration = "enddecl";
    }

    static class RichDescriptionExtensions {
        public static IEnumerable<KeyValuePair<string, string>> GetRichDescriptions(this IAnalysisSet set, string prefix = null, bool useLongDescription = false, string unionPrefix = null, string unionSuffix = null, bool alwaysUsePrefixSuffix = false, string defaultIfEmpty = null) {
            var items = new List<KeyValuePair<string, string>>();
            int itemCount = 0;
            foreach (var d in (useLongDescription ? set.GetDescriptions() : set.GetShortDescriptions())) {
                items.Add(new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Type, d));
                items.Add(new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Comma, ", "));
                itemCount += 1;
            }

            if (itemCount == 1) {
                items.RemoveAt(items.Count - 1);
            }

            if (alwaysUsePrefixSuffix || itemCount >= 2) {
                if (!string.IsNullOrEmpty(unionPrefix)) {
                    items.Insert(0, new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, unionPrefix));
                }
                if (!string.IsNullOrEmpty(unionSuffix)) {
                    items.Add(new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, unionSuffix));
                }
            }

            if (itemCount > 0 && !string.IsNullOrEmpty(prefix)) {
                items.Insert(0, new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, prefix));
            }

            if (items.Count == 0 && !string.IsNullOrEmpty(defaultIfEmpty)) {
                items.Add(new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, defaultIfEmpty));
            }

            return items;
        }
    }
}
