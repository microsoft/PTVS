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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Indicates the type of a variable result eval.
    /// 
    /// Types can indicate a physical 1st class object (function, module, etc...) or they can
    /// represent a logic piece of storage (field)
    /// </summary>
    public enum PythonMemberType {
        Unknown,
        /// <summary>
        /// Class definition.
        /// </summary>
        Class,
        /// <summary>
        /// An instance of a type.
        /// </summary>
        Instance,
        /// <summary>
        /// Function type information.
        /// </summary>
        Function,
        /// <summary>
        /// Method type information.
        /// </summary>
        Method,
        /// <summary>
        /// An instance of a module.
        /// </summary>
        Module,
        /// <summary>
        /// A class property definition.
        /// </summary>
        Property,
        /// <summary>
        /// A union of multiple types.
        /// </summary>
        Union,
        /// <summary>
        /// Member is a variable.
        /// </summary>
        Variable,
        /// <summary>
        /// Generic type.
        /// </summary>
        Generic
    }
}
