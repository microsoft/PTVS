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
using System.Collections.Generic;

namespace Microsoft.PythonTools.Interpreter.Default {
    class CPythonProperty : IBuiltinProperty {
        private readonly string _doc;
        private IPythonType _type;
        
        public CPythonProperty(PythonTypeDatabase typeDb, Dictionary<string, object> valueDict) {
            object value;
            if (valueDict.TryGetValue("doc", out value)) {
                _doc = value as string;
            }

            object type;
            valueDict.TryGetValue("type", out type);

            typeDb.LookupType(type, (typeValue) => _type = typeValue);
        }

        #region IBuiltinProperty Members

        public IPythonType Type {
            get { return _type; }
        }

        public bool IsStatic {
            get { return false; }
        }

        public string Documentation {
            get { return _doc; }
        }

        public string Description {
            get {
                return "property of type " + Type.Name;
            }
        }

        #endregion

        #region IMember Members

        public PythonMemberType MemberType {
            get { return PythonMemberType.Property; }
        }

        #endregion
    }
}
