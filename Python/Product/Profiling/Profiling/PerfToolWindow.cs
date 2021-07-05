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

namespace Microsoft.PythonTools.Profiling {
    [Guid(WindowGuidString)]
    class PerfToolWindow : ToolWindowPane {
        internal const string WindowGuidString = "328AF5EC-350F-4A96-B847-90F38B18E9BF";
        internal static Guid WindowGuid = new Guid(WindowGuidString);
        private SessionsNode _sessions;

        public PerfToolWindow(IServiceProvider services) : base(services) {
            ToolClsid = GuidList.VsUIHierarchyWindow_guid;
            Caption = Strings.PerformanceToolWindowTitle;
        }

        public override void OnToolWindowCreated() {
            base.OnToolWindowCreated();

            var frame = (IVsWindowFrame)Frame;
            object ouhw;
            ErrorHandler.ThrowOnFailure(frame.GetProperty((int)__VSFPROPID.VSFPROPID_DocView, out ouhw));

            // initialie w/ our hierarchy
            var hw = ouhw as IVsUIHierarchyWindow;
            _sessions = new SessionsNode((IServiceProvider)Package, hw);
            object punk;
            ErrorHandler.ThrowOnFailure(hw.Init(
                _sessions,
                (uint)(__UIHWINFLAGS.UIHWF_SupportToolWindowToolbars |
                __UIHWINFLAGS.UIHWF_InitWithHiddenParentRoot |
                __UIHWINFLAGS.UIHWF_HandlesCmdsAsActiveHierarchy),
                out punk
            ));

            // add our toolbar which  is defined in our VSCT file
            object otbh;
            ErrorHandler.ThrowOnFailure(frame.GetProperty((int)__VSFPROPID.VSFPROPID_ToolbarHost, out otbh));
            IVsToolWindowToolbarHost tbh = otbh as IVsToolWindowToolbarHost;
            Guid guidPerfMenuGroup = GuidList.guidPythonProfilingCmdSet;
            ErrorHandler.ThrowOnFailure(tbh.AddToolbar(VSTWT_LOCATION.VSTWT_TOP, ref guidPerfMenuGroup, PkgCmdIDList.menuIdPerfToolbar));
        }

        public SessionsNode Sessions => _sessions;
    }
}
