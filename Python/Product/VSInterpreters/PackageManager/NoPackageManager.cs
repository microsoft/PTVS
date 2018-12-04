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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Interpreter {
    class NoPackageManager : IPackageManager {
        public static readonly IPackageManager Instance = new NoPackageManager();

        private NoPackageManager() { }

        public bool IsReady => true;
        public IPythonInterpreterFactory Factory => null;

        public string ExtensionDisplayName => string.Empty;

        public string IndexDisplayName => string.Empty;

        public string SearchHelpText => throw new NotImplementedException();

        public string UniqueKey => "none";

        public int Priority => int.MaxValue;

        public event EventHandler InstalledFilesChanged { add { } remove { } }
        public event EventHandler InstalledPackagesChanged { add { } remove { } }

        public event EventHandler IsReadyChanged { add { } remove { } }

        public bool CanUninstall(PackageSpec package) {
            return true;
        }

        public Task<PackageSpec> GetInstalledPackageAsync(string name, CancellationToken cancellationToken) {
            return Task.FromResult(new PackageSpec());
        }

        public Task<IList<PackageSpec>> GetInstalledPackagesAsync(CancellationToken cancellationToken) {
            return Task.FromResult<IList<PackageSpec>>(Array.Empty<PackageSpec>());
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

        public Task<bool> ExecuteAsync(string arguments, IPackageManagerUI ui, CancellationToken cancellationToken) {
            throw new NotSupportedException();
        }

        public Task<bool> InstallAsync(PackageSpec package, IPackageManagerUI ui, CancellationToken cancellationToken) {
            throw new NotSupportedException();
        }

        public Task<bool> UninstallAsync(PackageSpec package, IPackageManagerUI ui, CancellationToken cancellationToken) {
            throw new NotSupportedException();
        }

        public Task<PackageSpec> GetInstalledPackageAsync(PackageSpec package, CancellationToken cancellationToken) {
            return Task.FromResult(new PackageSpec());
        }

        public Task<IList<PackageSpec>> GetInstallablePackagesAsync(CancellationToken cancellationToken) {
            return Task.FromResult<IList<PackageSpec>>(Array.Empty<PackageSpec>());
        }

        public Task<PackageSpec> GetInstallablePackageAsync(PackageSpec package, CancellationToken cancellationToken) {
            return Task.FromResult(new PackageSpec());
        }

        public string GetInstallCommandDisplayName(string searchQuery) {
            return string.Empty;
        }

        public void EnableNotifications() { }

        public void DisableNotifications() { }
    }
}
