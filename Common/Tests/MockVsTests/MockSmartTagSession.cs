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
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudioTools.MockVsTests {
#if DEV14_OR_LATER
#pragma warning disable 0618
#endif

    class MockSmartTagSession : ISmartTagSession {
        private ITrackingSpan _applicableToSpan;
        private ImageSource _iconSource;
        private SmartTagState _state;
        private ITrackingSpan _tagSpan;

        private readonly MockSmartTagBroker _broker;
        private readonly ObservableCollection<SmartTagActionSet> _actionSets =
            new ObservableCollection<SmartTagActionSet>();
        private readonly PropertyCollection _properties = new PropertyCollection();

        public MockSmartTagSession(MockSmartTagBroker broker) {
            _broker = broker;
        }

        public ObservableCollection<SmartTagActionSet> ActionSets {
            get { return _actionSets; }
        }

        ReadOnlyObservableCollection<SmartTagActionSet> ISmartTagSession.ActionSets {
            get { return new ReadOnlyObservableCollection<SmartTagActionSet>(_actionSets); }
        }

        private void Raise(EventHandler evt) {
            if (evt != null) {
                evt(this, EventArgs.Empty);
            }
        }

        public ITrackingSpan ApplicableToSpan {
            get { return _applicableToSpan; }
            set {
                if (_applicableToSpan != value) {
                    _applicableToSpan = value;
                    Raise(ApplicableToSpanChanged);
                }
            }
        }

        public event EventHandler ApplicableToSpanChanged;

        public ImageSource IconSource {
            get { return _iconSource; }
            set {
                if (_iconSource != value) {
                    _iconSource = value;
                    Raise(IconSourceChanged);
                }
            }
        }

        public event EventHandler IconSourceChanged;

        public SmartTagState State {
            get { return _state; }
            set {
                if (_state != value) {
                    _state = value;
                    Raise(StateChanged);
                }
            }
        }

        public event EventHandler StateChanged;

        public ITrackingSpan TagSpan {
            get { return _tagSpan; }
            set {
                if (_tagSpan != value) {
                    _tagSpan = value;
                    Raise(TagSpanChanged);
                }
            }
        }

        public event EventHandler TagSpanChanged;

        public string TagText { get; set; }

        public SmartTagType Type {
            get;
            set;
        }

        public void Collapse() {
            throw new NotImplementedException();
        }

        public void Dismiss() {
            if (IsDismissed == false) {
                IsDismissed = true;
                Raise(Dismissed);
            }
        }

        public event EventHandler Dismissed;

        public SnapshotPoint? GetTriggerPoint(ITextSnapshot textSnapshot) {
            return TriggerPoint.GetPoint(textSnapshot);
        }

        public ITrackingPoint GetTriggerPoint(ITextBuffer textBuffer) {
            return TriggerPoint;
        }

        public ITrackingPoint TriggerPoint { get; set; }

        public bool IsDismissed { get; set; }

        public bool Match() {
            throw new NotImplementedException();
        }

        public IIntellisensePresenter Presenter {
            get { throw new NotImplementedException(); }
            set { Raise(PresenterChanged); }
        }

        public event EventHandler PresenterChanged;

        public void Recalculate() {
            if (_broker != null) {
                var sources = new List<ISmartTagSource>();
                foreach (var p in _broker.SourceProviders) {
                    var source = p.TryCreateSmartTagSource(TextView.TextBuffer);
                    if (source != null) {
                        sources.Add(source);
                    }
                }

                var sets = new List<SmartTagActionSet>();
                foreach (var source in sources) {
                    source.AugmentSmartTagSession(this, sets);
                }

                _actionSets.Clear();
                foreach (var set in sets) {
                    if (set.Actions.Any()) {
                        _actionSets.Add(set);
                    }
                }
            }

            if (!_actionSets.Any()) {
                Dismiss();
            } else {
                Raise(Recalculated);
            }
        }

        public event EventHandler Recalculated;

        public void Start() {
            Recalculate();
        }

        public ITextView TextView { get; set; }

        public PropertyCollection Properties {
            get { return _properties; }
        }
    }
}
