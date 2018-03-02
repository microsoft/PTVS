// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

//#define WAIT_FOR_DEBUGGER

using System;
using System.Linq;
using Microsoft.DsTools.Core;
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