// Visual Studio Shared Project
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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Net;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnalysisTests {
    [TestClass]
    public class SocketUtilsTests {
        [TestMethod, Priority(0)]
        public void GetRandomPortListener() {
            var listener = SocketUtils.GetRandomPortListener(IPAddress.Loopback, out int port);
            try {
                Assert.IsNotNull(listener);
                Assert.AreEqual(port, ((IPEndPoint)listener.LocalEndpoint).Port);
                Assert.IsTrue(port >= 49152 && port < 65536);
            } finally {
                listener?.Stop();
            }
        }
    }
}
