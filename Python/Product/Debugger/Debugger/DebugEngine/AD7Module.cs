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

namespace Microsoft.PythonTools.Debugger.DebugEngine
{
    // this class represents a module loaded in the debuggee process to the debugger. 
    class AD7Module : IDebugModule2, IDebugModule3
    {
        public readonly PythonModule DebuggedModule;

        public AD7Module(PythonModule debuggedModule)
        {
            this.DebuggedModule = debuggedModule;
        }

        #region IDebugModule2 Members

        // Gets the MODULE_INFO that describes this module.
        // This is how the debugger obtains most of the information about the module.
        int IDebugModule2.GetInfo(enum_MODULE_INFO_FIELDS dwFields, MODULE_INFO[] infoArray)
        {
            MODULE_INFO info = new MODULE_INFO();

            if ((dwFields & enum_MODULE_INFO_FIELDS.MIF_NAME) != 0)
            {
                info.m_bstrName = DebuggedModule.Name;
                Debug.Assert(info.m_bstrName != null);
                info.dwValidFields |= enum_MODULE_INFO_FIELDS.MIF_NAME;
            }
            if ((dwFields & enum_MODULE_INFO_FIELDS.MIF_URL) != 0)
            {
                info.m_bstrUrl = DebuggedModule.Filename;
                Debug.Assert(info.m_bstrUrl != null);
                info.dwValidFields |= enum_MODULE_INFO_FIELDS.MIF_URL;
            }
            if ((dwFields & enum_MODULE_INFO_FIELDS.MIF_LOADORDER) != 0)
            {
                info.m_dwLoadOrder = (uint)this.DebuggedModule.ModuleId;
                info.dwValidFields |= enum_MODULE_INFO_FIELDS.MIF_LOADORDER;
            }
            if ((dwFields & enum_MODULE_INFO_FIELDS.MIF_URLSYMBOLLOCATION) != 0)
            {
                if (ModulePath.IsPythonSourceFile(DebuggedModule.Filename))
                {
                    info.m_bstrUrlSymbolLocation = DebuggedModule.Filename;
                }
                else
                {
                    info.m_bstrUrlSymbolLocation = string.Empty;
                }
                info.dwValidFields |= enum_MODULE_INFO_FIELDS.MIF_URLSYMBOLLOCATION;
            }

            infoArray[0] = info;

            return VSConstants.S_OK;
        }

        #endregion

        #region Deprecated interface methods
        // These methods are not called by the Visual Studio debugger, so they don't need to be implemented

        int IDebugModule2.ReloadSymbols_Deprecated(string urlToSymbols, out string debugMessage)
        {
            debugMessage = null;
            Debug.Fail("This function is not called by the debugger.");
            return VSConstants.E_NOTIMPL;
        }

        int IDebugModule3.ReloadSymbols_Deprecated(string pszUrlToSymbols, out string pbstrDebugMessage)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region IDebugModule3 Members

        // IDebugModule3 represents a module that supports alternate locations of symbols and JustMyCode states.
        // The sample does not support alternate symbol locations or JustMyCode, but it does not display symbol load information 

        // Gets the MODULE_INFO that describes this module.
        // This is how the debugger obtains most of the information about the module.
        int IDebugModule3.GetInfo(enum_MODULE_INFO_FIELDS dwFields, MODULE_INFO[] pinfo)
        {
            return ((IDebugModule2)this).GetInfo(dwFields, pinfo);
        }

        // Returns a list of paths searched for symbols and the results of searching each path.
        int IDebugModule3.GetSymbolInfo(enum_SYMBOL_SEARCH_INFO_FIELDS dwFields, MODULE_SYMBOL_SEARCH_INFO[] pinfo)
        {
            // This engine only supports loading symbols at the location specified in the binary's symbol path location in the PE file and
            // does so only for the primary exe of the debuggee.
            // Therefore, it only displays if the symbols were loaded or not. If symbols were loaded, that path is added.
            pinfo[0] = new MODULE_SYMBOL_SEARCH_INFO();
            pinfo[0].dwValidFields = 1; // SSIF_VERBOSE_SEARCH_INFO;

            pinfo[0].bstrVerboseSearchInfo = Strings.DebugModuleNoSymbolsRequired;
            /*
            if (this.DebuggedModule.SymbolsLoaded)
            {
                string symbolPath = "Symbols Loaded - " + this.DebuggedModule.SymbolPath;
                pinfo[0].bstrVerboseSearchInfo = symbolPath;
            }
            else
            {
                string symbolsNotLoaded = "Symbols not loaded";
                pinfo[0].bstrVerboseSearchInfo = symbolsNotLoaded;
            }*/
            return VSConstants.S_OK;
        }

        int IDebugModule3.IsUserCode(out int pfUser)
        {
            pfUser = DebuggedModule.IsUserCode ? 1 : 0;
            return VSConstants.S_OK;
        }

        // Loads and initializes symbols for the current module when the user explicitly asks for them to load.
        // The sample engine only supports loading symbols from the location pointed to by the PE file which will load
        // when the module is loaded.
        int IDebugModule3.LoadSymbols()
        {
            throw new NotImplementedException();
        }

        // Used to support the JustMyCode features of the debugger.
        // The sample engine does not support JustMyCode so this is not implemented
        int IDebugModule3.SetJustMyCodeState(int fIsUserCode)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}