// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Based on https://github.com/CXuesong/LanguageServer.NET

// #define WAIT_FOR_DEBUGGER

using System;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.DsTools.Core;
using Microsoft.PythonTools.Analysis.LanguageServer;
using Microsoft.PythonTools.VsCode.Commands;
using Microsoft.PythonTools.VsCode.Services;
using StreamJsonRpc;

namespace Microsoft.PythonTools.VsCode.Startup
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            var debugMode = CheckDebugMode(args);
            using (CoreShell.Create())
            {
                var services = CoreShell.Current.ServiceManager;
                var cts = new CancellationTokenSource();

                using (var cin = Console.OpenStandardInput())
                using (var cout = Console.OpenStandardOutput())

                using (var server = new Server(cts))
                using (JsonRpc.Attach(cout, cin, server))
                using (var vsc = JsonRpc.Attach(cout, cin))
                {
                    services.AddService(new UIService(vsc));
                    services.AddService(new TelemetryService(vsc));
                    using (var eventHandler = new ServerNotificationsHandler(server, services))
                    {
                        // Wait for the "shutdown" request.
                        cts.Token.WaitHandle.WaitOne();
                    }
                }
            }
        }

        private static bool CheckDebugMode(string[] args)
        {
            var debugMode = args.Any(a => a.EqualsOrdinal("--debug"));
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