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
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudioTools.Project {
    //[DefaultRegistryRoot("Software\\Microsoft\\VisualStudio\\9.0Exp")]
    //[PackageRegistration(UseManagedResourcesOnly = true)]
    public abstract class CommonProjectPackage : ProjectPackage, IVsInstalledProduct, IOleComponent {
        private IOleComponentManager _compMgr;
        private uint _componentID;

        public abstract ProjectFactory CreateProjectFactory();
        public abstract CommonEditorFactory CreateEditorFactory();
        public virtual CommonEditorFactory CreateEditorFactoryPromptForEncoding() {
            return null;
        }
        /// <summary>
        /// This method is called to get the icon that will be displayed in the
        /// Help About dialog when this package is selected.
        /// </summary>
        /// <returns>The resource id corresponding to the icon to display on the Help About dialog</returns>
        public abstract uint GetIconIdForAboutBox();
        /// <summary>
        /// This method is called during Devenv /Setup to get the bitmap to
        /// display on the splash screen for this package.
        /// </summary>
        /// <returns>The resource id corresponding to the bitmap to display on the splash screen</returns>
        public abstract uint GetIconIdForSplashScreen();
        /// <summary>
        /// This methods provides the product official name, it will be
        /// displayed in the help about dialog.
        /// </summary>
        public abstract string GetProductName();
        /// <summary>
        /// This methods provides the product description, it will be
        /// displayed in the help about dialog.
        /// </summary>
        public abstract string GetProductDescription();
        /// <summary>
        /// This methods provides the product version, it will be
        /// displayed in the help about dialog.
        /// </summary>
        public abstract string GetProductVersion();

        protected override void Initialize() {
            UIThread.EnsureService(this);

            base.Initialize();
            this.RegisterProjectFactory(CreateProjectFactory());
            var editFactory = CreateEditorFactory();
            if (editFactory != null) {
                this.RegisterEditorFactory(editFactory);
            }
            var encodingEditorFactory = CreateEditorFactoryPromptForEncoding();
            if (encodingEditorFactory != null) {
                RegisterEditorFactory(encodingEditorFactory);
            }
            var componentManager = _compMgr = (IOleComponentManager)GetService(typeof(SOleComponentManager));
            OLECRINFO[] crinfo = new OLECRINFO[1];
            crinfo[0].cbSize = (uint)Marshal.SizeOf(typeof(OLECRINFO));
            crinfo[0].grfcrf = (uint)_OLECRF.olecrfNeedIdleTime;
            crinfo[0].grfcadvf = (uint)_OLECADVF.olecadvfModal | (uint)_OLECADVF.olecadvfRedrawOff | (uint)_OLECADVF.olecadvfWarningsOff;
            crinfo[0].uIdleTimeInterval = 0;
            ErrorHandler.ThrowOnFailure(componentManager.FRegisterComponent(this, crinfo, out _componentID));
        }

        protected override void Dispose(bool disposing) {
            try {
                if (_componentID != 0) {
                    IOleComponentManager mgr = GetService(typeof(SOleComponentManager)) as IOleComponentManager;
                    if (mgr != null) {
                        mgr.FRevokeComponent(_componentID);
                    }
                    _componentID = 0;
                }
            } finally {
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// This method loads a localized string based on the specified resource.
        /// </summary>
        /// <param name="resourceName">Resource to load</param>
        /// <returns>String loaded for the specified resource</returns>
        public string GetResourceString(string resourceName) {
            string resourceValue;
            IVsResourceManager resourceManager = (IVsResourceManager)GetService(typeof(SVsResourceManager));
            if (resourceManager == null) {
                throw new InvalidOperationException("Could not get SVsResourceManager service. Make sure the package is Sited before calling this method");
            }
            Guid packageGuid = this.GetType().GUID;
            int hr = resourceManager.LoadResourceString(ref packageGuid, -1, resourceName, out resourceValue);
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(hr);
            return resourceValue;
        }

        #region IVsInstalledProduct Members

        /// <summary>
        /// This method is called during Devenv /Setup to get the bitmap to
        /// display on the splash screen for this package.
        /// </summary>
        /// <param name="pIdBmp">The resource id corresponding to the bitmap to display on the splash screen</param>
        /// <returns>HRESULT, indicating success or failure</returns>
        public int IdBmpSplash(out uint pIdBmp) {
            pIdBmp = GetIconIdForSplashScreen();
            return VSConstants.S_OK;
        }

        /// <summary>
        /// This method is called to get the icon that will be displayed in the
        /// Help About dialog when this package is selected.
        /// </summary>
        /// <param name="pIdIco">The resource id corresponding to the icon to display on the Help About dialog</param>
        /// <returns>HRESULT, indicating success or failure</returns>
        public int IdIcoLogoForAboutbox(out uint pIdIco) {
            pIdIco = GetIconIdForAboutBox();
            return VSConstants.S_OK;
        }

        /// <summary>
        /// This methods provides the product official name, it will be
        /// displayed in the help about dialog.
        /// </summary>
        /// <param name="pbstrName">Out parameter to which to assign the product name</param>
        /// <returns>HRESULT, indicating success or failure</returns>
        public int OfficialName(out string pbstrName) {
            pbstrName = GetProductName();
            return VSConstants.S_OK;
        }

        /// <summary>
        /// This methods provides the product description, it will be
        /// displayed in the help about dialog.
        /// </summary>
        /// <param name="pbstrProductDetails">Out parameter to which to assign the description of the package</param>
        /// <returns>HRESULT, indicating success or failure</returns>
        public int ProductDetails(out string pbstrProductDetails) {
            pbstrProductDetails = GetProductDescription();
            return VSConstants.S_OK;
        }

        /// <summary>
        /// This methods provides the product version, it will be
        /// displayed in the help about dialog.
        /// </summary>
        /// <param name="pbstrPID">Out parameter to which to assign the version number</param>
        /// <returns>HRESULT, indicating success or failure</returns>
        public int ProductID(out string pbstrPID) {
            pbstrPID = GetProductVersion();
            return VSConstants.S_OK;
        }

        #endregion

        #region IOleComponent Members

        public int FContinueMessageLoop(uint uReason, IntPtr pvLoopData, MSG[] pMsgPeeked) {
            return 1;
        }

        public int FDoIdle(uint grfidlef) {
            var onIdle = OnIdle;
            if (onIdle != null) {
                onIdle(this, new ComponentManagerEventArgs(_compMgr));
            }

            return 0;
        }

        internal event EventHandler<ComponentManagerEventArgs> OnIdle;

        public int FPreTranslateMessage(MSG[] pMsg) {
            return 0;
        }

        public int FQueryTerminate(int fPromptUser) {
            return 1;
        }

        public int FReserved1(uint dwReserved, uint message, IntPtr wParam, IntPtr lParam) {
            return 1;
        }

        public IntPtr HwndGetWindow(uint dwWhich, uint dwReserved) {
            return IntPtr.Zero;
        }

        public void OnActivationChange(IOleComponent pic, int fSameComponent, OLECRINFO[] pcrinfo, int fHostIsActivating, OLECHOSTINFO[] pchostinfo, uint dwReserved) {
        }

        public void OnAppActivate(int fActive, uint dwOtherThreadID) {
        }

        public void OnEnterState(uint uStateID, int fEnter) {
        }

        public void OnLoseActivation() {
        }

        public void Terminate() {
        }

        #endregion
    }
}
