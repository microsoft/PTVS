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
using Microsoft.VisualStudio;

namespace Microsoft.PythonTools.Project.Web {
    /// <summary>
    /// Merges the PTVS IVsCfg object with the Venus IVsCfg implementation redirecting
    /// things appropriately to either one.
    /// </summary>
    class PythonWebProjectConfig :
        IVsCfg,
        IVsProjectCfg,
        IVsProjectCfg2,
        IVsProjectFlavorCfg,
        IVsDebuggableProjectCfg,
        ISpecifyPropertyPages,
        IVsSpecifyProjectDesignerPages,
        IVsCfgBrowseObject {
        private readonly IVsCfg _pythonCfg;
        private readonly IVsProjectFlavorCfg _webCfg;

        public PythonWebProjectConfig(IVsCfg pythonCfg, IVsProjectFlavorCfg webConfig) {
            _pythonCfg = pythonCfg;
            _webCfg = webConfig;
        }

        #region IVsCfg Members

        public int get_DisplayName(out string pbstrDisplayName) {
            return _pythonCfg.get_DisplayName(out pbstrDisplayName);
        }

        public int get_IsDebugOnly(out int pfIsDebugOnly) {
            return _pythonCfg.get_IsDebugOnly(out pfIsDebugOnly);
        }

        public int get_IsReleaseOnly(out int pfIsReleaseOnly) {
            return _pythonCfg.get_IsReleaseOnly(out pfIsReleaseOnly);
        }

        #endregion

        #region IVsProjectCfg Members

        public int EnumOutputs(out IVsEnumOutputs ppIVsEnumOutputs) {
            IVsProjectCfg projCfg = _webCfg as IVsProjectCfg;
            if (projCfg != null) {
                return projCfg.EnumOutputs(out ppIVsEnumOutputs);
            }
            ppIVsEnumOutputs = null;
            return VSConstants.E_NOTIMPL;
        }

        public int OpenOutput(string szOutputCanonicalName, out IVsOutput ppIVsOutput) {
            IVsProjectCfg projCfg = _webCfg as IVsProjectCfg;
            if (projCfg != null) {
                return projCfg.OpenOutput(szOutputCanonicalName, out ppIVsOutput);
            }
            ppIVsOutput = null;
            return VSConstants.E_NOTIMPL;
        }

        public int get_BuildableProjectCfg(out IVsBuildableProjectCfg ppIVsBuildableProjectCfg) {
            IVsProjectCfg projCfg = _webCfg as IVsProjectCfg;
            if (projCfg != null) {
                return projCfg.get_BuildableProjectCfg(out ppIVsBuildableProjectCfg);
            }
            ppIVsBuildableProjectCfg = null;
            return VSConstants.E_NOTIMPL;
        }

        public int get_CanonicalName(out string pbstrCanonicalName) {
            IVsProjectCfg projCfg = _webCfg as IVsProjectCfg;
            if (projCfg != null) {
                return projCfg.get_CanonicalName(out pbstrCanonicalName);
            }
            pbstrCanonicalName = null;
            return VSConstants.E_NOTIMPL;
        }

        public int get_IsPackaged(out int pfIsPackaged) {
            IVsProjectCfg projCfg = _webCfg as IVsProjectCfg;
            if (projCfg != null) {
                return projCfg.get_IsPackaged(out pfIsPackaged);
            }
            pfIsPackaged = 0;
            return VSConstants.E_NOTIMPL;
        }

        public int get_IsSpecifyingOutputSupported(out int pfIsSpecifyingOutputSupported) {
            IVsProjectCfg projCfg = _webCfg as IVsProjectCfg;
            if (projCfg != null) {
                return projCfg.get_IsSpecifyingOutputSupported(out pfIsSpecifyingOutputSupported);
            }
            pfIsSpecifyingOutputSupported = 0;
            return VSConstants.E_NOTIMPL;
        }

        public int get_Platform(out Guid pguidPlatform) {
            IVsProjectCfg projCfg = _webCfg as IVsProjectCfg;
            if (projCfg != null) {
                return projCfg.get_Platform(out pguidPlatform);
            }
            pguidPlatform = Guid.Empty;
            return VSConstants.E_NOTIMPL;
        }

        public int get_ProjectCfgProvider(out IVsProjectCfgProvider ppIVsProjectCfgProvider) {
            IVsProjectCfg projCfg = _webCfg as IVsProjectCfg;
            if (projCfg != null) {
                return projCfg.get_ProjectCfgProvider(out ppIVsProjectCfgProvider);
            }
            ppIVsProjectCfgProvider = null;
            return VSConstants.E_NOTIMPL;
        }

        public int get_RootURL(out string pbstrRootURL) {
            IVsProjectCfg projCfg = _webCfg as IVsProjectCfg;
            if (projCfg != null) {
                return projCfg.get_RootURL(out pbstrRootURL);
            }
            pbstrRootURL = null;
            return VSConstants.E_NOTIMPL;
        }

        public int get_TargetCodePage(out uint puiTargetCodePage) {
            IVsProjectCfg projCfg = _webCfg as IVsProjectCfg;
            if (projCfg != null) {
                return projCfg.get_TargetCodePage(out puiTargetCodePage);
            }
            puiTargetCodePage = 0;
            return VSConstants.E_NOTIMPL;
        }

        public int get_UpdateSequenceNumber(ULARGE_INTEGER[] puliUSN) {
            IVsProjectCfg projCfg = _webCfg as IVsProjectCfg;
            if (projCfg != null) {
                return projCfg.get_UpdateSequenceNumber(puliUSN);
            }
            return VSConstants.E_NOTIMPL;
        }

        #endregion

        #region IVsProjectCfg2 Members

        public int OpenOutputGroup(string szCanonicalName, out IVsOutputGroup ppIVsOutputGroup) {
            IVsProjectCfg2 projCfg = _pythonCfg as IVsProjectCfg2;
            if (projCfg != null) {
                return projCfg.OpenOutputGroup(szCanonicalName, out ppIVsOutputGroup);
            }
            ppIVsOutputGroup = null;
            return VSConstants.E_NOTIMPL;
        }

        public int OutputsRequireAppRoot(out int pfRequiresAppRoot) {
            IVsProjectCfg2 projCfg = _pythonCfg as IVsProjectCfg2;
            if (projCfg != null) {
                return projCfg.OutputsRequireAppRoot(out pfRequiresAppRoot);
            }
            pfRequiresAppRoot = 1;
            return VSConstants.E_NOTIMPL;
        }

        public int get_CfgType(ref Guid iidCfg, out IntPtr ppCfg) {
            if (iidCfg == typeof(IVsDebuggableProjectCfg).GUID) {
                var pyCfg = _pythonCfg as IVsProjectFlavorCfg;
                if (pyCfg != null) {
                    return pyCfg.get_CfgType(ref iidCfg, out ppCfg);
                }
            }

            var projCfg = _webCfg as IVsProjectFlavorCfg;
            if (projCfg != null) {
                return projCfg.get_CfgType(ref iidCfg, out ppCfg);
            }
            ppCfg = IntPtr.Zero;
            return VSConstants.E_NOTIMPL;
        }

        public int get_IsPrivate(out int pfPrivate) {
            IVsProjectCfg2 projCfg = _pythonCfg as IVsProjectCfg2;
            if (projCfg != null) {
                return projCfg.get_IsPrivate(out pfPrivate);
            }
            pfPrivate = 0;
            return VSConstants.E_NOTIMPL;
        }

        public int get_OutputGroups(uint celt, IVsOutputGroup[] rgpcfg, uint[] pcActual = null) {
            IVsProjectCfg2 projCfg = _pythonCfg as IVsProjectCfg2;
            if (projCfg != null) {
                return projCfg.get_OutputGroups(celt, rgpcfg, pcActual);
            }
            return VSConstants.E_NOTIMPL;
        }

        public int get_VirtualRoot(out string pbstrVRoot) {
            IVsProjectCfg2 projCfg = _pythonCfg as IVsProjectCfg2;
            if (projCfg != null) {
                return projCfg.get_VirtualRoot(out pbstrVRoot);
            }
            pbstrVRoot = null;
            return VSConstants.E_NOTIMPL;
        }

        #endregion

        #region IVsProjectFlavorCfg Members

        public int Close() {
            IVsProjectFlavorCfg cfg = _webCfg as IVsProjectFlavorCfg;
            if (cfg != null) {
                return cfg.Close();
            }
            return VSConstants.S_OK;
        }

        #endregion

        #region IVsDebuggableProjectCfg Members

        public int DebugLaunch(uint grfLaunch) {
            IVsDebuggableProjectCfg cfg = _pythonCfg as IVsDebuggableProjectCfg;
            if (cfg != null) {
                return cfg.DebugLaunch(grfLaunch);
            }
            return VSConstants.E_NOTIMPL;
        }

        public int QueryDebugLaunch(uint grfLaunch, out int pfCanLaunch) {
            IVsDebuggableProjectCfg cfg = _pythonCfg as IVsDebuggableProjectCfg;
            if (cfg != null) {
                return cfg.QueryDebugLaunch(grfLaunch, out pfCanLaunch);
            }
            pfCanLaunch = 0;
            return VSConstants.E_NOTIMPL;
        }

        #endregion

        #region ISpecifyPropertyPages Members

        public void GetPages(CAUUID[] pPages) {
            var cfg = _pythonCfg as ISpecifyPropertyPages;
            if (cfg != null) {
                cfg.GetPages(pPages);
            }
        }

        #endregion

        #region IVsSpecifyProjectDesignerPages Members

        public int GetProjectDesignerPages(CAUUID[] pPages) {
            var cfg = _pythonCfg as IVsSpecifyProjectDesignerPages;
            if (cfg != null) {
                return cfg.GetProjectDesignerPages(pPages);
            }
            return VSConstants.E_NOTIMPL;
        }

        #endregion

        #region IVsCfgBrowseObject Members

        public int GetCfg(out IVsCfg ppCfg) {
            ppCfg = this;
            return VSConstants.S_OK;
        }

        public int GetProjectItem(out IVsHierarchy pHier, out uint pItemid) {
            var cfg = _pythonCfg as IVsCfgBrowseObject;
            if (cfg != null) {
                return cfg.GetProjectItem(out pHier, out pItemid);
            }
            pHier = null;
            pItemid = 0;
            return VSConstants.E_NOTIMPL;
        }

        #endregion
    }
}
