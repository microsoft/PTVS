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
using Microsoft.PythonTools.Infrastructure;

namespace Microsoft.PythonTools.TestAdapter.Services {
    static class ProcessExecute {

        static public (string, string) RunWithTimeout(
            string filename,
            Dictionary<string, string> env,
            IEnumerable<string> arguments,
            string workingDirectory,
            string pathEnv,
            int timeoutInSeconds
        ) {
            using (var proc = ProcessOutput.Run(
                filename,
                arguments,
                workingDirectory,
                env,
                false,
                null
            )) {
                if (!proc.ExitCode.HasValue) {
                    if (!proc.Wait(TimeSpan.FromSeconds(timeoutInSeconds))) {
                        try {
                            proc.Kill();
                        } catch (InvalidOperationException) {
                            // Process has already exited
                        }
                        throw new TimeoutException();
                    }
                }

                return (string.Join(Environment.NewLine, proc.StandardOutputLines),
                    string.Join(Environment.NewLine, proc.StandardErrorLines));
            }
        }
    }
}
