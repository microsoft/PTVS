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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

// #define WAIT_FOR_DEBUGGER

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis.LanguageServer;
using Microsoft.PythonTools.VsCode.Services;
using StreamJsonRpc;

namespace Microsoft.PythonTools.VsCode {
    internal static class Program {
        public static void Main(string[] args) {
            CheckDebugMode();
            using (CoreShell.Create()) {
                var services = CoreShell.Current.ServiceManager;

                using (var cin = Console.OpenStandardInput())
                using (var cout = Console.OpenStandardOutput())
                using (var server = new LanguageServer())
                using (var rpc = new JsonRpc(cout, cin, server)) {
                    var ui = new UIService(rpc);
                    rpc.SynchronizationContext = new SingleThreadSynchronizationContext(ui);

                    services.AddService(ui);
                    services.AddService(new TelemetryService(rpc));
                    var token = server.Start(services, rpc);
                    rpc.StartListening();

                    // Wait for the "shutdown" request.
                    token.WaitHandle.WaitOne();
                }
            }
        }

        private static void CheckDebugMode() {
#if WAIT_FOR_DEBUGGER
            var start = DateTime.Now;
            while (!System.Diagnostics.Debugger.IsAttached) {
                System.Threading.Thread.Sleep(1000);
                if ((DateTime.Now - start).TotalMilliseconds > 15000) {
                    break;
                }
            }
#endif
        }

        private sealed class SingleThreadSynchronizationContext : SynchronizationContext, IDisposable {
            private readonly ConcurrentQueue<Tuple<SendOrPostCallback, object>> _queue = new ConcurrentQueue<Tuple<SendOrPostCallback, object>>();
            private readonly UIService _ui;
            private readonly ManualResetEventSlim _workAvailable = new ManualResetEventSlim(false);
            private readonly CancellationTokenSource _cts = new CancellationTokenSource();

            public SingleThreadSynchronizationContext(UIService ui) {
                _ui = ui;
                Task.Run(() => QueueWorker());
            }

            public override void Post(SendOrPostCallback d, object state) {
                _queue.Enqueue(new Tuple<SendOrPostCallback, object>(d, state));
                _workAvailable.Set();
            }

            public void Dispose() {
                _cts.Cancel();
            }

            private void QueueWorker() {
                while(true) {
                    _workAvailable.Wait(_cts.Token);
                    if(_cts.IsCancellationRequested) {
                        break;
                    }
                    while(_queue.TryDequeue(out var t)) {
                        try {
                            t.Item1(t.Item2);
                        } catch(Exception ex) {
                            _ui.LogMessage($"Exception processing request: {ex.Message}", MessageType.Error);
                        }
                    }
                    _workAvailable.Reset();
                }
            }
        }
    }
}