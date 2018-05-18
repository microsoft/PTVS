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
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.PythonTools.Uwp.Project;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Uwp {
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    ///
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the 
    /// IVsPackage interface and uses the registration attributes defined in the framework to 
    /// register itself and its components with the shell.
    /// </summary>
    // This attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class is
    // a package.
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]

    // This attribute is needed to let the shell know that this package exposes some menus.
    [Guid(GuidList.guidUwpPkgString)]
    
    [ProvideObject(typeof(PythonUwpPropertyPage))]
    [ProvideObject(typeof(PythonUwpProject))]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionHasAppContainerProject_string, PackageAutoLoadFlags.BackgroundLoad)]
    [Description("Python - UWP support")] // TODO: Localization (this may not be needed)
    [ProvideProjectFactory(typeof(PythonUwpProjectFactory), null, null, null, null, ".\\NullPath", LanguageVsTemplate = "Python")]
    [InstalledProductRegistration("#110", "#112", AssemblyVersionInfo.Version, IconResourceID = 400)]
    public sealed class PythonUwpPackage : AsyncPackage {
        internal static PythonUwpPackage Instance;

        /// <summary>
        /// Default constructor of the package.
        /// Inside this method you can place any initialization code that does not require 
        /// any Visual Studio service because at this point the package object is created but 
        /// not sited yet inside Visual Studio environment. The place to do all the other 
        /// initialization is the Initialize method.
        /// </summary>
        public PythonUwpPackage() {
            Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering constructor for: {0}", this));
            Instance = this;
        }

        /////////////////////////////////////////////////////////////////////////////
        // Overridden Package Implementation
        #region Package Members

        /// <inheritdoc />
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress) {
            Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering InitializeAsync() of: {0}", ToString()));

            await base.InitializeAsync(cancellationToken, progress);
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            RegisterProjectFactory(new PythonUwpProjectFactory(this));
        }

        #endregion

        internal new object GetService(Type serviceType) {
            return base.GetService(serviceType);
        }

        public EnvDTE.DTE DTE {
            get {
                return (EnvDTE.DTE)GetService(typeof(EnvDTE.DTE));
            }
        }

        internal static PythonUwpProject GetProject(IServiceProvider serviceProvider, string filename) {
            IVsHierarchy hierarchy;
            IVsRunningDocumentTable rdt = serviceProvider.GetService(typeof(SVsRunningDocumentTable)) as IVsRunningDocumentTable;
            uint itemid;
            IntPtr docData = IntPtr.Zero;
            uint cookie;
            try {
                int hr = rdt.FindAndLockDocument((uint)_VSRDTFLAGS.RDT_ReadLock,
                    filename,
                    out hierarchy,
                    out itemid,
                    out docData,
                    out cookie);

                if (ErrorHandler.Succeeded(hr)) {
                    rdt.UnlockDocument((uint)_VSRDTFLAGS.RDT_ReadLock, cookie);
                }
                var res = hierarchy as PythonUwpProject;
                if (res != null) {
                    return res;
                }
                return null;
            } finally {
                if (docData != IntPtr.Zero) {
                    Marshal.Release(docData);
                }
            }
        }
    }
}
