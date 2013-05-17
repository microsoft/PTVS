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
    /// Indicates the type of a variable result lookup.
    /// 
    /// Types can indicate a physical 1st class object (function, module, etc...) or they can
    /// represent a logic piece of storage (field)
    /// </summary>
    public enum PythonMemberType {
        Unknown,
        /// <summary>
        /// The result is a user defined or built-in class.
        /// </summary>
        Class,
        /// <summary>
        /// An instance of a user defined or built-in class.
        /// </summary>
        Instance,
        /// <summary>
        /// The result is a delegate type.
        /// </summary>
        Delegate,
        /// <summary>
        /// The result is an instance of a delegate.
        /// </summary>
        DelegateInstance,
        /// <summary>
        /// The result is an enum type.
        /// </summary>
        Enum,
        /// <summary>
        /// The result is an enum instance.
        /// </summary>
        EnumInstance,
        /// <summary>
        /// An instance of a user defined or built-in function.
        /// </summary>
        Function,
        /// <summary>
        /// An instance of a user defined or built-in method.
        /// </summary>
        Method,
        /// <summary>
        /// An instance of a built-in or user defined module.
        /// </summary>
        Module,
        /// <summary>
        /// An instance of a namespace object that was imported from .NET.
        /// </summary>
        Namespace,

        /// <summary>
        /// A constant defined in source code.
        /// </summary>
        Constant,

        /// <summary>
        /// A .NET event object that is exposed to Python.
        /// </summary>
        Event,
        /// <summary>
        /// A .NET field object that is exposed to Python.
        /// </summary>
        Field,
        /// <summary>
        /// A .NET property object that is exposed to Python.
        /// </summary>
        Property,

        /// <summary>
        /// A merge of multiple types.
        /// </summary>
        Multiple,

        /// <summary>
        /// The member represents a keyword
        /// </summary>
        Keyword
    }
}
