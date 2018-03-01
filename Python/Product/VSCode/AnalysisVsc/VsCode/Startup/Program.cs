// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Based on https://github.com/CXuesong/LanguageServer.NET

// #define WAIT_FOR_DEBUGGER

using System.Linq;
using Microsoft.DsTools.Core;
using Microsoft.PythonTools.VsCode.Services;

namespace Microsoft.PythonTools.VsCode.Startup {
    internal static class Program {
        public static void Main(string[] args) {
            var debugMode = CheckDebugMode(args);
            using (CoreShell.Create()) {
                var connection = new VsCodeConnection(CoreShell.Current.ServiceManager);
                connection.Connect(debugMode);
            }
        }

        private static bool CheckDebugMode(string[] args) {
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