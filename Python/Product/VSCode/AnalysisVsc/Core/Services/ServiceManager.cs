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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.DsTools.Core.Diagnostics;
using Microsoft.DsTools.Core.Disposables;
using static System.FormattableString;

namespace Microsoft.DsTools.Core.Services {
    public class ServiceManager : IServiceManager {
        private readonly DisposeToken _disposeToken = DisposeToken.Create<ServiceManager>();
        private readonly ConcurrentDictionary<Type, object> _s = new ConcurrentDictionary<Type, object>();

        /// <summary>
        /// Add service to the service manager container
        /// </summary>
        /// <param name="service">Service instance</param>
        /// <param name="type">
        /// Optional type to register the instance for. In Visual Studio
        /// some global services are registered as 'SVsService` while
        /// actual interface type is IVsService.
        /// </param>
        public virtual IServiceManager AddService(object service, Type type = null) {
            _disposeToken.ThrowIfDisposed();

            type = type ?? service.GetType();
            Check.ArgumentNull(nameof(service), service);
            Check.InvalidOperation(() => _s.GetOrAdd(type, service) == service, 
                Invariant($"Another instance of service of type {type} already added"));
            return this;
        }

        /// <summary>
        /// Adds on-demand created service
        /// </summary>
        /// <param name="factory">Service factory</param>
        public virtual IServiceManager AddService<T>(Func<IServiceContainer, T> factory) where T : class {
            _disposeToken.ThrowIfDisposed();

            var lazy = new Lazy<object>(() => factory(this));
            Check.InvalidOperation(() => _s.TryAdd(typeof(T), lazy), $"Service of type {typeof(T)} already exists");
            return this;
        }

        /// <summary>
        /// Retrieves service from the container
        /// </summary>
        /// <typeparam name="T">Service type</typeparam>
        /// <returns>Service instance or null if it doesn't exist</returns>
        public virtual T GetService<T>(Type type = null) where T : class {
            if (_disposeToken.IsDisposed) {
                // Do not throw. When editor text buffer is closed, the associated service manager
                // is disposed. However, some actions may still hold on the text buffer reference
                // and actually determine if buffer is closed by checking if editor document 
                // is still attached as a service.
                return null;
            }

            type = type ?? typeof(T);
            if (!_s.TryGetValue(type, out object value)) {
                value = _s.FirstOrDefault(kvp => type.GetTypeInfo().IsAssignableFrom(kvp.Key)).Value;
            }

            return (T)CheckDisposed(value as T ?? (value as Lazy<object>)?.Value);
        }

        public virtual void RemoveService(object service) => _s.TryRemove(service.GetType(), out object dummy);

        public virtual IEnumerable<Type> AllServices => _s.Keys.ToList();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private object CheckDisposed(object service) {
            if (_disposeToken.IsDisposed) {
                (service as IDisposable)?.Dispose();
                _disposeToken.ThrowIfDisposed();
            }
            return service;
        }

        #region IDisposable
        public void Dispose() {
            if (_disposeToken.TryMarkDisposed()) {
                foreach (var service in _s.Values) {
                    if (service is Lazy<object> lazy && lazy.IsValueCreated) {
                        (lazy.Value as IDisposable)?.Dispose();
                    } else {
                        (service as IDisposable)?.Dispose();
                    }
                }
            }
        }
        #endregion
    }
}