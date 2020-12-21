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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Runtime.InteropServices;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace Microsoft.VisualStudioTools.MockVsTests
{
    [Export(typeof(SVsServiceProvider))]
    [Export(typeof(MockVsServiceProvider))]
    public class MockVsServiceProvider : SVsServiceProvider, IOleServiceProvider, IServiceContainer, IDisposable
    {
        private readonly MockVs _vs;
        private readonly Dictionary<Guid, object> _servicesByGuid = new Dictionary<Guid, object>();
        private readonly Dictionary<Guid, ServiceCreatorCallback> _serviceCreatorByGuid = new Dictionary<Guid, ServiceCreatorCallback>();
        private readonly List<IDisposable> _disposeAtEnd = new List<IDisposable>();
        private bool _isDisposed;

        [ImportingConstructor]
        public MockVsServiceProvider(MockVs mockVs)
        {
            _vs = mockVs;
            _servicesByGuid.Add(typeof(IOleServiceProvider).GUID, this);
        }

        public void AddService(Type type, object inst)
        {
            _servicesByGuid[type.GUID] = inst;
        }

        public void AddService(Type type, object inst, bool promote)
        {
            AddService(type, inst);
        }

        private object GetService(Guid serviceType, string typeName)
        {
            object res;
            if (_servicesByGuid.TryGetValue(serviceType, out res))
            {
                return res;
            }

            ServiceCreatorCallback creator;
            if (_serviceCreatorByGuid.TryGetValue(serviceType, out creator))
            {
                _servicesByGuid[serviceType] = res = creator(this, Type.GetTypeFromCLSID(serviceType));
                _serviceCreatorByGuid.Remove(serviceType);
                var disposable = res as IDisposable;
                if (disposable != null)
                {
                    _disposeAtEnd.Add(disposable);
                }
                return res;
            }

            Console.WriteLine(
                "Unknown Service {0} ({1:B})",
                typeName ?? Type.GetTypeFromCLSID(serviceType, false)?.FullName ?? "(unknown)",
                serviceType
            );
            Debug.WriteLine(
                "Unknown Service {0} ({1:B})",
                typeName ?? Type.GetTypeFromCLSID(serviceType, false)?.FullName ?? "(unknown)",
                serviceType
            );
            return null;
        }

        public object GetService(Guid serviceType)
        {
            return GetService(serviceType, null);
        }

        public object GetService(Type serviceType)
        {
            return GetService(serviceType.GUID, serviceType.FullName);
        }

        public int QueryService(ref Guid guidService, ref Guid riid, out IntPtr ppvObject)
        {
            object res;
            if ((res = GetService(guidService)) != null)
            {
                IntPtr punk = Marshal.GetIUnknownForObject(res);
                try
                {
                    return Marshal.QueryInterface(punk, ref riid, out ppvObject);
                }
                finally
                {
                    Marshal.Release(punk);
                }
            }

            Console.WriteLine("Unknown interface: {0}", guidService);
            ppvObject = IntPtr.Zero;
            return VSConstants.E_NOINTERFACE;
        }

        public void AddService(Type serviceType, ServiceCreatorCallback callback, bool promote)
        {
            _serviceCreatorByGuid[serviceType.GUID] = callback;
        }

        public void AddService(Type serviceType, ServiceCreatorCallback callback)
        {
            _serviceCreatorByGuid[serviceType.GUID] = callback;
        }

        public void RemoveService(Type serviceType, bool promote)
        {
            _servicesByGuid.Remove(serviceType.GUID);
            _serviceCreatorByGuid.Remove(serviceType.GUID);
        }

        public void RemoveService(Type serviceType)
        {
            _servicesByGuid.Remove(serviceType.GUID);
            _serviceCreatorByGuid.Remove(serviceType.GUID);
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                foreach (var d in _disposeAtEnd)
                {
                    d.Dispose();
                }
                _disposeAtEnd.Clear();
                _servicesByGuid.Clear();
                _serviceCreatorByGuid.Clear();
            }
        }
    }
}
