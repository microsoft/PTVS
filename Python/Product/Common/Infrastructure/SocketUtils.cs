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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.ExceptionServices;

namespace Microsoft.PythonTools.Infrastructure {
    static class SocketUtils {
        private static readonly Random _freePortRandom = new Random();

        public static T WithFreePort<T>(Func<int, T> action, int minimumPort = 49152, int maximumPort = 65536, bool excludeActiveConnections = true, bool randomStart = true) {
            ExceptionDispatchInfo edi = null;
            int range = maximumPort - minimumPort;
            if (range <= 0) {
                throw new ArgumentException("maximumPort must be larger than minimumPort");
            }

            var skip = new HashSet<int>();
            if (excludeActiveConnections) {
                var netProp = IPGlobalProperties.GetIPGlobalProperties();
                skip.UnionWith(netProp.GetActiveTcpConnections().Select(p => p.LocalEndPoint.Port));
                skip.UnionWith(netProp.GetActiveTcpListeners().Select(p => p.Port));
            }

            int start = randomStart ? _freePortRandom.Next(range) : 0;
            for (int i = 0; i < range; ++i) {
                int port = (i + start) % range + minimumPort;
                Debug.Assert(port >= minimumPort && port <= maximumPort);
                if (skip.Contains(port)) {
                    continue;
                }
                try {
                    return action(port);
                } catch (Exception ex) {
                    edi = ExceptionDispatchInfo.Capture(ex);
                }
            }
            edi?.Throw();
            throw new InvalidOperationException("No free ports");
        }

    }
}
