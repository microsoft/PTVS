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
using Microsoft.PythonTools.Analysis;

namespace Microsoft.PythonTools.Parsing {
    /// <summary>
    /// Provides the localized description of a code formatting option.
    /// 
    /// There is both a short description for use in lists, and a longer description
    /// which is available for tooltips or other UI elements.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class CodeFormattingDescriptionAttribute : Attribute {
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
