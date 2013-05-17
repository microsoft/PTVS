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


namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// Represents a built-in property which has a getter/setter.  
    /// </summary>
    public interface IBuiltinProperty : IMember {
        /// <summary>
        /// The type of the value the property gets/sets.
        /// </summary>
        IPythonType Type {
            get;
        }

        /// <summary>
        /// True if the property is static (declared on the class) not the instance.
        /// </summary>
        bool IsStatic {
            get;
        }

        /// <summary>
        /// Documentation for the property.
        /// </summary>
        string Documentation {
            get;
        }

        /// <summary>
        /// A user readable description of the property.
        /// </summary>
        string Description {
            get;
        }
    }
}
