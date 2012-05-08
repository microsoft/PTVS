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
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Microsoft.PythonTools.Django {
    /// <summary>
    /// Minimal language service.  Implemented directly rather than using the Managed Package
    /// Framework because we don't want to provide colorization services.  Instead we use the
    /// new Visual Studio 2010 APIs to provide these services.  But we still need this to
    /// provide a code window manager so that we can have a navigation bar (actually we don't, this
    /// should be switched over to using our TextViewCreationListener instead).
    /// </summary>
    [Guid("77291DD5-511B-45C8-BD20-62618C39D692")]
    internal sealed class DjangoLanguageInfo : IVsLanguageInfo, IVsLanguageDebugInfo {
        private readonly IServiceProvider _serviceProvider;
        private readonly IComponentModel _componentModel;

        public DjangoLanguageInfo(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
            _componentModel = serviceProvider.GetService(typeof(SComponentModel)) as IComponentModel;
        }

        public int GetCodeWindowManager(IVsCodeWindow pCodeWin, out IVsCodeWindowManager ppCodeWinMgr) {
            ppCodeWinMgr = null;
            return VSConstants.E_FAIL;
        }

        public int GetFileExtensions(out string pbstrExtensions) {
            // This is the same extension the language service was
            // registered as supporting.
            pbstrExtensions = ".djt";
            return VSConstants.S_OK;
        }


        public int GetLanguageName(out string bstrName) {
            // This is the same name the language service was registered with.
            bstrName = "Django Templates";
            return VSConstants.S_OK;
        }

        /// <summary>
        /// GetColorizer is not implemented because we implement colorization using the new managed APIs.
        /// </summary>
        public int GetColorizer(IVsTextLines pBuffer, out IVsColorizer ppColorizer) {
            ppColorizer = null;
            return VSConstants.E_FAIL;
        }

        public IServiceProvider ServiceProvider {
            get {
                return _serviceProvider;
            }
        }

        #region IVsLanguageDebugInfo Members

        public int GetLanguageID(IVsTextBuffer pBuffer, int iLine, int iCol, out Guid pguidLanguageID) {
            pguidLanguageID = DjangoPackage.DjangoTemplateLanguageGuid;
            return VSConstants.S_OK;
        }

        public int GetLocationOfName(string pszName, out string pbstrMkDoc, TextSpan[] pspanLocation) {
            pbstrMkDoc = null;
            return VSConstants.E_FAIL;
        }

        public int GetNameOfLocation(IVsTextBuffer pBuffer, int iLine, int iCol, out string pbstrName, out int piLineOffset) {
            pbstrName = "";
            piLineOffset = iCol;
            return VSConstants.S_OK;
        }

        public int GetProximityExpressions(IVsTextBuffer pBuffer, int iLine, int iCol, int cLines, out IVsEnumBSTR ppEnum) {
            ppEnum = null;
            return VSConstants.E_FAIL;
        }

        public int IsMappedLocation(IVsTextBuffer pBuffer, int iLine, int iCol) {
            return VSConstants.E_FAIL;
        }

        public int ResolveName(string pszName, uint dwFlags, out IVsEnumDebugName ppNames) {
            ppNames = null;
            return VSConstants.E_FAIL;
        }

        public int ValidateBreakpointLocation(IVsTextBuffer pBuffer, int iLine, int iCol, TextSpan[] pCodeSpan) {
            pCodeSpan[0].iStartLine = iLine;
            pCodeSpan[0].iEndLine = iLine;
            return VSConstants.S_OK;
        }

        #endregion
    }
}
