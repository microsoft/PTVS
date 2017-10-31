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
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.PythonTools.Debugger.DebugEngine;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Navigation {
    /// <summary>
    /// Minimal language service.  Implemented directly rather than using the Managed Package
    /// Framework because we don't want to provide colorization services.  Instead we use the
    /// new Visual Studio 2010 APIs to provide these services.  But we still need this to
    /// provide a code window manager so that we can have a navigation bar (actually we don't, this
    /// should be switched over to using our TextViewCreationListener instead).
    /// </summary>
    [Guid(GuidList.guidPythonLanguageService)]
    internal sealed class PythonLanguageInfo : IVsLanguageInfo, IVsLanguageDebugInfo {
        private readonly IServiceProvider _serviceProvider;

        public PythonLanguageInfo(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
        }

        public int GetCodeWindowManager(IVsCodeWindow pCodeWin, out IVsCodeWindowManager ppCodeWinMgr) {
            var ptvsService = _serviceProvider.GetPythonToolsService();
            ppCodeWinMgr = ptvsService.GetOrCreateCodeWindowManager(pCodeWin);
            return VSConstants.S_OK;
        }

        public int GetFileExtensions(out string pbstrExtensions) {
            // This is the same extension the language service was
            // registered as supporting.
            pbstrExtensions = PythonConstants.SourceFileExtensions;
            return VSConstants.S_OK;
        }


        public int GetLanguageName(out string bstrName) {
            // This is the same name the language service was registered with.
            bstrName = PythonConstants.LanguageName;
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
            pguidLanguageID = DebuggerConstants.guidLanguagePython;
            return VSConstants.S_OK;
        }

        public int GetLocationOfName(string pszName, out string pbstrMkDoc, TextSpan[] pspanLocation) {
            pbstrMkDoc = null;
            return VSConstants.E_FAIL;
        }

        public int GetNameOfLocation(IVsTextBuffer pBuffer, int iLine, int iCol, out string pbstrName, out int piLineOffset) {
            var model = _serviceProvider.GetService(typeof(SComponentModel)) as IComponentModel;
            var service = model.GetService<IVsEditorAdaptersFactoryService>();
            var entryService = model.GetService<AnalysisEntryService>();
            var buffer = service.GetDataBuffer(pBuffer);
            AnalysisEntry entry;
            if (entryService != null && entryService.TryGetAnalysisEntry(buffer, out entry)) {
                var location = entry.Analyzer.WaitForRequest(entry.Analyzer.GetNameOfLocationAsync(entry, buffer, iLine, iCol), "PythonLanguageInfo.GetNameOfLocation");
                if (location != null) {
                    pbstrName = location.name;
                    piLineOffset = location.lineOffset;

                    return VSConstants.S_OK;
                }
            }

            pbstrName = null;
            piLineOffset = 0;
            return VSConstants.E_FAIL;
        }

        /// <summary>
        /// Called by debugger to get the list of expressions for the Autos debugger tool window.
        /// </summary>
        /// <remarks>
        /// MSDN docs specify that <paramref name="iLine"/> and <paramref name="iCol"/> specify the beginning of the span,
        /// but they actually specify the end of it (going <paramref name="cLines"/> lines back).
        /// </remarks>
        public int GetProximityExpressions(IVsTextBuffer pBuffer, int iLine, int iCol, int cLines, out IVsEnumBSTR ppEnum) {
            var model = _serviceProvider.GetService(typeof(SComponentModel)) as IComponentModel;
            var service = model.GetService<IVsEditorAdaptersFactoryService>();
            var entryService = model.GetService<AnalysisEntryService>();
            var buffer = service.GetDataBuffer(pBuffer);
            AnalysisEntry entry;
            if (entryService != null && entryService.TryGetAnalysisEntry(buffer, out entry)) {
                var names = entry.Analyzer.WaitForRequest(entry.Analyzer.GetProximityExpressionsAsync(entry, buffer, iLine, iCol, cLines), "PythonLanguageInfo.GetProximityExpressions");
                ppEnum = new EnumBSTR(names ?? Enumerable.Empty<string>());
            } else {
                ppEnum = new EnumBSTR(Enumerable.Empty<string>());
            }
            return VSConstants.S_OK;
        }

        public int IsMappedLocation(IVsTextBuffer pBuffer, int iLine, int iCol) {
            return VSConstants.E_FAIL;
        }

        public int ResolveName(string pszName, uint dwFlags, out IVsEnumDebugName ppNames) {
            /*if((((RESOLVENAMEFLAGS)dwFlags) & RESOLVENAMEFLAGS.RNF_BREAKPOINT) != 0) {
                // TODO: This should go through the project/analysis and see if we can
                // resolve the names...
            }*/
            ppNames = null;
            return VSConstants.E_FAIL;
        }

        public int ValidateBreakpointLocation(IVsTextBuffer pBuffer, int iLine, int iCol, TextSpan[] pCodeSpan) {            
            // per the docs, even if we don't indend to validate, we need to set the span info:
            // http://msdn.microsoft.com/en-us/library/microsoft.visualstudio.textmanager.interop.ivslanguagedebuginfo.validatebreakpointlocation.aspx
            // 
            // Caution
            // Even if you do not intend to support the ValidateBreakpointLocation method but your 
            // language does support breakpoints, you must implement this method and return a span 
            // that contains the specified line and column; otherwise, breakpoints cannot be set 
            // anywhere except line 1. You can return E_NOTIMPL to indicate that you do not otherwise 
            // support this method but the span must always be set. The example shows how this can be done.

            // http://pytools.codeplex.com/workitem/787
            // We were previously returning S_OK here indicating to VS that we have in fact validated
            // the breakpoint.  Validating breakpoints actually interacts and effectively disables
            // the "Highlight entire source line for breakpoints and current statement" option as instead
            // VS highlights the validated region.  So we return E_NOTIMPL here to indicate that we have 
            // not validated the breakpoint, and then VS will happily respect the option when we're in 
            // design mode.
            pCodeSpan[0].iStartLine = iLine;
            pCodeSpan[0].iEndLine = iLine;
            return VSConstants.E_NOTIMPL;
        }

#endregion
    }
}
