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
// See the Apache License 2.0 for the specific language governing permissions and
// limitations under the License.

using System;
using System.IO;
using System.Linq;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace VSInterpretersTests {
    [TestClass]
    public class PipDownloaderTests {
        [TestMethod, Priority(UnitTestPriority.P0)]
        public void EnsurePipPreservesPipConfiguration() {
            var python = PythonPaths.LatestVersion;
            python.AssertInstalled("Unable to run test because Python 3 must be installed");

            var pipDownloader = Path.GetFullPath(Path.Combine(
                TestData.Root,
                "..",
                "Product",
                "PythonTools",
                "pip_downloader.py"
            ));

            const string sentinel = "sentinel-pip-config.ini";
            const string probe = @"
import os
import runpy
import sys
from unittest.mock import patch

module = runpy.run_path(sys.argv[1])
calls = []

class MutatingEnsurePip:
    @staticmethod
    def bootstrap(**kwargs):
        os.environ[""PIP_CONFIG_FILE""] = os.devnull

def record_call(args):
    calls.append(args)

os.environ[""PIP_CONFIG_FILE""] = sys.argv[2]
with patch.object(module[""subprocess""], ""check_call"", side_effect=record_call):
    module[""install_from_ensurepip""](MutatingEnsurePip)

print(""RESULT:"" + os.environ[""PIP_CONFIG_FILE""])
for call in calls:
    print(""CALL:"" + ""|"".join(call))
";

            string probePath;
            using (FileUtils.TemporaryTextFile(out probePath, probe))
            using (var output = ProcessOutput.RunHiddenAndCapture(
                python.InterpreterPath,
                probePath,
                pipDownloader,
                sentinel
            )) {
                Assert.IsTrue(output.Wait(TimeSpan.FromSeconds(30)), "Probe did not exit promptly");

                var diagnostics = string.Join(
                    Environment.NewLine,
                    output.StandardOutputLines.Concat(output.StandardErrorLines)
                );
                Assert.AreEqual(0, output.ExitCode, diagnostics);

                var lines = output.StandardOutputLines
                    .Where(line => line.StartsWith("RESULT:") || line.StartsWith("CALL:"))
                    .ToArray();
                Assert.AreEqual(3, lines.Length, diagnostics);
                Assert.AreEqual("RESULT:" + sentinel, lines[0]);
                var ensurePipCall = lines[1].Substring("CALL:".Length).Split('|');
                var pipCall = lines[2].Substring("CALL:".Length).Split('|');
                Assert.AreEqual(ensurePipCall[0], pipCall[0]);
                CollectionAssert.AreEqual(
                    new[] {
                        "-m",
                        "ensurepip",
                        "--upgrade",
                        "--default-pip"
                    },
                    ensurePipCall.Skip(1).ToArray()
                );
                CollectionAssert.AreEqual(
                    new[] {
                        "-m",
                        "pip",
                        "install",
                        "-U",
                        "pip",
                        "setuptools",
                        "wheel"
                    },
                    pipCall.Skip(1).ToArray()
                );
            }
        }
    }
}
