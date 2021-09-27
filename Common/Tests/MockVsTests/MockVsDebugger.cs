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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

namespace Microsoft.VisualStudioTools.MockVsTests
{
    class MockVsDebugger : IVsDebugger
    {
        public int AdviseDebugEventCallback(object punkDebuggerEvents)
        {
            throw new NotImplementedException();
        }

        public int AdviseDebuggerEvents(IVsDebuggerEvents pSink, out uint pdwCookie)
        {
            throw new NotImplementedException();
        }

        public int AllowEditsWhileDebugging(ref Guid guidLanguageService)
        {
            throw new NotImplementedException();
        }

        public int ExecCmdForTextPos(VsTextPos[] pTextPos, ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            throw new NotImplementedException();
        }

        public int GetDataTipValue(VisualStudio.TextManager.Interop.IVsTextLines pTextBuf, VisualStudio.TextManager.Interop.TextSpan[] pTS, string pszExpression, out string pbstrValue)
        {
            throw new NotImplementedException();
        }

        public int GetENCUpdate(out object ppUpdate)
        {
            throw new NotImplementedException();
        }

        public int GetMode(DBGMODE[] pdbgmode)
        {
            pdbgmode[0] = DBGMODE.DBGMODE_Design;
            return VSConstants.S_OK;
        }

        public int InsertBreakpointByName(ref Guid guidLanguage, string pszCodeLocationText)
        {
            throw new NotImplementedException();
        }

        public int IsBreakpointOnName(ref Guid guidLanguage, string pszCodeLocationText, out int pfIsBreakpoint)
        {
            throw new NotImplementedException();
        }

        public int LaunchDebugTargets(uint cTargets, IntPtr rgDebugTargetInfo)
        {
            throw new NotImplementedException();
        }

        public int ParseFileRedirection(string pszArgs, out string pbstrArgsProcessed, out IntPtr phStdInput, out IntPtr phStdOutput, out IntPtr phStdError)
        {
            throw new NotImplementedException();
        }

        public int QueryStatusForTextPos(VsTextPos[] pTextPos, ref Guid pguidCmdGroup, uint cCmds, VisualStudio.OLE.Interop.OLECMD[] prgCmds, IntPtr pCmdText)
        {
            throw new NotImplementedException();
        }

        public int RemoveBreakpointsByName(ref Guid guidLanguage, string pszCodeLocationText)
        {
            throw new NotImplementedException();
        }

        public int ToggleBreakpointByName(ref Guid guidLanguage, string pszCodeLocationText)
        {
            throw new NotImplementedException();
        }

        public int UnadviseDebugEventCallback(object punkDebuggerEvents)
        {
            throw new NotImplementedException();
        }

        public int UnadviseDebuggerEvents(uint dwCookie)
        {
            throw new NotImplementedException();
        }
    }
}
