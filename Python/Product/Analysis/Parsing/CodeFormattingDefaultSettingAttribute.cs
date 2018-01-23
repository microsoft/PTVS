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

namespace Microsoft.PythonTools.Parsing {
    /// <summary>
    /// Provides the default value for the code formatting option.
    /// 
    /// This is the default value that is used by an IDE or other tool.  When
    /// used programmatically the code formatting engine defaults to not altering
    /// code at all.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    sealed class CodeFormattingDefaultValueAttribute : Attribute {
        private readonly object _defaultValue;

        internal CodeFormattingDefaultValueAttribute(object defaultValue) {
            _defaultValue = defaultValue;
        }

        public object DefaultValue {
            get {
                return _defaultValue;
            }
        }
    }
}
