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
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.PythonTools.Infrastructure;

namespace Microsoft.PythonTools.Interpreter {
    [Export(typeof(IPackageManagerProvider))]
    sealed class CPythonPipPackageManagerProvider : IPackageManagerProvider {
        class PipCommandsV26 : PipPackageManagerCommands {
            public override IEnumerable<string> Base() => new[] { "-c", ProcessOutput.QuoteSingleArgument("import pip; pip.main()") };
            public override IEnumerable<string> Prepare() => new[] { PythonToolsInstallPath.GetFile("pip_downloader.py", typeof(PipPackageManager).Assembly) };
        }

        class PipCommandsV27AndLater : PipPackageManagerCommands {
            public override IEnumerable<string> Prepare() => new[] { PythonToolsInstallPath.GetFile("pip_downloader.py", typeof(PipPackageManager).Assembly) };
        }

        private static readonly PipPackageManagerCommands CommandsV26 = new PipCommandsV26();
        private static readonly PipPackageManagerCommands CommandsV27AndLater = new PipCommandsV27AndLater();

        private readonly ICondaLocatorProvider _condaLocatorProvider;
        private readonly Dictionary<IPythonInterpreterFactory, IPackageManager> _packageManagerMap;

        [ImportingConstructor]
        public CPythonPipPackageManagerProvider(
            [Import] ICondaLocatorProvider condaLocatorProvider
        ) {
            _condaLocatorProvider = condaLocatorProvider;
            _packageManagerMap = new Dictionary<IPythonInterpreterFactory, IPackageManager>();
        }

        public IEnumerable<IPackageManager> GetPackageManagers(IPythonInterpreterFactory factory) {
            IPackageManager pm = null;
            lock (_packageManagerMap) {
                if (!_packageManagerMap.TryGetValue(factory, out pm)) {
                    pm = TryCreatePackageManager(factory);
                    if (pm != null) {
                        _packageManagerMap.Add(factory, pm);
                    }
                }
            }

            if (pm != null) {
                yield return pm;
            }
        }

        private IPackageManager TryCreatePackageManager(IPythonInterpreterFactory factory) {
            if (factory == null) {
                return null;
            }

            try {
                // 'python -m pip', causes this error on Python 2.6: pip is a package and cannot be directly executed
                // We have to use 'python -m pip' on pip v10, because pip.main() no longer exists
                // pip v10 is not supported on Python 2.6, so pip.main() is fine there
                var cmds = factory.Configuration.Version > new Version(2, 6) ? CommandsV27AndLater : CommandsV26;
                return new PipPackageManager(factory, cmds, 1000, _condaLocatorProvider);
            } catch (NotSupportedException) {
                return null;
            }
        }
    }
}
