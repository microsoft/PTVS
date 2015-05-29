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

#if !DEV14_OR_LATER
using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.PythonTools.Intellisense {
    class SmartTagController : IIntellisenseController {
        private readonly ISmartTagBroker _broker;
        private readonly ITextView _textView;
        private ISmartTagSession _curSession;
        private string _curSessionText;
        internal SmartTagAugmentTask _curTask;

        public SmartTagController(ISmartTagBroker broker, ITextView textView) {
            _broker = broker;
            _textView = textView;
            _textView.Caret.PositionChanged += Caret_PositionChanged;
        }

        private void Caret_PositionChanged(object sender, CaretPositionChangedEventArgs e) {
            var session = _curSession;
            if (session == null || session.IsDismissed) {
                return;
            }

            var snapshot = _textView.TextSnapshot;
            var caret = e.NewPosition.Point.GetPoint(snapshot, PositionAffinity.Successor);
            if (caret.HasValue && session.ApplicableToSpan.GetSpan(snapshot).Contains(caret.Value)) {
                return;
            }

            session.Dismiss();
            Interlocked.CompareExchange(ref _curSession, null, session);
        }

        static internal SmartTagController CreateInstance(ISmartTagBroker broker, ITextView textView, IList<ITextBuffer> subjectBuffers) {
            Type key = typeof(SmartTagController);

            SmartTagController controller = null;
            if (textView.Properties.TryGetProperty(key, out controller)) {
                return controller;
            }

            controller = new SmartTagController(broker, textView);
            textView.Properties.AddProperty(key, controller);

            foreach (var buffer in subjectBuffers) {
                buffer.ChangedLowPriority += controller.SubjectBufferChangedLowPriority;
            }

            return controller;
        }

        public void ConnectSubjectBuffer(ITextBuffer subjectBuffer) {
            subjectBuffer.ChangedLowPriority += SubjectBufferChangedLowPriority;
        }

        void SubjectBufferChangedLowPriority(object sender, TextContentChangedEventArgs e) {
            var session = _curSession;
            if (session == null || session.IsDismissed) {
                return;
            }

            session.Dismiss();
            Interlocked.CompareExchange(ref _curSession, null, session);
        }

        public void Detach(ITextView textView) {
            _textView.Caret.PositionChanged -= Caret_PositionChanged;
        }

        public void DisconnectSubjectBuffer(ITextBuffer subjectBuffer) {
            subjectBuffer.ChangedLowPriority -= SubjectBufferChangedLowPriority;
        }

        internal void ShowSmartTag() {
            if (_textView.IsClosed) {
                // pending call from the idle loop but the editor was closed
                return;
            }
            var model = _textView.TextViewModel;
            if (model == null) {
                // may have been nulled out if we're racing with close
                return;
            }

            var session = _curSession;
            var task = Volatile.Read(ref _curTask);
            if (session != null && !session.IsDismissed) {
                // Check whether the task has completed, and if so, recalculate
                // the contents.
                if (task != null) {
                    if (!task.IsAborted) {
                        session.Recalculate();
                        return;
                    }
                    // Task is aborted, so clear it out unless someone has
                    // already switched it up
                    if (Interlocked.CompareExchange(ref _curTask, null, task) != task) {
                        return;
                    }
                    // Otherwise, dismiss the session so we can create a new one
                    session.Dismiss();
                } else {
                    // Task was completed, so leave it alone
                    return;
                }
            }

            // Figure out the point in the buffer where we are triggering.
            // We need to use the view's data buffer as the source location
            var snapshot = model.DataBuffer.CurrentSnapshot;
            var caretPoint = _textView.Caret.Position.Point.GetPoint(snapshot, PositionAffinity.Successor);
            if (!caretPoint.HasValue) {
                return;
            }

            var triggerPoint = snapshot.CreateTrackingPoint(caretPoint.Value, PointTrackingMode.Positive);
            var newSession = _broker.CreateSmartTagSession(
                _textView,
                SmartTagType.Factoid,
                triggerPoint,
                SmartTagState.Collapsed
            );
            newSession.Properties.AddProperty(typeof(SmartTagController), this);

            var orig = Interlocked.CompareExchange(ref _curSession, newSession, session);
            if (orig == session) {
                newSession.Start();
                if (newSession.ApplicableToSpan != null) {
                    _curSessionText = newSession.ApplicableToSpan.GetText(snapshot);
                }
            } else {
                newSession.Dismiss();
            }
        }
    }
}
#endif
