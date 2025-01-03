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

using System;
using System.Diagnostics;
using System.Linq.Expressions;
using Microsoft.PythonTools.Debugger.Concord.Proxies.Structs;
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies {

    /// <summary>
    ///  Represents a proxy for a typed memory location in a process being debugged. 
    /// </summary>
    internal interface IDataProxy : IValueStore {
        DkmProcess Process { get; }
        ulong Address { get; }
        long ObjectSize { get; }
    }

    internal interface IWritableDataProxy : IDataProxy {
        void Write(object value);
    }

    /// <summary>
    ///  Represents a proxy for a typed memory location in a process being debugged. 
    /// </summary>
    internal interface IDataProxy<T> : IDataProxy, IValueStore<T> {
    }

    internal interface IWritableDataProxy<T> : IDataProxy<T>, IWritableDataProxy {
        /// <summary>
        /// Replace the value in the memory location with the one provided.
        /// </summary>
        void Write(T value);
    }

    /// <summary>
    /// Provides various helper extension and static methods for <see cref="IDataProxy"/>.
    /// </summary>
    internal static class DataProxy {
        private static class FactoryBuilder<TProxy> where TProxy : IDataProxy {
            public delegate TProxy FactoryFunc(DkmProcess process, ulong address, bool polymorphic);

            public static readonly FactoryFunc Factory;

            static FactoryBuilder() {
                FactoryFunc nonPolymorphicFactory = (process, address, polymorphic) => {
                    Debug.Fail("IDebuggeeReference-derived type " + typeof(TProxy).Name + " does not have a (DkmProcess, ulong) constructor or cannot be instantiated.");
                    throw new NotSupportedException();
                };
                var type = typeof(TProxy);

                // Make sure we have a constructor that takes a DkmProcess and a ulong.  If we don't, we can't instantiate the type.
                if (!type.IsAbstract) { 
                    var ctor = type.GetConstructor(new[] { typeof(DkmProcess), typeof(ulong) });
                    if (ctor != null) {
                        var processParam = Expression.Parameter(typeof(DkmProcess));
                        var addressParam = Expression.Parameter(typeof(ulong));
                        var polymorphicParam = Expression.Parameter(typeof(bool));
                        nonPolymorphicFactory = Expression.Lambda<FactoryFunc>(
                            Expression.New(ctor, processParam, addressParam),
                            new[] { processParam, addressParam, polymorphicParam })
                            .Compile();
                    }
                }

                if (typeof(IPyObject).IsAssignableFrom(typeof(TProxy))) {
                    Factory = (process, address, polymorphic) => {
                        if (polymorphic) {
                            return (TProxy)(object)PyObject.FromAddress(process, address);
                        } else {
                            return nonPolymorphicFactory(process, address, polymorphic);
                        }
                    };
                } else {
                    Factory = nonPolymorphicFactory;
                }
            }
        }

        /// <summary>
        /// Create a new proxy of a given type. This method exists to facilitate generic programming, as a workaround for the lack
        /// of parametrized constructor constraint in CLR generics.
        /// </summary>
        public static TProxy Create<TProxy>(DkmProcess process, ulong address, bool polymorphic = true)
            where TProxy : IDataProxy {
            return FactoryBuilder<TProxy>.Factory(process, address, polymorphic);
        }

        /// <summary>
        /// Returns a proxy for an object that is shifted by <paramref name="elementOffset"/> elements (not bytes!) relative to the object represeted
        /// by the current proxy.
        /// </summary>
        /// <remarks>
        /// This is the equivalent of operator+ on pointers in C. Negative values are permitted.
        /// </remarks>
        /// <param name="elementOffset">Number of elements to shift by.</param>
        /// <returns></returns>
        public static TProxy GetAdjacentProxy<TProxy>(this TProxy r, long elementOffset)
            where TProxy : IDataProxy {
            return Create<TProxy>(r.Process, r.Address.OffsetBy(elementOffset * r.ObjectSize));
        }
    }
}
