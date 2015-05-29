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

namespace Microsoft.PythonTools.Analysis {
    /// <summary>
    /// Provides information about a value exported from a module.
    /// </summary>
    public struct ExportedMemberInfo {
        private readonly string _fromName, _name;
        
        internal ExportedMemberInfo(string fromName, string name) {
            _fromName = fromName;
            _name = name;
        }

        /// <summary>
        /// The name of the value being exported, fully qualified with the
        /// module/package name.
        /// </summary>
        public string Name {
            get {
                if (string.IsNullOrEmpty(_fromName)) {
                    return _name;
                } else {
                    return _fromName + "." + _name;
                }
            }
        }

        /// <summary>
        /// True if this was defined in the module or false if this was defined in another module
        /// but imported in the module that we're getting members from.
        /// </summary>
        [Obsolete("Only defined names are returned")]
        public bool IsDefinedInModule {
            get { return true; }
        }

        /// <summary>
        /// The name of the member or module that can be imported.
        /// </summary>
        public string ImportName {
            get {
                return _name;
            }
        }

        /// <summary>
        /// The name of the module it is imported from, if applicable.
        /// </summary>
        public string FromName {
            get {
                return _fromName;
            }
        }
    }
}
