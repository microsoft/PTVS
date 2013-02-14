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


namespace Microsoft.PythonTools.Analysis {
    /// <summary>
    /// Provides information about a value exported from a module.
    /// </summary>
    public struct ExportedMemberInfo {
        private readonly string _name;
        private readonly bool _isDefinedInModule;

        internal ExportedMemberInfo(string name, bool isDefinedInModule) {
            _name = name;
            _isDefinedInModule = isDefinedInModule;
        }

        /// <summary>
        /// The name of the value being exported, fully qualified with the module/package name.
        /// </summary>
        public string Name {
            get {
                return _name;
            }
        }

        /// <summary>
        /// True if this was defined in the module or false if this was defined in another module
        /// but imported in the module that we're getting members from.
        /// </summary>
        public bool IsDefinedInModule {
            get {
                return _isDefinedInModule;
            }
        }
    }
}
