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

using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace VSInterpretersTests {
    [TestClass]
    public class PipRequirementsUtilsTests {
        [TestMethod, Priority(0)]
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

        [TestMethod, Priority(0)]
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

        [TestMethod, Priority(0)]
        public void PackagesNotMissing() {
            // AnyPackageMissing only checks if a package is listed or not
            // It does NOT compare version numbers.
            Assert.IsFalse(PipRequirementsUtils.AnyPackageMissing(
                new[] { "#comment Flask" },
                new PackageSpec[0]
            ));

            Assert.IsFalse(PipRequirementsUtils.AnyPackageMissing(
                new[] { "Django#comment Flask" },
                new[] { new PackageSpec("DJANGO", "1.2") }
            ));

            Assert.IsFalse(PipRequirementsUtils.AnyPackageMissing(
                new[] { "django==1.1" },
                new[] { new PackageSpec("Flask", "2.0"),
                        new PackageSpec("Django", "1.1") }
            ));

            Assert.IsFalse(PipRequirementsUtils.AnyPackageMissing(
                new[] { "  django>=1.1,<1.9#comment    #comment" },
                new[] { new PackageSpec("DJANGO", "1.5") }
            ));

            Assert.IsFalse(PipRequirementsUtils.AnyPackageMissing(
                new[] { "django  >=   1.2 ,   < 1.98    #   comment  " },
                new[] { new PackageSpec("Django", "1.8") }
            ));

            Assert.IsFalse(PipRequirementsUtils.AnyPackageMissing(
                new[] { "Flask= =1.1",
                        "FlaskAdmin < = 3.9",
                        "Django> = 1.4 , <  = 1.9" },
                new[] { new PackageSpec("Django", "1.8"),
                        new PackageSpec("Flask", "2.1"),
                        new PackageSpec("FlaskAdmin", "3.2") }
            ));

            Assert.IsFalse(PipRequirementsUtils.AnyPackageMissing(
                new[] { "django >= 1.1.5, flask<= 3.0 " }, //Should only attempt to match "Django"
                new[] { new PackageSpec("Django", "1.2") }
            ));

            Assert.IsFalse(PipRequirementsUtils.AnyPackageMissing(
                new[] { "-r user/ptvs/requirements.txt",
                        "git+https://myvcs.com/some_dependency",
                        "#Django" },
                new PackageSpec[0]
            ));

            Assert.IsFalse(PipRequirementsUtils.AnyPackageMissing(
                new[] { "-r Django asd mor ereqs.txt",
                        "git+ Django https://myvcs.com/some_d ependency  @sometag#egg=S    omeDependency" },
                new PackageSpec[0]
            ));

            Assert.IsFalse(PipRequirementsUtils.AnyPackageMissing(
                new[] { "-r asd mor ereqs.txt",
                        "django#Bottle",
                        "git+https://myvcs.com/some_dependency",
                        "Flask#flaskAdmin" },
                new[] { new PackageSpec("Django", "1.2"),
                        new PackageSpec("Flask", "2.2") }
            ));

            Assert.IsFalse(PipRequirementsUtils.AnyPackageMissing(
                new[] { "git", "requirementstxt" },
                new[] { new PackageSpec("git", "6.0"),
                        new PackageSpec("requirementstxt", "7.0")}
            ));
        }

        [TestMethod, Priority(0)]
        public void PackagesMissing() {
            // AnyPackageMissing only checks if a package is listed or not
            // It does NOT compare version numbers.
            Assert.IsTrue(PipRequirementsUtils.AnyPackageMissing(
                new[] { "Django" },
                new PackageSpec[0]
            ));

            Assert.IsTrue(PipRequirementsUtils.AnyPackageMissing(
                new[] { "Django#Flask" },
                new[] { new PackageSpec("Flask", "2.0") }
            ));

            Assert.IsTrue(PipRequirementsUtils.AnyPackageMissing(
                new[] { "Fla sk" },
                new[] { new PackageSpec("Flask", "2.0") }
            ));

            Assert.IsTrue(PipRequirementsUtils.AnyPackageMissing(
                new[] { "  django>=1.1,<1.9#comment" },
                new PackageSpec[0]
            ));

            Assert.IsTrue(PipRequirementsUtils.AnyPackageMissing(
                new[] { "Django  >=   1.2  ,   < 1.98 ",
                        "Flask  >=   2.2 ,   < 2.8 " },
                new[] { new PackageSpec("Flask", "2.8") }
            ));

            Assert.IsTrue(PipRequirementsUtils.AnyPackageMissing(
                new[] { "django >= 1.1.5, flask<= 3.0 " }, //Should only attempt to match "Django"
                new[] { new PackageSpec("Flask", "2.2") }
            ));

            Assert.IsTrue(PipRequirementsUtils.AnyPackageMissing(
                new[] { "Flask",
                        "FlaskAdmin" },
                new[] { new PackageSpec("Flask", "1.0.2") }
            ));
        }


    }
}
