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
    /// Represents information about an individual parameter.  Used for providing
    /// signature help.
    /// </summary>
    public interface IParameterInfo {
        /// <summary>
        /// The name of the parameter.
        /// </summary>
        string Name {
            get;
        }

        /// <summary>
        /// The types of the parameter.
        /// </summary>
        IList<IPythonType> ParameterTypes {
            get;
        }

        /// <summary>
        /// Documentation for the parameter.
        /// </summary>
        string Documentation {
            get;
        }

        /// <summary>
        /// True if the parameter is a *args parameter.
        /// </summary>
        bool IsParamArray {
            get;
        }

        /// <summary>
        /// True if the parameter is a **args parameter.
        /// </summary>
        bool IsKeywordDict {
            get;
        }

        /// <summary>
        /// Default value.  Returns String.Empty for optional parameters, or a string representation of the default value
        /// </summary>
        string DefaultValue {
            get;
        }
    }
}
