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
    /// Provides the localized description of a code formatting option.
    /// 
    /// There is both a short description for use in lists, and a longer description
    /// which is available for tooltips or other UI elements.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    sealed class CodeFormattingDescriptionAttribute : Attribute {
        private readonly string _short, _long;

        internal CodeFormattingDescriptionAttribute(string shortDescriptionResourceId, string longDescriptionResourceId) {
            _short = shortDescriptionResourceId;
            _long = longDescriptionResourceId;
        }

        public string ShortDescription {
            get {
                return Resources.ResourceManager.GetString(_short);
            }
        }

        public string LongDescription {
            get {
                return Resources.ResourceManager.GetString(_long);
            }
        }
    }
}
