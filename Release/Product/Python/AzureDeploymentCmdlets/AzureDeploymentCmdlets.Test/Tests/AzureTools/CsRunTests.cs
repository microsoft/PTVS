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

using Microsoft.PythonTools.AzureDeploymentCmdlets.AzureTools;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PythonTools.AzureDeploymentCmdlets.Test.Tests.AzureTools
{
    [TestClass]
    public class CsRunTests : TestBase
    {
        [TestMethod]
        public void RoleInfoIsExtractedFromEmulatorOutput()
        {
            var dummyEmulatorOutput = "Exported interface at http://127.0.0.1:81/.\r\nExported interface at tcp://127.0.0.1:8080/.";
            var output = CsRun.GetRoleInfoMessage(dummyEmulatorOutput);
            Assert.IsTrue(output.Contains("Role is running at http://127.0.0.1:81"));
            Assert.IsTrue(output.Contains("Role is running at tcp://127.0.0.1:8080"));
        }
    }
}