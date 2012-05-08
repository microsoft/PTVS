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

using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Flavor;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Django.Project {
    [Guid(DjangoProjectGuid)]
    public class DjangoProjectFactory : FlavoredProjectFactory {
        internal const string DjangoProjectGuid = "5F0BE9CA-D677-4A4D-8806-6076C0FAAD37";
        private DjangoPackage _package;

        public DjangoProjectFactory(DjangoPackage package) {
            _package = package;
        }

        #region IVsAggregatableProjectFactory

        /// <summary>
        /// Create an instance of our project. The initialization will be done later
        /// when VS call InitalizeForOuter on it.
        /// </summary>
        /// <param name="outerProject">This is only useful if someone else is subtyping us</param>
        /// <returns>An uninitialized instance of our project</returns>
        protected override object PreCreateForOuter(object outerProject) {
            // Note: to support being aggregated (flavored) ourself, we must use
            // CreateInstance (passing in the outer object) rather than new
            // Using the ILocalRegistry implementation means we can register our
            // CLSID under the VS registry hive rather then globaly (although this
            // approachpr still support creating globaly registered CLSID).

            ILocalRegistry localRegistry = (ILocalRegistry)_package.GetService(typeof(SLocalRegistry));

            // Create an instance of our project
            IntPtr newProjectIUnknown;
            Guid riid = VSConstants.IID_IUnknown;
            ErrorHandler.ThrowOnFailure(localRegistry.CreateInstance(typeof(DjangoProject).GUID, outerProject, ref riid, 0, out newProjectIUnknown));

            var newProject = (DjangoProject)Marshal.GetObjectForIUnknown(newProjectIUnknown);
            newProject._package = _package;

            return newProject;
        }

        #endregion

    }
}
