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

using System;
using Microsoft.PythonTools.Analysis;

namespace Microsoft.PythonTools.Parsing {
    /// <summary>
    /// Provides binary examples for a code formatting option of how it affects the code
    /// when the option is turned on or off.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    class CodeFormattingExampleAttribute : Attribute {
        private readonly string _on, _off;

        internal CodeFormattingExampleAttribute(string doc) : this(doc, doc) { }

        internal CodeFormattingExampleAttribute(string on, string off) {
            _on = on;
            _off = off;
        }

        public virtual string On => _on;
        public virtual string Off => _off;
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    class CodeFormattingExampleResourceAttribute : CodeFormattingExampleAttribute {
        internal CodeFormattingExampleResourceAttribute(string docResourceId)
            : base(docResourceId) { }

        internal CodeFormattingExampleResourceAttribute(string onResourceId, string offResourceId)
            : base(onResourceId, offResourceId) { }

        public override string On => Resources.ResourceManager.GetString(base.On);
        public override string Off => Resources.ResourceManager.GetString(base.Off);
    }
}
