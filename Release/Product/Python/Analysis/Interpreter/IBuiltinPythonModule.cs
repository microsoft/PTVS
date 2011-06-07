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
