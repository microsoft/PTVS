/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;

namespace PythonToolsTests {
    [TestClass]
    public class PythonProjectTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy(includeTestData: false);
        }

        public TestContext TestContext { get; set; }

        [TestMethod, Priority(0)]
        public void MergeRequirements() {
            // Comments should be preserved, only package specs should change.
            AssertUtil.AreEqual(
                InterpretersNode.MergeRequirements(new[] {
                    "a # with a comment",
                    "B==0.2",
                    "# just a comment B==01234",
                    "",
                    "x < 1",
                    "d==1.0 e==2.0 f==3.0"
                }, new[] {
                    "b==0.1",
                    "a==0.2",
                    "c==0.3",
                    "e==4.0",
                    "x==0.8"
                }, false),
                "a==0.2 # with a comment",
                "b==0.1",
                "# just a comment B==01234",
                "",
                "x==0.8",
                "d==1.0 e==4.0 f==3.0"
            );

            // addNew is true, so the c==0.3 should be added.
            AssertUtil.AreEqual(
                InterpretersNode.MergeRequirements(new[] {
                    "a # with a comment",
                    "b==0.2",
                    "# just a comment B==01234"
                }, new[] {
                    "B==0.1",   // case is updated
                    "a==0.2",
                    "c==0.3"
                }, true),
                "a==0.2 # with a comment",
                "B==0.1",
                "# just a comment B==01234",
                "c==0.3"
            );

            // No existing entries, so the new ones are sorted and returned.
            AssertUtil.AreEqual(
                InterpretersNode.MergeRequirements(null, new[] {
                    "b==0.2",
                    "a==0.1",
                    "c==0.3"
                }, false),
                "a==0.1",
                "b==0.2",
                "c==0.3"
            );

            // Check all the inequalities
            const string inequalities = "<=|>=|<|>|!=|==";
            AssertUtil.AreEqual(
                InterpretersNode.MergeRequirements(
                    inequalities.Split('|').Select(s => "a " + s + " 1.2.3"),
                    new[] { "a==0" },
                    false
                ),
                inequalities.Split('|').Select(_ => "a==0").ToArray()
            );
        }
    }
}
