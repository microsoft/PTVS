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
    /// Provides a category for a code formatting option.  Categories group various options
    /// based upon what syntactic elements they alter.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    sealed class CodeFormattingCategoryAttribute : Attribute {
        private readonly CodeFormattingCategory _category;

        internal CodeFormattingCategoryAttribute(CodeFormattingCategory category) {
            _category = category;
        }

        public CodeFormattingCategory Category {
            get {
                return _category;
            }
        }
    }

    enum CodeFormattingCategory {
        None,
        /// <summary>
        /// The category applies to new line spacing between source elements.
        /// </summary>
        NewLines,
        /// <summary>
        /// The category applies to the formatting of class definitions
        /// </summary>
        Classes,
        /// <summary>
        /// The category applies to the formatting of function definitions
        /// </summary>
        Functions,
        /// <summary>
        /// The category applies to the spacing within expressions.
        /// </summary>
        Spacing,
        /// <summary>
        /// The category applies to the spacing around operators.
        /// </summary>
        Operators,
        /// <summary>
        /// The category applies to the reformatting of various statements.
        /// </summary>
        Statements,
        /// <summary>
        /// The category applies to automatically applied wrapping.
        /// </summary>
        Wrapping
    }
}
