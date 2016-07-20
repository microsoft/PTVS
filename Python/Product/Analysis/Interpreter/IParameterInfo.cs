// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

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
