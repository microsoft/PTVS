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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Interpreter {
    class NoPackageManager : IPackageManager {
        public bool IsReady => true;

        public event EventHandler InstalledFilesChanged { add { } remove { } }
        public event EventHandler InstalledPackagesChanged { add { } remove { } }

        public Task<PackageSpec> GetInstalledPackageAsync(string name, CancellationToken cancellationToken) {
            return Task.FromResult(PackageSpec.Empty);
        }

        public Task<IList<PackageSpec>> GetInstalledPackagesAsync(CancellationToken cancellationToken) {
            return Task.FromResult<IList<PackageSpec>>(new List<PackageSpec>());
        }

        public void NotifyPackagesChanged() { }

        public sealed class NoSuppression : IDisposable {
            public static readonly IDisposable Instance = new NoSuppression();
            private NoSuppression() { }
            void IDisposable.Dispose() { }
        }

        public IDisposable SuppressNotifications() => NoSuppression.Instance;

        public void SetInterpreterFactory(IPythonInterpreterFactory factory) { }

        public Task PrepareAsync(IPackageManagerUI ui, CancellationToken cancellationToken) {
            return Task.FromResult<object>(null);
        }
    }
}
