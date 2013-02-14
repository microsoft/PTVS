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
    /// Well known built-in types that the analysis engine needs for doing interpretation.
    /// </summary>
    public enum BuiltinTypeId {
        Unknown,
        Int,
        Long,
        Float,
        Complex,
        Dict,
        Bool,
        List,
        Tuple,
        Generator,
        Function,
        Set,
        Type,
        Object,
        /// <summary>
        /// The unicode string type
        /// </summary>
        Str,
        BuiltinMethodDescriptor,
        BuiltinFunction,
        NoneType,
        Ellipsis,
        /// <summary>
        /// The non-unicode string type
        /// </summary>
        Bytes,

        /// <summary>
        /// The type returned by dict.iterkeys (2.x) or dict.keys (3.x)
        /// Also the type returned by iter(dict())
        /// </summary>
        DictKeys,

        /// <summary>
        /// The type returned by dict.itervalues (2.x) or dict.values (3.x)
        /// </summary>
        DictValues,

        /// <summary>
        /// The type returned by dict.iteritems (2.x) or dict.items (3.x)
        /// </summary>
        DictItems,

        /// <summary>
        /// The type of a module
        /// </summary>
        Module,

        ListIterator,
        TupleIterator,
        SetIterator,
        
        /// <summary>
        /// StrIterator is the same as BytesIterator on 2.x but not 3.x.
        /// StrIterator is the same as for Unicode on both 2.x and 3.x.
        /// </summary>
        StrIterator,
        BytesIterator,

        CallableIterator
    }
}
