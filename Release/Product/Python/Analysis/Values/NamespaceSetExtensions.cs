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
using System.Diagnostics;
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
        public static ISet<Namespace> GetMember(this ISet<Namespace> self, Node node, AnalysisUnit unit, string name) {
            ISet<Namespace> res = null;
            // name can be empty if we have "foo."
            if (name != null && name.Length > 0) {
                bool madeSet = false;
                foreach (var ns in self) {
                    ISet<Namespace> got = ns.GetMember(node, unit, name);
                    if (res == null) {
                        res = got;
                        continue;
                    } else if (!madeSet) {
                        res = new HashSet<Namespace>(res);
                        madeSet = true;
                    }
                    res.UnionWith(got);
                }
            }
            return res ?? EmptySet<Namespace>.Instance;
        }

        /// <summary>
        /// Performs a SetMember operation for the given name and propagates the given values types
        /// for the provided member name.
        /// </summary>
        public static void SetMember(this ISet<Namespace> self, Node node, AnalysisUnit unit, string name, ISet<Namespace> value) {
            if (name != null && name.Length > 0) {
                foreach (var ns in self) {
                    ns.SetMember(node, unit, name, value);
                }
            }
        }

        /// <summary>
        /// Performs a delete index operation propagating the index types into the provided object.
        /// </summary>
        public static void DeleteMember(this ISet<Namespace> self, Node node, AnalysisUnit unit, string name) {
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
        public static ISet<Namespace> Call(this ISet<Namespace> self, Node node, AnalysisUnit unit, ISet<Namespace>[] args, NameExpression[] keywordArgNames) {
            ISet<Namespace> res = EmptySet<Namespace>.Instance;
            bool madeSet = false;
            foreach (var ns in self) {
                var call = ns.Call(node, unit, args, keywordArgNames);
                Debug.Assert(call != null);

                res = res.Union(call, ref madeSet);
            }

            return res;
        }

        /// <summary>
        /// Performs a get index operation propagating any index types into the value and returns 
        /// the associated types associated with the object.
        /// </summary>
        public static ISet<Namespace> GetIndex(this ISet<Namespace> self, Node node, AnalysisUnit unit, ISet<Namespace> index) {
            ISet<Namespace> res = null;
            bool madeSet = false;
            foreach (var ns in self) {
                ISet<Namespace> got = ns.GetIndex(node, unit, index);
                if (res == null) {
                    res = got;
                    continue;
                } else if (!madeSet) {
                    res = new HashSet<Namespace>(res);
                    madeSet = true;
                }
                res.UnionWith(got);
            }

            return res ?? EmptySet<Namespace>.Instance;
        }

        /// <summary>
        /// Performs a set index operation propagating the index types and value types into
        /// the provided object.
        /// </summary>
        public static void SetIndex(this ISet<Namespace> self, Node node, AnalysisUnit unit, ISet<Namespace> index, ISet<Namespace> value) {
            foreach (var ns in self) {
                ns.SetIndex(node, unit, index, value);
            }
        }

        /// <summary>
        /// Performs a delete index operation propagating the index types into the provided object.
        /// </summary>
        public static void DeleteIndex(this ISet<Namespace> self, Node node, AnalysisUnit analysisState, ISet<Namespace> index) {
        }

        /// <summary>
        /// Performs an augmented assignment propagating the value into the object.
        /// </summary>
        public static void AugmentAssign(this ISet<Namespace> self, AugmentedAssignStatement node, AnalysisUnit unit, ISet<Namespace> value) {
            foreach (var ns in self) {
                ns.AugmentAssign(node, unit, value);
            }
        }

        /// <summary>
        /// Returns the set of types which are accessible when code enumerates over the object
        /// in a for statement.
        /// </summary>
        public static ISet<Namespace> GetEnumeratorTypes(this ISet<Namespace> self, Node node, AnalysisUnit unit) {
            ISet<Namespace> res = null;
            bool madeSet = false;
            foreach (var ns in self) {
                ISet<Namespace> got = ns.GetEnumeratorTypes(node, unit);
                if (res == null) {
                    res = got;
                    continue;
                } else if (!madeSet) {
                    res = new HashSet<Namespace>(res);
                    madeSet = true;
                }
                res.UnionWith(got);
            }

            return res ?? EmptySet<Namespace>.Instance;
        }

        /// <summary>
        /// Performs a __get__ on the object.
        /// </summary>
        public static ISet<Namespace> GetDescriptor(this ISet<Namespace> self, Node node, Namespace instance, Namespace context, AnalysisUnit unit) {
            ISet<Namespace> res = null;
            bool madeSet = false;
            foreach (var ns in self) {
                ISet<Namespace> got = ns.GetDescriptor(node, instance, context, unit);
                if (res == null) {
                    res = got;
                    continue;
                } else if (!madeSet) {
                    res = new HashSet<Namespace>(res);
                    madeSet = true;
                }
                res.UnionWith(got);
            }

            return res ?? EmptySet<Namespace>.Instance;
        }

        /// <summary>
        /// Performs a __get__ on the object when accessed from a class instead of an instance.
        /// </summary>
        public static ISet<Namespace> GetStaticDescriptor(this ISet<Namespace> self, AnalysisUnit unit) {
            ISet<Namespace> res = null;
            bool madeSet = false;
            foreach (var ns in self) {
                ISet<Namespace> got = ns.GetStaticDescriptor(unit);
                if (res == null) {
                    res = got;
                    continue;
                } else if (!madeSet) {
                    res = new HashSet<Namespace>(res);
                    madeSet = true;
                }
                res.UnionWith(got);
            }

            return res ?? EmptySet<Namespace>.Instance;
        }

        public static ISet<Namespace> BinaryOperation(this ISet<Namespace> self, Node node, AnalysisUnit unit, PythonOperator operation, ISet<Namespace> rhs) {            
            ISet<Namespace> res = null;
            bool madeSet = false;
            foreach (var ns in self) {
                ISet<Namespace> got = ns.BinaryOperation(node, unit, operation, rhs);
                if (res == null) {
                    res = got;
                    continue;
                } else if (!madeSet) {
                    res = new HashSet<Namespace>(res);
                    madeSet = true;
                }
                res.UnionWith(got);
            }

            return res ?? EmptySet<Namespace>.Instance;
        }

        public static ISet<Namespace> UnaryOperation(this ISet<Namespace> self, Node node, AnalysisUnit unit, PythonOperator operation) {
            ISet<Namespace> res = null;
            bool madeSet = false;
            foreach (var ns in self) {
                ISet<Namespace> got = ns.UnaryOperation(node, unit, operation);
                if (res == null) {
                    res = got;
                    continue;
                } else if (!madeSet) {
                    res = new HashSet<Namespace>(res);
                    madeSet = true;
                }
                res.UnionWith(got);
            }

            return res ?? EmptySet<Namespace>.Instance;
        }

        public static Namespace GetUnionType(this ISet<Namespace> types) {
            Namespace type = null;
            if (types.Count == 1) {
                type = System.Linq.Enumerable.First(types);
            } else if (types.Count > 0) {
                // simplify the types.
                var set = new HashSet<Namespace>(types, TypeUnion.UnionComparer);
                if (set.Count == 1) {
                    type = System.Linq.Enumerable.First(set);
                }
            }
            return type;
        }

    }
}
