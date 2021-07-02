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

using Microsoft.CookiecutterTools.Infrastructure;

namespace Microsoft.CookiecutterTools.Model
{
    class ContextItem
    {
        public ContextItem(string name, string selector, string defaultValue, string[] items = null)
        {
            Name = name;
            Selector = selector;
            DefaultValue = defaultValue;
            Values = items ?? new string[0];
            Visible = !name.StartsWithOrdinal("_");
        }

        public string Name { get; }
        public string Label { get; set; }
        public string Description { get; set; }
        public string Url { get; set; }
        public string Selector { get; set; }
        public string DefaultValue { get; }
        public string[] Values { get; }
        public bool Visible { get; set; }
        public string ValueSource { get; set; }
    }
}
