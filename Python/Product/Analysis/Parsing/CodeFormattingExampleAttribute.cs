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
    /// Provides binary examples for a code formatting option of how it affects the code
    /// when the option is turned on or off.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple=false)]
    public sealed class CodeFormattingExampleAttribute : Attribute {
        private readonly string _on, _off;

        internal CodeFormattingExampleAttribute(string doc) {
            _on = _off = doc;
        }

        internal CodeFormattingExampleAttribute(string on, string off) {
            _on = on;
            _off = off;
        }

        public string On {
            get {
                return _on;
            }
        }

        public string Off {
            get {
                return _off;
            }
        }
    }
}
