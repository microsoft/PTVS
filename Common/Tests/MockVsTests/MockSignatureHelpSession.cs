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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

namespace Microsoft.VisualStudioTools.MockVsTests
{
    class MockSignatureHelpSession : ISignatureHelpSession
    {
        private bool _dismissed;
        private readonly ITextView _view;
        private readonly ReadOnlyObservableCollection<ISignature> _sigs;
        private readonly ITrackingPoint _triggerPoint;
        private readonly PropertyCollection _properties = new PropertyCollection();
        private ISignature _active;

        public MockSignatureHelpSession(ITextView view, ObservableCollection<ISignature> sigs, ITrackingPoint triggerPoint)
        {
            _view = view;
            sigs.CollectionChanged += sigs_CollectionChanged;
            _triggerPoint = triggerPoint;
            _sigs = new ReadOnlyObservableCollection<ISignature>(sigs);
        }

        void sigs_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Add)
            {
                throw new NotImplementedException();
            }
            if (_active == null)
            {
                _active = _sigs[0];
            }
        }

        public ISignature SelectedSignature
        {
            get
            {
                return _active;
            }
            set
            {
                _active = value;
            }
        }

        public event EventHandler<SelectedSignatureChangedEventArgs> SelectedSignatureChanged
        {
            add { throw new NotImplementedException(); }
            remove { throw new NotImplementedException(); }
        }

        public System.Collections.ObjectModel.ReadOnlyObservableCollection<ISignature> Signatures
        {
            get { return _sigs; }
        }

        public void Collapse()
        {
            throw new NotImplementedException();
        }

        public void Dismiss()
        {
            _dismissed = true;
            var dismissed = Dismissed;
            if (dismissed != null)
            {
                dismissed(this, EventArgs.Empty);
            }
        }

        public event EventHandler Dismissed;

        public VisualStudio.Text.SnapshotPoint? GetTriggerPoint(VisualStudio.Text.ITextSnapshot textSnapshot)
        {
            return GetTriggerPoint(textSnapshot.TextBuffer).GetPoint(textSnapshot);
        }

        public VisualStudio.Text.ITrackingPoint GetTriggerPoint(VisualStudio.Text.ITextBuffer textBuffer)
        {
            if (textBuffer == _triggerPoint.TextBuffer)
            {
                return _triggerPoint;
            }
            throw new NotImplementedException();

        }

        public bool IsDismissed
        {
            get { return _dismissed; }
        }

        public bool Match()
        {
            throw new NotImplementedException();
        }

        public IIntellisensePresenter Presenter
        {
            get { throw new NotImplementedException(); }
        }

        public event EventHandler PresenterChanged
        {
            add { throw new NotImplementedException(); }
            remove { throw new NotImplementedException(); }
        }

        public void Recalculate()
        {
            throw new NotImplementedException();
        }

        public event EventHandler Recalculated
        {
            add { throw new NotImplementedException(); }
            remove { throw new NotImplementedException(); }
        }

        public void Start()
        {
            throw new NotImplementedException();
        }

        public VisualStudio.Text.Editor.ITextView TextView
        {
            get { return _view; }
        }

        public VisualStudio.Utilities.PropertyCollection Properties
        {
            get { return _properties; }
        }
    }
}
