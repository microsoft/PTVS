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

using System.Linq;
using System.Text.RegularExpressions;
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
                    "f==3.0"
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
                "f==3.0"
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
        public void FindRequirementsRegexTest() {
            var r = PipRequirementsUtils.FindRequirementRegex;
            Assert.IsTrue(r.Matches("abcd").Cast<Match>().First().Groups["name"].Value.Equals("abcd"));
            Assert.IsTrue(r.Matches(" abcd  #this is a comment").Cast<Match>().First().Groups["name"].Value.Equals("abcd"));
            Assert.IsTrue(r.Matches("abcd==1").Cast<Match>().First().Groups["name"].Value.Equals("abcd"));
            Assert.IsTrue(r.Matches("abcd == 1").Cast<Match>().First().Groups["name"].Value.Equals("abcd"));
            Assert.IsTrue(r.Matches("abcd >= 1, abcde<=2").Cast<Match>().First().Groups["name"].Value.Equals("abcd"));
            Assert.IsTrue(r.Matches("abcd    >=   1.0").Cast<Match>().First().Groups["name"].Value.Equals("abcd"));
            Assert.IsTrue(r.Matches("abcd >= 1.0, <= 2.0,!=1.5").Cast<Match>().First().Groups["name"].Value.Equals("abcd"));

        }

        [TestMethod, Priority(0)]
        public void AnyPackageMissing() {
            // AnyPackageMissing only checks if a package is listed or not
            // It does NOT compare version numbers.
            Assert.IsFalse(PipRequirementsUtils.AnyPackageMissing(
                new[] { "Django" },
                new[] { new PackageSpec("Django", "1.11") }
            ));

            Assert.IsFalse(PipRequirementsUtils.AnyPackageMissing(
                new[] { "django" },
                new[] { new PackageSpec("Django", "1.11") }
            ));

            Assert.IsFalse(PipRequirementsUtils.AnyPackageMissing(
                new[] { "Flask", "Flask-Admin" },
                new[] { new PackageSpec("Flask", "1.0.2"), new PackageSpec("Flask-Admin", "1.5.2") }
            ));

            Assert.IsFalse(PipRequirementsUtils.AnyPackageMissing(
                new[] { "django >=1.9, <2" },
                new[] { new PackageSpec("Django", "1.11") }
            ));

            Assert.IsFalse(PipRequirementsUtils.AnyPackageMissing(
                new[] { "django==1.11" },
                new[] { new PackageSpec("Django", "1.11") }
            ));

            Assert.IsFalse(PipRequirementsUtils.AnyPackageMissing(
                new[] { "django >=1.9, <2" },
                new[] { new PackageSpec("Django", "1.8") }
            ));

            Assert.IsFalse(PipRequirementsUtils.AnyPackageMissing(
                new[] { "django >= 2.1.5,<3.0 " },
                new[] { new PackageSpec("Django", "1.8") }
            ));

            Assert.IsFalse(PipRequirementsUtils.AnyPackageMissing(
                new[] { "django >= 2.1.5, <= 3.0 " },
                new[] { new PackageSpec("Django", "2.2") }
            ));
            Assert.IsFalse(PipRequirementsUtils.AnyPackageMissing(
                new[] { "django>=2.1.5,<=3.0#comment" },
                new[] { new PackageSpec("Django", "2.2") }
            ));

            Assert.IsTrue(PipRequirementsUtils.AnyPackageMissing(
                new[] { "django >= 2.1.5,<3.0" },
                new PackageSpec[0]
            ));

            Assert.IsTrue(PipRequirementsUtils.AnyPackageMissing(
                new[] { "django >= 2.1.5,<3.0" },
                new[] { new PackageSpec("flask", "2.2") }
            ));

            Assert.IsTrue(PipRequirementsUtils.AnyPackageMissing(
                new[] { "Flask" },
                new PackageSpec[0]
            ));

            Assert.IsTrue(PipRequirementsUtils.AnyPackageMissing(
                new[] { "Flask", "Flask-Admin" },
                new[] { new PackageSpec("Flask", "1.0.2") }
            ));
        }
    }
}
