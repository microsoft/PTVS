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
    /// <summary>
    /// Implemented by objects that can manage packages. Initially, these
    /// object should not trigger any events or require disposal until the
    /// <see cref="EnableNotifications"/> function is called. Package
    /// managers are assumed to be very cheap to create.
    /// </summary>
    public interface IPackageManager {
        /// <summary>
        /// Returns the interpreter factory associated with this manager.
        /// </summary>
        IPythonInterpreterFactory Factory { get; }

        /// <summary>
        /// True if the package manager is ready for use. This may return false
        /// if a tool needs to be installed, for example.
        /// </summary>
        bool IsReady { get; }

        event EventHandler IsReadyChanged;

        /// <summary>
        /// Localized name to display for the extension in the environments window.
        /// Example: 'Packages (PyPI)'
        /// </summary>
        string ExtensionDisplayName { get; }

        /// <summary>
        /// Localized name of the index where packages are fetched from.
        /// Example: 'PyPI'
        /// </summary>
        string IndexDisplayName { get; }

        /// <summary>
        /// Localized watermark text for the search query text box.
        /// Example: 'Search PyPI and installed packages'
        /// </summary>
        string SearchHelpText { get; }

        /// <summary>
        /// Returns a description for the command that appears in search result.
        /// Example: 'pip install mypackage from PyPI'
        /// </summary>
        string GetInstallCommandDisplayName(string searchQuery);

        /// <summary>
        /// Returns if the specified package is allowed to be uninstalled.
        /// </summary>
        bool CanUninstall(PackageSpec package);

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
        /// <param name="arguments">
        /// The full command. The caller is responsible for quoting individual
        /// arguments according to the current platform's rules.
        /// </param>
        /// <exception cref="NotSupportedException">
        /// The command cannot be run.
        /// </exception>
        Task<bool> ExecuteAsync(string arguments, IPackageManagerUI ui, CancellationToken cancellationToken);

        /// <summary>
        /// Installs the specified package. Not all fields of the package spec
        /// need to be specified.
        /// </summary>
        Task<bool> InstallAsync(PackageSpec package, IPackageManagerUI ui, CancellationToken cancellationToken);

        /// <summary>
        /// Uninstalls the specified package. Not all fields of the package spec
        /// need to be specified.
        /// </summary>
        Task<bool> UninstallAsync(PackageSpec package, IPackageManagerUI ui, CancellationToken cancellationToken);

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

        /// <summary>
        /// Starts producing notifications from the package manager. After
        /// calling this function, the package manager instance should be
        /// retained until after <see cref="DisableNotifications"/> or
        /// <c>Dispose</c> (if implemented) is called.
        /// </summary>
        /// <remarks>
        /// Implementers should avoid doing anything that may requires
        /// disposal until this function is called.
        /// </remarks>
        void EnableNotifications();

        /// <summary>
        /// Stops producing notifications from the package manager.
        /// </summary>
        /// <remarks>
        /// If a package manager is disposable, it should be equivalent to
        /// calling this function.
        /// </remarks>
        void DisableNotifications();

        /// <summary>
        /// Temporarily suppresses notifications from the package manager.
        /// This will generally be more efficient than completely disabling
        /// notifications, and should produce any notifications that occur
        /// while suppressed after the returned object is disposed.
        /// </summary>
        IDisposable SuppressNotifications();

        /// <summary>
        /// Called to inform the package manager that its packages may have
        /// changed. Package managers have no obligation to use this information
        /// in any way.
        /// </summary>
        void NotifyPackagesChanged();

        /// <summary>
        /// Gets a uniquifying key. Only one package manager with a given key is
        /// made available, and it will be the one with the lowest Priority.
        /// </summary>
        string UniqueKey { get; }

        /// <summary>
        /// The priority for the package manager against all others. Lower values
        /// appear first.
        /// </summary>
        int Priority { get; }
    }
}
