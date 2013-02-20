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

using System;

namespace Microsoft.PythonTools.Parsing {
    /// <summary>
    /// Provides a category for a code formatting option.  Categories group various options
    /// based upon what syntactic elements they alter.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class CodeFormattingCategoryAttribute : Attribute {
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

    public enum CodeFormattingCategory {
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
