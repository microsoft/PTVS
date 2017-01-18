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

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;

namespace TestUtilities.Python {
    public static class PythonTestExtensions {
        public static HashSet<string> FindModules(this IPythonInterpreterFactory factory, params string[] moduleNames) {
            return factory.FindModulesAsync(moduleNames).GetAwaiter().GetResult();
        }

        public static void Parse(this IPythonProjectEntry entry, PythonLanguageVersion version, string code) {
            using (var parser = Parser.CreateParser(new StringReader(code), version)) {
                entry.UpdateTree(parser.ParseFile(), null);
            }
        }

        public static void ParseFormat(this IPythonProjectEntry entry, PythonLanguageVersion version, string format, params object[] args) {
            entry.Parse(version, string.Format(format, args));
        }

        public static Task InstallPipAsync(this IPythonInterpreterFactory factory) {
            if (factory.PackageManager.IsReady) {
                return Task.FromResult<object>(null);
            }
            return factory.PackageManager.PrepareAsync(new TestPackageManagerUI(), CancellationToken.None);
        }

        public static void InstallPip(this IPythonInterpreterFactory factory) {
            factory.InstallPipAsync().WaitAndUnwrapExceptions();
        }

        public static Task<bool> PipInstallAsync(this IPythonInterpreterFactory factory, string arguments) {
            return factory.PackageManager.InstallAsync(
                PackageSpec.FromArguments(arguments),
                new TestPackageManagerUI(),
                CancellationToken.None
            );
        }

        public static bool PipInstall(this IPythonInterpreterFactory factory, string arguments) {
            return factory.PipInstallAsync(arguments).WaitAndUnwrapExceptions();
        }

        public static Task<bool> PipUninstallAsync(this IPythonInterpreterFactory factory, string arguments) {
            return factory.PackageManager.UninstallAsync(
                PackageSpec.FromArguments(arguments),
                new TestPackageManagerUI(),
                CancellationToken.None
            );
        }

        public static bool PipUninstall(this IPythonInterpreterFactory factory, string arguments) {
            return factory.PipUninstallAsync(arguments).WaitAndUnwrapExceptions();
        }
    }
}
