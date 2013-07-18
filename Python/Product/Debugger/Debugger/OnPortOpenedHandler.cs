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
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PythonTools {
    internal class OnPortOpenedHandler {
        class OnPortOpenedInfo {
            public readonly int Port;
            public readonly TimeSpan Timeout;
            public readonly int Sleep;
            public readonly Func<bool> ShortCircuitPredicate;
            public readonly Action Action;
            public readonly DateTime StartTime;

            public OnPortOpenedInfo(
                int port,
                int? timeout = null,
                int? sleep = null,
                Func<bool> shortCircuitPredicate = null,
                Action action = null
            ) {
                Port = port;
                Timeout = TimeSpan.FromMilliseconds(timeout ?? 60000);  // 1 min timeout
                Sleep = sleep ?? 500;                                   // 1/2 second sleep
                ShortCircuitPredicate = shortCircuitPredicate ?? (() => false);
                Action = action ?? (() => { });
                StartTime = System.DateTime.Now;
            }
        }

        internal static void CreateHandler(
            int port,
            int? timeout = null,
            int? sleep = null,
            Func<bool> shortCircuitPredicate = null,
            Action action = null
        ) {
            ThreadPool.QueueUserWorkItem(
                OnPortOpened,
                new OnPortOpenedInfo(
                    port,
                    timeout,
                    sleep,
                    shortCircuitPredicate,
                    action
                )
            );
        }

        private static void OnPortOpened(object infoObj) {
            var info = (OnPortOpenedInfo)infoObj;

            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)) {
                socket.Blocking = true;
                while (true) {
                    // Short circuit
                    if (info.ShortCircuitPredicate()) {
                        return;
                    }

                    // Try connect
                    try {
                        socket.Connect(IPAddress.Loopback, info.Port);
                        break;
                    } catch {
                        // Connect failure
                        // Fall through
                    }

                    // Timeout
                    if ((System.DateTime.Now - info.StartTime) >= info.Timeout) {
                        break;
                    }

                    // Sleep
                    System.Threading.Thread.Sleep(info.Sleep);
                }
            }

            // Launch browser (if not short-circuited)
            if (!info.ShortCircuitPredicate()) {
                info.Action();
            }
        }
    }
}
