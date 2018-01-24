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

using System;
using System.Net;
using System.Net.Sockets;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace AnalysisTests {
    [TestClass]
    public class SocketUtilsTests {
        [TestMethod, Priority(0)]
        public void GetFreePorts() {
            // Sanity test, no randomization, no excluding active ports
            Assert.AreEqual(50000, SocketUtils.WithFreePort(p => p, 50000, 60000, false, false));

            // Expect exception to be silenced when a later port succeeds
            Assert.AreEqual(50001, SocketUtils.WithFreePort(p => {
                if (p == 50000) throw new InvalidCastException();
                return p;
            }, 50000, 60000, false, false));

            // Expect last exception when no ports succeed
            AssertUtil.Throws<InvalidCastException>(() => SocketUtils.WithFreePort(p => (object)null ?? throw new InvalidCastException(), 50000, 60000, false, false));

            // Expect ArgumentException for invalid range
            AssertUtil.Throws<ArgumentException>(() => SocketUtils.WithFreePort(p => p, 1, 1));

            // Expect InvalidOperationException when all ports in use
            var listener = SocketUtils.WithFreePort(p => {
                var s = new TcpListener(IPAddress.Loopback, p);
                s.Start();
                return s;
            });
            try {
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                AssertUtil.Throws<InvalidOperationException>(() => SocketUtils.WithFreePort(p => p, port, port + 1));
            } finally {
                listener.Stop();
            }
        }
    }
}
