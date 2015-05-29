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

#if DEV14_OR_LATER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Repl {
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    sealed class InteractiveWindowRoleAttribute : Attribute {
        private readonly string _name;

        public InteractiveWindowRoleAttribute(string name) {
            if (name.Contains(","))
                throw new ArgumentException("ReplRoleAttribute name cannot contain any commas. Apply multiple attributes if you want to support multiple roles.", "name");

            _name = name;
        }

        public string Name {
            get { return _name; }
        }
    }
}
#endif