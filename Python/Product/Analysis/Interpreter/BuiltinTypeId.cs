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


namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// Well known built-in types that the analysis engine needs for doing interpretation.
    /// </summary>
    public enum BuiltinTypeId : int {
        Unknown,
        Object,
        Type,
        NoneType,

        Bool,
        Int,
        /// <summary>
        /// The long integer type.
        /// </summary>
        /// <remarks>
        /// Interpreters should map this value to Int if they only have one
        /// integer type.
        /// </remarks>
        Long,
        Float,
        Complex,

        Tuple,
        List,
        Dict,
        Set,
        FrozenSet,

        /// <summary>
        /// The default string type.
        /// </summary>
        /// <remarks>
        /// Interpreters should map this value to either Bytes or Unicode
        /// depending on the type of "abc"
        /// </remarks>
        Str,
        /// <summary>
        /// The non-Unicode string type.
        /// </summary>
        Bytes,
        /// <summary>
        /// The Unicode string type.
        /// </summary>
        Unicode,

        /// <summary>
        /// The iterator for the default string type.
        /// </summary>
        /// <remarks>
        /// Interpreters should map this value to either BytesIterator or
        /// UnicodeIterator depending on the type of iter("abc").
        /// </remarks>
        StrIterator,
        /// <summary>
        /// The iterator for the non-Unicode string type.
        /// </summary>
        BytesIterator,
        /// <summary>
        /// The iterator for the Unicode string type.
        /// </summary>
        UnicodeIterator,

        Module,
        Function,
        BuiltinMethodDescriptor,
        BuiltinFunction,
        Generator,

        Property,
        ClassMethod,
        StaticMethod,

        Ellipsis,

        TupleIterator,
        ListIterator,
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
        SetIterator,
        CallableIterator,
    }

    internal static class BuiltinTypeIdExtensions {
        /// <summary>
        /// Indicates whether an ID should be remapped by an interpreter.
        /// </summary>
        public static bool IsVirtualId(this BuiltinTypeId id) {
            return id == BuiltinTypeId.Str ||
                id == BuiltinTypeId.StrIterator ||
                (int)id > (int)LastTypeId;
        }

        public static BuiltinTypeId LastTypeId {
            get {
                return BuiltinTypeId.CallableIterator;
            }
        }
    }
}
