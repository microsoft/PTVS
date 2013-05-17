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
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis {
    /// <summary>
    /// Represents the result from an analysis lookup.
    /// </summary>
    public interface IAnalysisValue {
        /// <summary>
        /// Gets a description of the variable result.
        /// </summary>
        string Description {
            get;
        }

        /// <summary>
        /// Gets a short description of the variable result.
        /// </summary>
        string ShortDescription {
            get;
        }

        /// <summary>
        /// Returns the location of where the variable is defined.
        /// </summary>
        LocationInfo Location {
            get;
        }

        IEnumerable<LocationInfo> References {
            get;
        }

        /// <summary>
        /// Gets the type of variable result.
        /// </summary>
        PythonMemberType ResultType {
            get;
        }

        /// <summary>
        /// Gets the concrete type used by IronPython or null if it does not have a concrete type.
        /// </summary>
        IPythonType PythonType {
            get;
        }
    }
}
