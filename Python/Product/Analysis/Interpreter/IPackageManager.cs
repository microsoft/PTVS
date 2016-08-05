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
    public interface IPackageManager {
        /// <summary>
        /// Called once to initialize the interpreter factory associated with
        /// a package manager instance.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// factory is null.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// SetInterpreterFactory has already been called.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// factory is not supported by the package manager.
        /// </exception>
        void SetInterpreterFactory(IPythonInterpreterFactory factory);

        /// <summary>
        /// True if the package manager is ready for use. This may return false
        /// if a tool needs to be installed, for example.
        /// </summary>
        bool IsReady { get; }

        event EventHandler IsReadyChanged;

        /// <summary>
        /// Prepares the package manager for use. This only needs to be called
        /// if <see cref="IsReady"/> is false. After successful completion,
        /// <see cref="IsReady"/> should be true.
        /// </summary>
        Task PrepareAsync(IPackageManagerUI ui, CancellationToken cancellationToken);

        /// <summary>
        /// Executes an arbitrary command using the package manager. This may
        /// not be supported by all implementations.
        /// </summary>
        /// <exception cref="NotSupportedException">
        /// The command cannot be run.
        /// </exception>
        Task ExecuteAsync(string arguments, IPackageManagerUI ui, CancellationToken cancellationToken);

        /// <summary>
        /// Installs the specified package. Not all fields
        /// </summary>
        Task InstallAsync(PackageSpec package, IPackageManagerUI ui, CancellationToken cancellationToken);

        Task<IList<PackageSpec>> GetInstalledPackagesAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Returns a new package spec with all information filled in from the
        /// current installation. If the parameter is not valid or the package
        /// is non currently installed, an invalid package spec will be
        /// returned.
        /// </summary>
        Task<PackageSpec> GetInstalledPackageAsync(PackageSpec package, CancellationToken cancellationToken);

        Task<IList<PackageSpec>> GetInstallablePackagesAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Returns a new package spec with all information filled in from the
        /// installable package. If the parameter is not valid or the package
        /// is non currently installed, an invalid package spec will be
        /// returned.
        /// </summary>
        Task<PackageSpec> GetInstallablePackageAsync(PackageSpec package, CancellationToken cancellationToken);

        /// <summary>
        /// Raised when the result of calling
        /// <see cref="GetInstalledPackagesAsync"/> is known to have changed.
        /// </summary>
        event EventHandler InstalledPackagesChanged;

        /// <summary>
        /// Raised when the contents of any of the watched directories have
        /// changed. This event will not be raised more than once per second.
        /// </summary>
        event EventHandler InstalledFilesChanged;

        IDisposable SuppressNotifications();

        /// <summary>
        /// Called to inform the package manager that its packages may have
        /// changed. Package managers have no obligation to use this information
        /// in any way.
        /// </summary>
        void NotifyPackagesChanged();
    }
}
