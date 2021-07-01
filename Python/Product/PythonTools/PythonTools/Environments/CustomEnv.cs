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

namespace Microsoft.PythonTools.Environments
{
    static class CustomEnv
    {
        public static async Task<IPythonInterpreterFactory> CreateCustomEnv(
            IInterpreterRegistryService registryService,
            IInterpreterOptionsService optionsService,
            string prefixPath,
            string interpreterPath,
            string windowsInterpreterPath,
            string pathEnvironmentVariable,
            InterpreterArchitecture architecture,
            Version version,
            string description
        )
        {
            if (registryService == null)
            {
                throw new ArgumentNullException(nameof(registryService));
            }

            if (optionsService == null)
            {
                throw new ArgumentNullException(nameof(optionsService));
            }

            var id = optionsService.AddConfigurableInterpreter(
                description,
                new VisualStudioInterpreterConfiguration(
                    "", // ignored - id is generated and returned by AddConfigurableInterpreter
                    description,
                    prefixPath,
                    interpreterPath,
                    windowsInterpreterPath,
                    pathEnvironmentVariable,
                    architecture,
                    version
                )
            );

            return registryService.FindInterpreter(id);
        }
    }
}
