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

using System.Collections.Generic;

namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// Represents an object which is a function.  Provides documentation for signature help.
    /// </summary>
    public interface IPythonFunction : IMember {
        string Name {
            get;
        }

        string Documentation {
            get;
        }

        bool IsBuiltin {
            get;            
        }
        
        /// <summary>
        /// False if binds instance when in a class, true if always static.
        /// </summary>
        bool IsStatic {
            get;
        }

        IList<IPythonFunctionOverload> Overloads {
            get;
        }

        IPythonType DeclaringType {
            get;
        }

        IPythonModule DeclaringModule {
            get;
        }
    }
}
