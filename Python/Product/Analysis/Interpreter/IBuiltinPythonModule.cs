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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.


namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// Represents a built-in Python module.  The built-in module needs to respond to
    /// some extra requests for members by name which supports getting hidden members
    /// such as "NoneType" which logically live in the built-in module but don't actually
    /// exist there by name.
    /// 
    /// The full list of types which will be accessed through GetAnyMember but don't exist
    /// in the built-in module includes:
    ///     NoneType
    ///     generator
    ///     builtin_function
    ///     builtin_method_descriptor
    ///     function
    ///     ellipsis
    ///     
    /// These are the addition types in BuiltinTypeId which do not exist in __builtin__.
    /// </summary>
    public interface IBuiltinPythonModule : IPythonModule {
        IMember GetAnyMember(string name);
    }
}
