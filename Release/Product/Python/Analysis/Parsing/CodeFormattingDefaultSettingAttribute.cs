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
    /// Provides the default value for the code formatting option.
    /// 
    /// This is the default value that is used by an IDE or other tool.  When
    /// used programmatically the code formatting engine defaults to not altering
    /// code at all.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class CodeFormattingDefaultValueAttribute : Attribute {
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
