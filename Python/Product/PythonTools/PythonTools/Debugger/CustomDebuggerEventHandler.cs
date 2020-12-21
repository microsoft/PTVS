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
using System.ComponentModel.Design;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.PythonTools.Debugger;
using Microsoft.PythonTools.Debugger.DebugEngine;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;

namespace Microsoft.PythonTools.Debugger {
    [Guid(Guids.CustomDebuggerEventHandlerId)]
    class CustomDebuggerEventHandler : IVsCustomDebuggerEventHandler110 {
        private readonly IServiceProvider _serviceProvider;

        public CustomDebuggerEventHandler(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
        }

        public int OnCustomDebugEvent(ref Guid ProcessId, VsComponentMessage message) {
            switch ((VsPackageMessage)message.MessageCode) {
                case VsPackageMessage.WarnAboutPythonSymbols:
                    WarnAboutPythonSymbols((string)message.Parameter1);
                    return VSConstants.S_OK;
                // TODO: Evaluate if this is still necessary
                //case VsPackageMessage.WarnAboutPGO:
                //    WarnAboutPGO((string)message.Parameter1);
                //    return VSConstants.S_OK;
                case VsPackageMessage.SetDebugOptions:
                    SetDebugOptions((IDebugEngine2)message.Parameter1);
                    return VSConstants.S_OK;
                default:
                    return VSConstants.S_OK;
            }
        }

        private void WarnAboutPythonSymbols(string moduleName) {
            var dialog = new TaskDialog(_serviceProvider);

            var openSymbolSettings = new TaskDialogButton(Strings.MixedModeDebugSymbolsRequiredOpenSymbolSettings);
            var downloadSymbols = new TaskDialogButton(Strings.MixedModeDebugSymbolsRequiredDownloadSymbols);
            dialog.Buttons.Add(openSymbolSettings);
            dialog.Buttons.Add(downloadSymbols);
            dialog.Buttons.Add(TaskDialogButton.Close);
            dialog.UseCommandLinks = true;
            dialog.Title = Strings.MixedModeDebugSymbolsRequiredTitle;
            dialog.Content = Strings.MixedModeDebugSymbolsRequiredMessage.FormatUI(moduleName);
            dialog.Width = 0;

            dialog.ShowModal();

            if (dialog.SelectedButton == openSymbolSettings) {
                var cmdId = new CommandID(VSConstants.GUID_VSStandardCommandSet97, VSConstants.cmdidToolsOptions);
                _serviceProvider.GlobalInvoke(cmdId, "1F5E080F-CBD2-459C-8267-39fd83032166");
            } else if (dialog.SelectedButton == downloadSymbols) {
                PythonToolsPackage.OpenWebBrowser(
                    _serviceProvider,
                    string.Format("https://go.microsoft.com/fwlink/?LinkId=308954&clcid=0x{0:X}", CultureInfo.CurrentCulture.LCID)
                );
            }
        }

        // TODO: Evaluate if this is still necessary
        //private void WarnAboutPGO(string moduleName) {
        //    const string content =
        //        "Python/native mixed-mode debugging does not support Python interpreters that are built with PGO (profile-guided optimization) enabled. " +
        //        "If you are using a stock Python interpreter, you should upgrade to a more recent version of it. If you're using a custom built " +
        //        "interpreter, please disable PGO.";
        //    MessageBox.Show(content, "PGO Is Not Supported", MessageBoxButton.OK, MessageBoxImage.Error);
        //}

        private void SetDebugOptions(IDebugEngine2 engine) {
            var pyService = _serviceProvider.GetPythonToolsService();

            var options = new StringBuilder();
            options.Append(AD7Engine.StopOnEntry + "=True");
            if (pyService.DebuggerOptions.WaitOnAbnormalExit) {
                options.Append(";" + AD7Engine.WaitOnAbnormalExitSetting + "=True");
            }
            if (pyService.DebuggerOptions.WaitOnNormalExit) {
                options.Append(";" + AD7Engine.WaitOnNormalExitSetting + "=True");
            }
            if (pyService.DebuggerOptions.TeeStandardOutput) {
                options.Append(";" + AD7Engine.RedirectOutputSetting + "=True");
            }
            if (pyService.DebuggerOptions.BreakOnSystemExitZero) {
                options.Append(";" + AD7Engine.BreakSystemExitZero + "=True");
            }
            if (pyService.DebuggerOptions.DebugStdLib) {
                options.Append(";" + AD7Engine.DebugStdLib + "=True");
            }
            if (pyService.DebuggerOptions.ShowFunctionReturnValue) {
                options.Append(";" + AD7Engine.ShowReturnValue + "=True");
            }

            engine.SetMetric(AD7Engine.DebugOptionsMetric, options.ToString());
        }
    }
}
