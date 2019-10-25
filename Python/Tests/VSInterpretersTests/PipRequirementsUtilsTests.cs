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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace VSInterpretersTests {
    [TestClass]
    public class PipRequirementsUtilsTests {
        [TestMethod, Priority(UnitTestPriority.P1)]
        public void MergeRequirements() {
            // Comments should be preserved, only package specs should change.
            AssertUtil.AreEqual(
                PipRequirementsUtils.MergeRequirements(new[] {
                    "a # with a comment",
                    "B==0.2",
                    "# just a comment B==01234",
                    "",
                    "x < 1",
                    "d==1.0",
                    "e==2.0",
                    "f==3.0",
                    "-r user/requirements.txt",
                    "git+https://myvcs.com/some_dependency",
                }, new[] {
                    "b==0.1",
                    "a==0.2",
                    "c==0.3",
                    "e==4.0",
                    "x==0.8"
                }.Select(p => PackageSpec.FromRequirement(p)), false),
                "a==0.2 # with a comment",
                "b==0.1",
                "# just a comment B==01234",
                "",
                "x==0.8",
                "d==1.0",
                "e==4.0",
                "f==3.0",
                "-r user/requirements.txt",
                "git+https://myvcs.com/some_dependency"
            );

            // addNew is true, so the c==0.3 should be added.
            AssertUtil.AreEqual(
                PipRequirementsUtils.MergeRequirements(new[] {
                    "a # with a comment",
                    "b==0.2",
                    "# just a comment B==01234"
                }, new[] {
                    "B==0.1",   // case is updated
                    "a==0.2",
                    "c==0.3"
                }.Select(p => PackageSpec.FromRequirement(p)), true),
                "a==0.2 # with a comment",
                "B==0.1",
                "# just a comment B==01234",
                "c==0.3"
            );

            // No existing entries, so the new ones are sorted and returned.
            AssertUtil.AreEqual(
                PipRequirementsUtils.MergeRequirements(null, new[] {
                    "b==0.2",
                    "a==0.1",
                    "c==0.3"
                }.Select(p => PackageSpec.FromRequirement(p)), false),
                "a==0.1",
                "b==0.2",
                "c==0.3"
            );

            // Check all the inequalities
            const string inequalities = "<=|>=|<|>|!=|==";
            AssertUtil.AreEqual(
                PipRequirementsUtils.MergeRequirements(
                    inequalities.Split('|').Select(s => "a " + s + " 1.2.3"),
                    new[] { "a==0" }.Select(p => PackageSpec.FromRequirement(p)),
                    false
                ),
                inequalities.Split('|').Select(_ => "a==0").ToArray()
            );
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void MergeRequirementsMismatchedCase() {
            AssertUtil.AreEqual(
                PipRequirementsUtils.MergeRequirements(new[] {
                    "aaaaaa==0.0",
                    "BbBbBb==0.1",
                    "CCCCCC==0.2"
                }, new[] {
                    "aaaAAA==0.1",
                    "bbbBBB==0.2",
                    "cccCCC==0.3"
                }.Select(p => PackageSpec.FromRequirement(p)), false),
                "aaaAAA==0.1",
                "bbbBBB==0.2",
                "cccCCC==0.3"
            );

            // https://pytools.codeplex.com/workitem/2465
            AssertUtil.AreEqual(
                PipRequirementsUtils.MergeRequirements(new[] {
                    "Flask==0.10.1",
                    "itsdangerous==0.24",
                    "Jinja2==2.7.3",
                    "MarkupSafe==0.23",
                    "Werkzeug==0.9.6"
                }, new[] {
                    "flask==0.10.1",
                    "itsdangerous==0.24",
                    "jinja2==2.7.3",
                    "markupsafe==0.23",
                    "werkzeug==0.9.6"
                }.Select(p => PackageSpec.FromRequirement(p)), false),
                "flask==0.10.1",
                "itsdangerous==0.24",
                "jinja2==2.7.3",
                "markupsafe==0.23",
                "werkzeug==0.9.6"
            );
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public async Task DetectReqPkgMissingPython2Async() {
            PythonVersion pythonInterpreter =   PythonPaths.Python27_x64 ??
                                                PythonPaths.Python27;
            pythonInterpreter.AssertInstalled("Unable to run test because python 2.7 must be installed");

            await DetectReqPkgMissingAsync(pythonInterpreter);
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public async Task DetectReqPkgMissingPython3Async() {
            PythonVersion pythonInterpreter =   PythonPaths.Python37_x64 ??
                                                PythonPaths.Python37 ??
                                                PythonPaths.Python36_x64 ??
                                                PythonPaths.Python36 ??
                                                PythonPaths.Python35_x64 ??
                                                PythonPaths.Python35;
            pythonInterpreter.AssertInstalled("Unable to run test because python 3.5, 3.6, or 3.7 must be installed");

            await DetectReqPkgMissingAsync(pythonInterpreter);
        }

        private async Task DetectReqPkgMissingAsync(PythonVersion pythonInterpreter) {
            string virtualEnvPath = TestData.GetTempPath();
            string interpreterExePath = Path.Combine(virtualEnvPath, "Scripts", "python.exe");
            string reqTextPath = Path.Combine(virtualEnvPath, "requirements.txt");
            var installPackages = new[] { "cookies >= 2.0", "Bottle==0.8.2" };

            pythonInterpreter.CreateVirtualEnv(virtualEnvPath, installPackages);

            // Test cases for packages not missing
            bool isPackageMissing = await PipRequirementsUtils.DetectMissingPackagesAsync(interpreterExePath, reqTextPath);
            Assert.IsFalse(isPackageMissing, "Expected no missing packages because requirements.txt does not exist");

            File.WriteAllText(reqTextPath, String.Empty);
            isPackageMissing = await PipRequirementsUtils.DetectMissingPackagesAsync(interpreterExePath, reqTextPath);
            Assert.IsFalse(isPackageMissing, "Expected no missing packages because requirements.txt is empty");
            File.Delete(reqTextPath);

            File.WriteAllLines(reqTextPath, installPackages);
            File.AppendAllLines(reqTextPath,
                new string[] {
                    "    ",
                    "$InvalidLineOfText   ",
                    "#MissingPackageName",
                    " git+https://myvcs.com/some_dependency@",
                    "coo kies",
                    "cookies >  = 100 ",
                    "cookies >= 1.0 <= 2.0 flask == 2.0"
                });
            isPackageMissing = await PipRequirementsUtils.DetectMissingPackagesAsync(interpreterExePath, reqTextPath);
            Assert.IsFalse(isPackageMissing, "Expected no missing packages because all packages are installed and invalid lines should be ignored");
            File.Delete(reqTextPath);

            File.WriteAllLines(reqTextPath, installPackages, encoding: new UTF8Encoding(true));
            isPackageMissing = await PipRequirementsUtils.DetectMissingPackagesAsync(interpreterExePath, reqTextPath);
            Assert.IsFalse(isPackageMissing, "Expected no missing packages because all packages are installed and UTF-8 BOM signature should be ignored");
            File.Delete(reqTextPath);

            // Test cases for packages missing
            File.WriteAllLines(reqTextPath,
                new string[] { "   MissingPackageName   " });
            isPackageMissing = await PipRequirementsUtils.DetectMissingPackagesAsync(interpreterExePath, reqTextPath);
            Assert.IsTrue(isPackageMissing, "Expected missing packages because \"MissingPackageName\" it is not installed");
            File.Delete(reqTextPath);

            File.WriteAllLines(reqTextPath,
                new string[] { "Cookies<=1.0" });
            isPackageMissing = await PipRequirementsUtils.DetectMissingPackagesAsync(interpreterExePath, reqTextPath);
            Assert.IsTrue(isPackageMissing, "Expected missing packages because \"cookies\" is incorrect version");
            File.Delete(reqTextPath);

            File.WriteAllLines(reqTextPath,
                new string[] { "Cookies>=1.0", "Cookies>=100.0" });
            isPackageMissing = await PipRequirementsUtils.DetectMissingPackagesAsync(interpreterExePath, reqTextPath);
            Assert.IsTrue(isPackageMissing, "Expected missing package because \"cookies\" has a valid and invalid package version");
            File.Delete(reqTextPath);
        }
    }
}
