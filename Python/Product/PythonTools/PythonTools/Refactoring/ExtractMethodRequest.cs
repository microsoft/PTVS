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

using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Refactoring {
    /// <summary>
    /// Encapsulates all of the possible knobs which can be flipped when extracting a method.
    /// </summary>
    class ExtractMethodRequest {
        private readonly string _name;
        private readonly string[] _parameters;
        private readonly ScopeStatement _targetScope;

        public ExtractMethodRequest(ScopeStatement targetScope, string name, string[] parameters) {
            _name = name;
            _parameters = parameters;
            _targetScope = targetScope;
        }

        /// <summary>
        /// The name of the new method which should be created
        /// </summary>
        public string Name {
            get {
                return _name;
            }
        }

        /// <summary>
        /// The variables which are consumed by the method but which should be passed in as parameters
        /// (versus closing over the variables)
        /// </summary>
        public string[] Parameters {
            get {
                return _parameters;
            }
        }

        /// <summary>
        /// The target scope to extract the method to.
        /// </summary>
        public ScopeStatement TargetScope {
            get {
                return _targetScope;
            }
        }
    }
}
