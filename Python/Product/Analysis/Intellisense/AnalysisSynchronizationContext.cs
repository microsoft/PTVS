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

using System;
using System.Threading;
using Microsoft.PythonTools.Analysis;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Provides the synchronization context for our analysis.  This enables working with
    /// System.Threading.Tasks to post work back onto the analysis queue thread in a simple
    /// manner.
    /// </summary>
    class AnalysisSynchronizationContext : SynchronizationContext {
        private readonly AnalysisQueue _queue;
        [ThreadStatic]
        internal static AutoResetEvent _waitEvent;

        public AnalysisSynchronizationContext(AnalysisQueue queue) {
            _queue = queue;
        }

        public override void Post(SendOrPostCallback d, object state) {
            try {
                _queue.Enqueue(new AnalysisItem(d, state), AnalysisPriority.High);
            } catch (ObjectDisposedException) {
            }
        }

        public override void Send(SendOrPostCallback d, object state) {
            if (_waitEvent == null) {
                _waitEvent = new AutoResetEvent(false);
            }
            var waitable = new WaitableAnalysisItem(d, state);
            try {
                _queue.Enqueue(waitable, AnalysisPriority.High);
                _waitEvent.WaitOne();
            } catch (ObjectDisposedException) {
            }
        }

        class AnalysisItem : IAnalyzable {
            private SendOrPostCallback _delegate;
            private object _state;

            public AnalysisItem(SendOrPostCallback callback, object state) {
                _delegate = callback;
                _state = state;
            }

            #region IAnalyzable Members

            public virtual void Analyze(CancellationToken cancel) {
                _delegate(_state);
            }

            #endregion
        }

        class WaitableAnalysisItem : AnalysisItem {
            public WaitableAnalysisItem(SendOrPostCallback callback, object state)
                : base(callback, state) {
            }

            public override void Analyze(CancellationToken cancel) {
                base.Analyze(cancel);
                AnalysisSynchronizationContext._waitEvent.Set();
            }
        }
    }
}
