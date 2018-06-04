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


using System;
using Microsoft.PythonTools.Parsing;

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

    public static class BuiltinTypeIdExtensions {
        /// <summary>
        /// Indicates whether an ID should be remapped by an interpreter.
        /// </summary>
        public static bool IsVirtualId(this BuiltinTypeId id) {
            return id == BuiltinTypeId.Str ||
                id == BuiltinTypeId.StrIterator ||
                (int)id > (int)LastTypeId;
        }

        public static BuiltinTypeId LastTypeId => BuiltinTypeId.CallableIterator;

        public static string GetModuleName(this BuiltinTypeId id, Version version) {
            return id.GetModuleName(version.Major == 3);
        }

        public static string GetModuleName(this BuiltinTypeId id, PythonLanguageVersion languageVersion) {
            return id.GetModuleName(languageVersion.IsNone() || languageVersion.Is3x());
        }

        private static string GetModuleName(this BuiltinTypeId id, bool is3x) {
            return is3x ? "builtins" : "__builtin__";
        }

        public static string GetTypeName(this BuiltinTypeId id, Version version) {
            return id.GetTypeName(version.Major == 3);
        }

        public static string GetTypeName(this BuiltinTypeId id, PythonLanguageVersion languageVersion) {
            return id.GetTypeName(languageVersion.IsNone() || languageVersion.Is3x());
        }

        private static string GetTypeName(this BuiltinTypeId id, bool is3x) {
            string name;
            switch (id) {
                case BuiltinTypeId.Bool: name = "bool"; break;
                case BuiltinTypeId.Complex: name = "complex"; break;
                case BuiltinTypeId.Dict: name = "dict"; break;
                case BuiltinTypeId.Float: name = "float"; break;
                case BuiltinTypeId.Int: name = "int"; break;
                case BuiltinTypeId.List: name = "list"; break;
                case BuiltinTypeId.Long: name = is3x ? "int" : "long"; break;
                case BuiltinTypeId.Object: name = "object"; break;
                case BuiltinTypeId.Set: name = "set"; break;
                case BuiltinTypeId.Str: name = "str"; break;
                case BuiltinTypeId.Unicode: name = is3x ? "str" : "unicode"; break;
                case BuiltinTypeId.Bytes: name = is3x ? "bytes" : "str"; break;
                case BuiltinTypeId.Tuple: name = "tuple"; break;
                case BuiltinTypeId.Type: name = "type"; break;

                case BuiltinTypeId.BuiltinFunction: name = "builtin_function"; break;
                case BuiltinTypeId.BuiltinMethodDescriptor: name = "builtin_method_descriptor"; break;
                case BuiltinTypeId.DictKeys: name = "dict_keys"; break;
                case BuiltinTypeId.DictValues: name = "dict_values"; break;
                case BuiltinTypeId.DictItems: name = "dict_items"; break;
                case BuiltinTypeId.Function: name = "function"; break;
                case BuiltinTypeId.Generator: name = "generator"; break;
                case BuiltinTypeId.NoneType: name = "NoneType"; break;
                case BuiltinTypeId.Ellipsis: name = "ellipsis"; break;
                case BuiltinTypeId.Module: name = "module_type"; break;
                case BuiltinTypeId.ListIterator: name = "list_iterator"; break;
                case BuiltinTypeId.TupleIterator: name = "tuple_iterator"; break;
                case BuiltinTypeId.SetIterator: name = "set_iterator"; break;
                case BuiltinTypeId.StrIterator: name = "str_iterator"; break;
                case BuiltinTypeId.UnicodeIterator: name = is3x ? "str_iterator" : "unicode_iterator"; break;
                case BuiltinTypeId.BytesIterator: name = is3x ? "bytes_iterator" : "str_iterator"; break;
                case BuiltinTypeId.CallableIterator: name = "callable_iterator"; break;

                case BuiltinTypeId.Property: name = "property"; break;
                case BuiltinTypeId.ClassMethod: name = "classmethod"; break;
                case BuiltinTypeId.StaticMethod: name = "staticmethod"; break;
                case BuiltinTypeId.FrozenSet: name = "frozenset"; break;

                case BuiltinTypeId.Unknown:
                default:
                    return null;
            }
            return name;
        }

        public static BuiltinTypeId GetTypeId(this string name) {
            switch (name) {
                case "int": return BuiltinTypeId.Int;
                case "long": return BuiltinTypeId.Long;
                case "bool": return BuiltinTypeId.Bool;
                case "float": return BuiltinTypeId.Float;
                case "str": return BuiltinTypeId.Str;
                case "complex": return BuiltinTypeId.Complex;
                case "dict": return BuiltinTypeId.Dict;
                case "list": return BuiltinTypeId.List;
                case "object": return BuiltinTypeId.Object;

                case "set": return BuiltinTypeId.Set;
                case "unicode": return BuiltinTypeId.Unicode;
                case "bytes": return BuiltinTypeId.Bytes;
                case "tuple": return BuiltinTypeId.Tuple;
                case "type": return BuiltinTypeId.Type;
                case "frozenset": return BuiltinTypeId.FrozenSet;

                case "builtin_function": return BuiltinTypeId.BuiltinFunction;
                case "builtin_method_descriptor": return BuiltinTypeId.BuiltinMethodDescriptor;
                case "dict_keys": return BuiltinTypeId.DictKeys;
                case "dict_values": return BuiltinTypeId.DictValues;
                case "dict_items": return BuiltinTypeId.DictItems;

                case "function": return BuiltinTypeId.Function;
                case "generator": return BuiltinTypeId.Generator;
                case "NoneType": return BuiltinTypeId.NoneType;
                case "ellipsis": return BuiltinTypeId.Ellipsis;
                case "module_type": return BuiltinTypeId.Module;

                case "list_iterator": return BuiltinTypeId.ListIterator;
                case "tuple_iterator": return BuiltinTypeId.TupleIterator;
                case "set_iterator": return BuiltinTypeId.SetIterator;
                case "str_iterator": return BuiltinTypeId.StrIterator;
                case "unicode_iterator": return BuiltinTypeId.UnicodeIterator;
                case "bytes_iterator": return BuiltinTypeId.BytesIterator;
                case "callable_iterator": return BuiltinTypeId.CallableIterator;

                case "property": return BuiltinTypeId.Property;
                case "classmethod": return BuiltinTypeId.ClassMethod;
                case "staticmethod": return BuiltinTypeId.StaticMethod;
            }
            return BuiltinTypeId.Unknown;
        }

    }
}
