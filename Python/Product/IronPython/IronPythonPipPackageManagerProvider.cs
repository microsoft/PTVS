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
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.IronPythonTools.Interpreter {
    [Export(typeof(IPackageManagerProvider))]
    sealed class IronPythonPipPackageManagerProvider : IPackageManagerProvider {
        class IPyPipCommands : PipPackageManagerCommands {
            public override IEnumerable<string> Base() => new[] { "-X:Frames", "-c", ProcessOutput.QuoteSingleArgument("import pip; pip.main()") };
            public override IEnumerable<string> CheckIsReady() => new[] { "-X:Frames", "-c", "import pip" };
            public override IEnumerable<string> Prepare() => new[] { "-X:Frames", PythonToolsInstallPath.GetFile("pip_downloader.py", typeof(PipPackageManager).Assembly) };
        }

        private static readonly PipPackageManagerCommands Commands = new IPyPipCommands();

        private readonly ICondaLocatorProvider _condaLocatorProvider;

        [ImportingConstructor]
        public IronPythonPipPackageManagerProvider(
            [Import] ICondaLocatorProvider condaLocatorProvider
        ) {
            _condaLocatorProvider = condaLocatorProvider;
        }

        public IEnumerable<IPackageManager> GetPackageManagers(IPythonInterpreterFactory factory) {
            IPackageManager pm = null;
            if (factory is IronPythonAstInterpreterFactory) {
                try {
                    pm = new PipPackageManager(factory, Commands, 0, _condaLocatorProvider);
                } catch (NotSupportedException) {
                }
            }

            if (pm != null) {
                yield return pm;
            }
        }
    }
}
