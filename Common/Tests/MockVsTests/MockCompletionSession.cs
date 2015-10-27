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
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudioTools.MockVsTests {
    class MockCompletionSession : ICompletionSession {
        private bool _dismissed, _started;
        private readonly ITextView _view;
        private readonly ReadOnlyObservableCollection<CompletionSet> _sets;
        private readonly ITrackingPoint _triggerPoint;
        private readonly PropertyCollection _properties = new PropertyCollection();
        private CompletionSet _active;

        public MockCompletionSession(ITextView view, ObservableCollection<CompletionSet> sets, ITrackingPoint triggerPoint) {
            _view = view;
            sets.CollectionChanged += sets_CollectionChanged;
            _triggerPoint = triggerPoint;
            _sets = new ReadOnlyObservableCollection<CompletionSet>(sets);
        }

        void sets_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
            if (e.Action != NotifyCollectionChangedAction.Add) {
                throw new NotImplementedException();
            }
            if (_active == null) {
                _active = _sets[0];
            }
        }

        public void Commit() {
            if (SelectedCompletionSet != null) {
                Completion selectedCompletion = SelectedCompletionSet.SelectionStatus.Completion;
                if (selectedCompletion != null && selectedCompletion.InsertionText != null) {
                    ITrackingSpan applicableTo = SelectedCompletionSet.ApplicableTo;
                    ITextBuffer buffer = applicableTo.TextBuffer;
                    ITextSnapshot snapshot = buffer.CurrentSnapshot;
                    SnapshotSpan replaceSpan = applicableTo.GetSpan(snapshot);

                    buffer.Replace(replaceSpan.Span, selectedCompletion.InsertionText);
                    TextView.Caret.EnsureVisible();
                }
            }

            var committed = Committed;
            if (committed != null) {
                committed(this, EventArgs.Empty);
            }
            Dismiss();
        }

        public event EventHandler Committed;

        public System.Collections.ObjectModel.ReadOnlyObservableCollection<CompletionSet> CompletionSets {
            get { return _sets; }
        }

        public void Filter() {
            foreach (CompletionSet completionSet in _sets) {
                completionSet.Filter();
            }

            // Now that we're through, see if there's a better match out there.
            this.Match();
        }

        public bool IsStarted {
            get { return _started; }
        }

        public CompletionSet SelectedCompletionSet {
            get {
                return _active;
            }
            set {
                _active = value;
            }
        }

        public event EventHandler<ValueChangedEventArgs<CompletionSet>> SelectedCompletionSetChanged {
            add { throw new NotImplementedException(); }
            remove { throw new NotImplementedException(); }
        }

        public void Collapse() {
            throw new NotImplementedException();
        }

        public void Dismiss() {
            _dismissed = true;
            var dismissed = Dismissed;
            if (dismissed != null) {
                dismissed(this, EventArgs.Empty);
            }
        }

        public event EventHandler Dismissed;

        public VisualStudio.Text.SnapshotPoint? GetTriggerPoint(VisualStudio.Text.ITextSnapshot textSnapshot) {
            return GetTriggerPoint(textSnapshot.TextBuffer).GetPoint(textSnapshot);
        }

        public VisualStudio.Text.ITrackingPoint GetTriggerPoint(VisualStudio.Text.ITextBuffer textBuffer) {
            if (textBuffer == _triggerPoint.TextBuffer) {
                return _triggerPoint;
            }
            throw new NotImplementedException();
        }

        public bool IsDismissed {
            get { return _dismissed; }
        }

        public bool Match() {
            foreach (CompletionSet completionSet in _sets) {
                completionSet.SelectBestMatch();
            }

            return true;
        }

        public IIntellisensePresenter Presenter {
            get { throw new NotImplementedException(); }
        }

        public event EventHandler PresenterChanged {
            add { throw new NotImplementedException(); }
            remove { throw new NotImplementedException(); }
        }

        public void Recalculate() {
            throw new NotImplementedException();
        }

        public event EventHandler Recalculated {
            add { throw new NotImplementedException(); }
            remove { throw new NotImplementedException(); }
        }

        public void Start() {
            _started = true;
        }

        public VisualStudio.Text.Editor.ITextView TextView {
            get { return _view; }
        }

        public VisualStudio.Utilities.PropertyCollection Properties {
            get { return _properties; }
        }
    }
}
