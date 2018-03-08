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

#define WAIT_FOR_DEBUGGER

using System;
using System.Linq;
using Microsoft.PythonTools.VsCode.Services;
using StreamJsonRpc;

namespace Microsoft.PythonTools.VsCode {
    internal static class Program {
        public static void Main(string[] args) {
            var debugMode = CheckDebugMode(args);
            using (CoreShell.Create()) {
                var services = CoreShell.Current.ServiceManager;

                using (var cin = Console.OpenStandardInput())
                using (var cout = Console.OpenStandardOutput())
                using (var server = new LanguageServer())
                using (var rpc = JsonRpc.Attach(cout, cin, server)) {
                    services.AddService(new UIService(rpc));
                    services.AddService(new TelemetryService(rpc));
                    var token = server.Start(services, rpc);
                    // Wait for the "shutdown" request.
                    token.WaitHandle.WaitOne();
                }
            }
        }

        private static bool CheckDebugMode(string[] args) {
            var debugMode = args.Any(a => a == "--debug");
            //if (debugMode) {
#if WAIT_FOR_DEBUGGER
            while (!System.Diagnostics.Debugger.IsAttached) {
                System.Threading.Thread.Sleep(1000);
            }
#endif
            //}
            return debugMode;
        }
    }
}