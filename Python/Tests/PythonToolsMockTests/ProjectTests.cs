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

using System.Linq;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.MockVsTests;
using TestUtilities;
using TestUtilities.SharedProject;

namespace PythonToolsMockTests {
    [TestClass]
    public class ProjectTests : SharedProjectTest {
        public static ProjectType PythonProject = ProjectTypes.First(x => x.ProjectExtension == ".pyproj");

        [TestMethod]
        public void BasicProjectTest() {
            UIThread.InitializeAndNeverInvoke();

            var sln = new ProjectDefinition(
                "HelloWorld",
                PythonProject,
                Compile("server")
            ).Generate();
            
            var vs = sln.ToMockVs();
            Assert.IsFalse(vs.WaitForItem("HelloWorld", "Python Environments").IsNull);
            Assert.IsFalse(vs.WaitForItem("HelloWorld", "References").IsNull);
            Assert.IsFalse(vs.WaitForItem("HelloWorld", "Search Paths").IsNull);
            Assert.IsFalse(vs.WaitForItem("HelloWorld", "server.py").IsNull);
            var view = vs.OpenItem("HelloWorld", "server.py");

            view.Type("import ");

            var session = view.TopSession as ICompletionSession;

            AssertUtil.Contains(session.Completions(), "sys");
        }
    }
}
