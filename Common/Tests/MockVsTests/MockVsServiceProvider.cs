// Visual Studio Shared Project
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
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace Microsoft.VisualStudioTools.MockVsTests {
    [Export(typeof(SVsServiceProvider))]
    [Export(typeof(MockVsServiceProvider))]
    public class MockVsServiceProvider : SVsServiceProvider, IOleServiceProvider, IServiceContainer, IDisposable {
        private readonly MockVs _vs;
        private readonly Dictionary<Type, object> _servicesByType = new Dictionary<Type, object>();
        private readonly Dictionary<Guid, object> _servicesByGuid = new Dictionary<Guid, object>();
        private readonly Dictionary<Type, ServiceCreatorCallback> _serviceCreatorByType = new Dictionary<Type, ServiceCreatorCallback>();
        private readonly List<IDisposable> _disposeAtEnd = new List<IDisposable>();
        private bool _isDisposed;

        [ImportingConstructor]
        public MockVsServiceProvider(MockVs mockVs) {
            _vs = mockVs;
            _servicesByType.Add(typeof(IOleServiceProvider), this);
        }

        public void AddService(Type type, object inst) {
            _servicesByType[type] = inst;
            _servicesByGuid[type.GUID] = inst;
        }

        public void AddService(Type type, object inst, bool promote) {
            AddService(type, inst);
        }

        public object GetService(Guid serviceType) {
            object res;
            if (_servicesByGuid.TryGetValue(serviceType, out res)) {
                return res;
            }
            Console.WriteLine("Unknown service: " + serviceType);
            throw new NotImplementedException();
        }

        public object GetService(Type serviceType) {
            object res;
            if (_servicesByType.TryGetValue(serviceType, out res)) {
                return res;
            }

            if (_servicesByGuid.TryGetValue(serviceType.GUID, out res)) {
                return res;
            }

            ServiceCreatorCallback creator;
            if (_serviceCreatorByType.TryGetValue(serviceType, out creator)) {
                _servicesByType[serviceType] = res = creator(this, serviceType);
                _serviceCreatorByType.Remove(serviceType);
                var disposable = res as IDisposable;
                if (disposable != null) {
                    _disposeAtEnd.Add(disposable);
                }
                return res;
            }

            Console.WriteLine("Unknown service: " + serviceType.FullName);
            return null;
        }

        public int QueryService(ref Guid guidService, ref Guid riid, out IntPtr ppvObject) {
            object res;
            if (_servicesByGuid.TryGetValue(guidService, out res)) {
                IntPtr punk = Marshal.GetIUnknownForObject(res);
                try {
                    return Marshal.QueryInterface(punk, ref riid, out ppvObject);
                } finally {
                    Marshal.Release(punk);
                }
            }

            Console.WriteLine("Unknown interface: {0}", guidService);
            ppvObject = IntPtr.Zero;
            return VSConstants.E_NOINTERFACE;
        }

        public void AddService(Type serviceType, ServiceCreatorCallback callback, bool promote) {
            _serviceCreatorByType[serviceType] = callback;
        }

        public void AddService(Type serviceType, ServiceCreatorCallback callback) {
            _serviceCreatorByType[serviceType] = callback;
        }

        public void RemoveService(Type serviceType, bool promote) {
            _servicesByType.Remove(serviceType);
            _servicesByGuid.Remove(serviceType.GUID);
            _serviceCreatorByType.Remove(serviceType);
        }

        public void RemoveService(Type serviceType) {
            _servicesByType.Remove(serviceType);
            _serviceCreatorByType.Remove(serviceType);
        }

        public void Dispose() {
            if (!_isDisposed) {
                _isDisposed = true;
                foreach (var d in _disposeAtEnd) {
                    d.Dispose();
                }
                _disposeAtEnd.Clear();
                _servicesByType.Clear();
                _servicesByGuid.Clear();
                _serviceCreatorByType.Clear();
            }
        }
    }
}
