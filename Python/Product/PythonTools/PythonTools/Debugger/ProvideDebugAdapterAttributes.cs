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

using System;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.VisualStudioTools {
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    class ProvideDebugAdapterAttribute : RegistrationAttribute {
        private readonly string _debugAdapterHostCLSID = "{DAB324E9-7B35-454C-ACA8-F6BB0D5C8673}";
        private readonly string _name;
        private readonly string _engineId;
        private readonly string _adapterLauncherCLSID;
        private readonly string _customProtocolExtensionCLSID;
        private readonly string _languageName;
        private readonly string _languageId;
        private readonly Type _adapterLauncherType;
        private readonly Type _customProtocolType;

        public ProvideDebugAdapterAttribute(string name, string engineId, string adapterLauncherCLSID, string customProtocolExtensionCLSID, string languageName, string languageId, Type adapterLauncherType, Type customProtocolType) {
            _name = name;
            _engineId = engineId;
            _adapterLauncherCLSID = adapterLauncherCLSID;
            _customProtocolExtensionCLSID = customProtocolExtensionCLSID;
            _languageName = languageName;
            _languageId = languageId;
            _adapterLauncherType = adapterLauncherType;
            _customProtocolType = customProtocolType;
        }

        public override void Register(RegistrationContext context) {
            var engineKey = context.CreateKey("AD7Metrics\\Engine\\" + _engineId);


            // The following this line are boiler-plate settings required by all debug adapters.
            // Indicates that the "Debug Adapter Host" engine should be used
            engineKey.SetValue("CLSID", _debugAdapterHostCLSID);

            // Indicates that the engine should be loaded directly by VS
            engineKey.SetValue("AlwaysLoadLocal", 1);

            // Indicates that the engine supports 'goto' and 'gotoTargets' feature.
            engineKey.SetValue("SetNextStatement", 1);

            // Address and callstack breakpoints are not currently supported by the Debug Adapter Host
            engineKey.SetValue("AddressBP", 0);
            engineKey.SetValue("CallStackBP", 0);

            /*
             * "Attach to Process" support
             * To support attaching via the VS "Attach to Process" dialog:
             *     - Set the "Attach" property to "1" below
             *     - Provide a program provider.
             *     - Provide a port supplier GUID.  To attach to processes on the local machine by PID, the default
             *         port supplier is suffient, and can be used by uncommenting the "PortSupplier" property below.
             *     - Provide a custom IAdapterLauncher implementation to generate launch configuration JSON
             *         for the adapter based on the selection in the "Attach to Process" dialog, and specify
             *         its CLSID in the "AdapterLauncher" property below.
             */
            engineKey.SetValue("Attach", 1);
            engineKey.SetValue("PortSupplier", "{708C1ECA-FF48-11D2-904F-00C04FA302A1}");
            engineKey.SetValue("ProgramProvider", typeof(PythonTools.Debugger.DebugEngine.AD7ProgramProvider).GUID.ToString("B"));
            engineKey.SetValue("AdapterLauncher", _adapterLauncherCLSID);
            engineKey.SetValue("AutoSelectPriority", 6); // prioritize it higher than Native when auto-detecting Code Type

            /*
             * Modules request on attach behavior(optional)
             * If a debug adapter supports the "modules" request, the Debug Adapter Host will issue a request to get
             * the list of modules on attach.  Some debug adapters automatically send a set of "module" events on
             * attach and don't need the "modules" request, so it can be disabled by setting this property to "1".
             */
            engineKey.SetValue("SuppressModulesRequestOnAttach", 1);
            /*
             * Custom Protocol Extensions (optional)
             *   A debug adapter can implement non-standard extensions to the VS Code Debug Protocol, e.g. to communicate with
             *   custom UI or services hosted in Visual Studio.  To register custom protocol extensions, provide an implementation
             *   of "ICustomProtocolExtension", and specify its CLSID below.  The CLSID is defined in the "Type Registrations"
             *   section of this file.
             */
            engineKey.SetValue("CustomProtocolExtension", _customProtocolExtensionCLSID);

            /*
             * Set to "1" if the debug adapter will use the VS "Exception Setting" tool window.  The debug adapter's must
             * support one of the following:
             *     -Exception Breakpoints
             *         The debug adapter's response to the "initialize" request must contain a set of ExceptionBreakpointFilters,
             *         and the "ExceptionBreakpointCategory" property must be defined below.An optional set of
             *         "ExceptionBreakpointMappings" may also be provided if the VS exception names do not correspond to the
             *         "Label" properties of the ExceptionBreakpointFilters.
             *     -Exception Options
             *         The debug adapter's response to the "initialize" request must contain the "SupportsExceptionOptions"
             *         and "SupportsExceptionDetailsRequest" flags, and ExceptionCategoryMapping information must be supplied.
             */
            engineKey.SetValue("Exceptions", 1);
            engineKey.SetValue("ExceptionBreakpointCategory", "{EC1375B7-E2CE-43E8-BF75-DC638DE1F1F9}");

            var exceptionMapping = engineKey.CreateSubkey("ExceptionCategoryMappings");
            exceptionMapping.SetValue("Python Exceptions", "{EC1375B7-E2CE-43E8-BF75-DC638DE1F1F9}");

            /*
             * Set to "1" if the debug adapter supports the VS exception conditions experience(For skipping exceptions in specific modules).
             * The debug adapter's response to the "initialize" request must contain the "SupportsExceptionConditions"
             * and "SupportsExceptionDetailsRequest" flags.
             */
            engineKey.SetValue("ExceptionConditions", 0);

            /*
             * Debug Adapter Host settings
             * These settings control the behavior of the Debug Adapter Host
             *
             * Name of the debug adapter
             * This appears in VS in several places.  For example:
             *     -The "Select Code Type" dialog for choosing which debugger to attach to a process(if Attach is supported)
             *     -The "Debugging" column of the "Processes" tool window
             */
            engineKey.SetValue("Name", _name);

            /*
             * Path to the debug adapter executable
             */
            // engineKey.SetValue("Adapter", @"$PackageFolder$\DebugAdapter.exe");

            /*
             * Arguments for the debug adapter executable (optional)
             */
            // engineKey.SetValue("AdapterArgs", "");

            /*
             * Language name
             * This appears in (e.g.) the "Language" column of the Stack Trace tool window.
             */
            engineKey.SetValue("Language", _languageName);
            engineKey.SetValue("LanguageId", _languageId);

            // support search navigation for symbols while debugging
            engineKey.SetValue("SupportsEESearch", 1);

            /* 
             * Adapter launcher registration 
             */
            var adapterKey = context.CreateKey($"CLSID\\{_adapterLauncherCLSID}");
            var adapterAssembly = _adapterLauncherType.Assembly.GetName().Name;
            var adapterClassName = _adapterLauncherType.FullName;
            adapterKey.SetValue("Assembly", adapterAssembly);
            adapterKey.SetValue("Class", adapterClassName);
            adapterKey.SetValue("CodeBase", $@"$PackageFolder$\{adapterAssembly}.dll");

            var customProtocolKey = context.CreateKey($"CLSID\\{_customProtocolExtensionCLSID}");
            var customProtocolAssembly = _customProtocolType.Assembly.GetName().Name;
            var customProtocolClassName = _customProtocolType.FullName;
            customProtocolKey.SetValue("Assembly", customProtocolAssembly);
            customProtocolKey.SetValue("Class", customProtocolClassName);
            customProtocolKey.SetValue("CodeBase", $@"$PackageFolder$\{customProtocolAssembly}.dll");

            // When auto-detecting code type in Attach to Process dialog, we don't want Python processes
            // to be detected as both Python and Native by default (i.e. no mixed-mode debugging).
            using (var autoSelectIncompatKey = engineKey.CreateSubkey("AutoSelectIncompatibleList")) {
                autoSelectIncompatKey.SetValue("guidNativeOnlyEng", "{3B476D35-A401-11D2-AAD4-00C04F990171}");
            }

            using (var incompatKey = engineKey.CreateSubkey("IncompatibleList")) {
                incompatKey.SetValue("guidCOMPlusNativeEng", "{92EF0900-2251-11D2-B72E-0000F87572EF}");
                incompatKey.SetValue("guidCOMPlusOnlyEng", "{449EC4CC-30D2-4032-9256-EE18EB41B62B}");
                incompatKey.SetValue("guidScriptEng", "{F200A7E7-DEA5-11D0-B854-00A0244A1DE2}");
                incompatKey.SetValue("guidCOMPlusOnlyEng2", "{5FFF7536-0C87-462D-8FD2-7971D948E6DC}");
                incompatKey.SetValue("guidCOMPlusOnlyEng4", "{FB0D4648-F776-4980-95F8-BB7F36EBC1EE}");
                incompatKey.SetValue("guidNativeOnlyEng", "{3B476D35-A401-11D2-AAD4-00C04F990171}");
            }
        }

        public override void Unregister(RegistrationContext context) {
        }
    }
}
