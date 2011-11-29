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

using System.Threading;
using Microsoft.PythonTools.Analysis;
using System;

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
            _queue.Enqueue(new AnalysisItem(d, state), AnalysisPriority.High);
        }

        public override void Send(SendOrPostCallback d, object state) {
            if (_waitEvent == null) {
                _waitEvent = new AutoResetEvent(false);
            }
            var waitable = new WaitableAnalysisItem(d, state);
            _queue.Enqueue(waitable, AnalysisPriority.High);
            _waitEvent.WaitOne();
        }

        class AnalysisItem : IAnalyzable {
            private SendOrPostCallback _delegate;
            private object _state;

            public AnalysisItem(SendOrPostCallback callback, object state) {
                _delegate = callback;
                _state = state;
            }

            #region IAnalyzable Members

            public virtual void Analyze() {
                _delegate(_state);
            }

            #endregion
        }

        class WaitableAnalysisItem : AnalysisItem {
            public WaitableAnalysisItem(SendOrPostCallback callback, object state)
                : base(callback, state) {
            }

            public override void Analyze() {
                base.Analyze();
                AnalysisSynchronizationContext._waitEvent.Set();
            }
        }
    }
}
