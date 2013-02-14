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

using System.Diagnostics;
using System.Linq;
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Provides operations which can be performed in bulk over a set of namespaces which then result
    /// in a new set of namespaces as the result.
    /// </summary>
    internal static class NamespaceSetExtensions {
        /// <summary>
        /// Performs a GetMember operation for the given name and returns the types of variables which
        /// are associated with that name.
        /// </summary>
        public static INamespaceSet GetMember(this INamespaceSet self, Node node, AnalysisUnit unit, string name) {
            var res = NamespaceSet.Empty;
            // name can be empty if we have "foo."
            if (name != null && name.Length > 0) {
                foreach (var ns in self) {
                    res = res.Union(ns.GetMember(node, unit, name));
                }
            }
            return res;
        }

        /// <summary>
        /// Performs a SetMember operation for the given name and propagates the given values types
        /// for the provided member name.
        /// </summary>
        public static void SetMember(this INamespaceSet self, Node node, AnalysisUnit unit, string name, INamespaceSet value) {
            if (name != null && name.Length > 0) {
                foreach (var ns in self) {
                    ns.SetMember(node, unit, name, value);
                }
            }
        }

        /// <summary>
        /// Performs a delete index operation propagating the index types into the provided object.
        /// </summary>
        public static void DeleteMember(this INamespaceSet self, Node node, AnalysisUnit unit, string name) {
            if (name != null && name.Length > 0) {
                foreach (var ns in self) {
                    ns.DeleteMember(node, unit, name);
                }
            }
        }

        /// <summary>
        /// Performs a call operation propagating the argument types into any user defined functions
        /// or classes and returns the set of types which result from the call.
        /// </summary>
        public static INamespaceSet Call(this INamespaceSet self, Node node, AnalysisUnit unit, INamespaceSet[] args, NameExpression[] keywordArgNames) {
            var res = NamespaceSet.Empty;
            foreach (var ns in self) {
                var call = ns.Call(node, unit, args, keywordArgNames);
                Debug.Assert(call != null);

                res = res.Union(call);
            }

            return res;
        }

        /// <summary>
        /// Performs a get iterator operation propagating any iterator types into the value and returns 
        /// the associated types associated with the object.
        /// </summary>
        public static INamespaceSet GetIterator(this INamespaceSet self, Node node, AnalysisUnit unit) {
            var res = NamespaceSet.Empty;
            foreach (var ns in self) {
                res = res.Union(ns.GetIterator(node, unit));
            }

            return res;
        }

        /// <summary>
        /// Performs a get index operation propagating any index types into the value and returns 
        /// the associated types associated with the object.
        /// </summary>
        public static INamespaceSet GetIndex(this INamespaceSet self, Node node, AnalysisUnit unit, INamespaceSet index) {
            var res = NamespaceSet.Empty;
            foreach (var ns in self) {
                res = res.Union(ns.GetIndex(node, unit, index));
            }

            return res;
        }

        /// <summary>
        /// Performs a set index operation propagating the index types and value types into
        /// the provided object.
        /// </summary>
        public static void SetIndex(this INamespaceSet self, Node node, AnalysisUnit unit, INamespaceSet index, INamespaceSet value) {
            foreach (var ns in self) {
                ns.SetIndex(node, unit, index, value);
            }
        }

        /// <summary>
        /// Performs a delete index operation propagating the index types into the provided object.
        /// </summary>
        public static void DeleteIndex(this INamespaceSet self, Node node, AnalysisUnit unit, INamespaceSet index) {
        }

        /// <summary>
        /// Performs an augmented assignment propagating the value into the object.
        /// </summary>
        public static void AugmentAssign(this INamespaceSet self, AugmentedAssignStatement node, AnalysisUnit unit, INamespaceSet value) {
            foreach (var ns in self) {
                ns.AugmentAssign(node, unit, value);
            }
        }

        /// <summary>
        /// Returns the set of types which are accessible when code enumerates over the object
        /// in a for statement.
        /// </summary>
        public static INamespaceSet GetEnumeratorTypes(this INamespaceSet self, Node node, AnalysisUnit unit) {
            var res = NamespaceSet.Empty;
            foreach (var ns in self) {
                res = res.Union(ns.GetEnumeratorTypes(node, unit));
            }

            return res;
        }

        /// <summary>
        /// Performs a __get__ on the object.
        /// </summary>
        public static INamespaceSet GetDescriptor(this INamespaceSet self, Node node, Namespace instance, Namespace context, AnalysisUnit unit) {
            var res = NamespaceSet.Empty;
            foreach (var ns in self) {
                res = res.Union(ns.GetDescriptor(node, instance, context, unit));
            }

            return res;
        }

        /// <summary>
        /// Performs a __get__ on the object when accessed from a class instead of an instance.
        /// </summary>
        public static INamespaceSet GetStaticDescriptor(this INamespaceSet self, AnalysisUnit unit) {
            var res = NamespaceSet.Empty;
            foreach (var ns in self) {
                res = res.Union(ns.GetStaticDescriptor(unit));
            }

            return res;
        }

        public static INamespaceSet BinaryOperation(this INamespaceSet self, Node node, AnalysisUnit unit, PythonOperator operation, INamespaceSet rhs) {
            var res = NamespaceSet.Empty;
            foreach (var ns in self) {
                res = res.Union(ns.BinaryOperation(node, unit, operation, rhs));
            }

            return res;
        }

        public static INamespaceSet UnaryOperation(this INamespaceSet self, Node node, AnalysisUnit unit, PythonOperator operation) {
            var res = NamespaceSet.Empty;
            foreach (var ns in self) {
                res = res.Union(ns.UnaryOperation(node, unit, operation));
            }

            return res;
        }

        public static Namespace GetUnionType(this INamespaceSet types) {
            var union = NamespaceSet.CreateUnion(types, UnionComparer.Instances[0]);
            Namespace type = null;
            if (union.Count == 2) {
                type = union.FirstOrDefault(t => t.GetConstantValue() != null);
            }
            return type ?? union.FirstOrDefault();
        }

    }
}
