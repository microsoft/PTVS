// ----------------------------------------------------------------------------------
//
// Copyright 2011 Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------

using System.IO;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Scaffolding;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PythonTools.AzureDeploymentCmdlets.Test.Tests.Scaffolding
{
    [TestClass]
    public class ScaffoldTests : TestBase
    {
        [TestMethod]
        public void ParseTests()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                string path = files.CreateEmptyFile("Scaffold.xml");
                File.WriteAllText(path, Properties.Resources.ValidScaffoldXml);

                Scaffold scaffold = Scaffold.Parse(path);

                Assert.AreEqual(scaffold.Files.Count, 6);
                Assert.AreEqual(scaffold.Files[0].PathExpression, "modules\\.*");
                Assert.AreEqual(scaffold.Files[1].Path, @"bin/node123dfx65.exe");
                Assert.AreEqual(scaffold.Files[1].TargetPath, @"/bin/node.exe");
                Assert.AreEqual(scaffold.Files[2].Path, @"bin/iisnode.dll");
                Assert.AreEqual(scaffold.Files[3].Path, @"bin/setup.cmd");
                Assert.AreEqual(scaffold.Files[4].Path, "Web.config");
                Assert.AreEqual(scaffold.Files[4].Rules.Count, 1);
                Assert.AreEqual(scaffold.Files[5].Path, "WebRole.xml");
                Assert.AreEqual(scaffold.Files[5].Copy, false);
                Assert.AreEqual(scaffold.Files[5].Rules.Count, 1);
            }
        }
    }
}
