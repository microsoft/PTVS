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
using System.IO;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace AnalysisTests {
    [TestClass]
    public class MutateStdLibTest {
        [TestInitialize]
        public void TestInitialize() => TestEnvironmentImpl.TestInitialize();

        [TestCleanup]
        public void TestCleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(2)]
        [TestCategory("10s"), TestCategory("60s")]
        public void TestMutateStdLibV26() {
            TestMutateStdLib(PythonPaths.Python26_x64 ?? PythonPaths.Python26);
        }

        [TestMethod, Priority(2)]
        [TestCategory("10s"), TestCategory("60s")]
        public void TestMutateStdLibV27() {
            TestMutateStdLib(PythonPaths.Python27_x64 ?? PythonPaths.Python27);
        }

        [TestMethod, Priority(2)]
        [TestCategory("10s"), TestCategory("60s")]
        public void TestMutateStdLibV31() {
            TestMutateStdLib(PythonPaths.Python31_x64 ?? PythonPaths.Python31);
        }

        [TestMethod, Priority(2)]
        [TestCategory("10s"), TestCategory("60s")]
        public void TestMutateStdLibV32() {
            TestMutateStdLib(PythonPaths.Python32_x64 ?? PythonPaths.Python32);
        }

        [TestMethod, Priority(2)]
        [TestCategory("10s"), TestCategory("60s")]
        public void TestMutateStdLibV33() {
            TestMutateStdLib(PythonPaths.Python33_x64 ?? PythonPaths.Python33);
        }

        [TestMethod, Priority(2)]
        [TestCategory("10s"), TestCategory("60s")]
        public void TestMutateStdLibV34() {
            TestMutateStdLib(PythonPaths.Python34_x64 ?? PythonPaths.Python34);
        }

        [TestMethod, Priority(2)]
        [TestCategory("10s"), TestCategory("60s")]
        public void TestMutateStdLibV35() {
            TestMutateStdLib(PythonPaths.Python35_x64 ?? PythonPaths.Python35);
        }

        [TestMethod, Priority(2)]
        [TestCategory("10s"), TestCategory("60s")]
        public void TestMutateStdLibV36() {
            TestMutateStdLib(PythonPaths.Python36_x64 ?? PythonPaths.Python36);
        }

        private void TestMutateStdLib(PythonVersion version) {
            version.AssertInstalled();

            for (int i = 0; i < 100; i++) {
                int seed = (int)DateTime.Now.Ticks;
                var random = new Random(seed);
                Console.WriteLine("Seed == " + seed);


                Console.WriteLine("Testing version {0} {1}", version.Version, Path.Combine(version.PrefixPath, "Lib"));
                int ran = 0, succeeded = 0;
                string[] files;
                try {
                    files = Directory.GetFiles(Path.Combine(version.PrefixPath, "Lib"));
                } catch (DirectoryNotFoundException) {
                    continue;
                }

                foreach (var file in files) {
                    try {
                        if (file.EndsWith(".py")) {
                            ran++;
                            TestOneFileMutated(file, version.Version, random);
                            succeeded++;
                        }
                    } catch (Exception e) {
                        Console.WriteLine(e);
                        Console.WriteLine("Failed: {0}", file);
                        break;
                    }
                }

                Assert.AreEqual(ran, succeeded);
            }
        }

        private static void TestOneFileMutated(string filename, PythonLanguageVersion version, Random random) {
            var originalText = File.ReadAllText(filename);
            int start = random.Next(originalText.Length);
            int end = random.Next(originalText.Length);

            int realStart = Math.Min(start, end);
            int length = Math.Max(start, end) - Math.Min(start, end);
            //Console.WriteLine("Removing {1} chars at {0}", realStart, length);
            originalText = originalText.Substring(realStart, length);

            ParserRoundTripTest.TestOneString(version, originalText);
        }
    }
}
