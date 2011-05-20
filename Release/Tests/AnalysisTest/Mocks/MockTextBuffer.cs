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
using Microsoft.PythonTools;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace AnalysisTest.Mocks {
    class MockTextBuffer : ITextBuffer {
        internal MockTextSnapshot _snapshot;
        private MockTextEdit _edit;
        private PropertyCollection _properties;

        public MockTextBuffer(string content) {
            _snapshot = new MockTextSnapshot(this, content);
        }

        public void ChangeContentType(Microsoft.VisualStudio.Utilities.IContentType newContentType, object editTag) {
            throw new NotImplementedException();
        }
#pragma warning disable 67
        public event EventHandler<TextContentChangedEventArgs> Changed;

        public event EventHandler<TextContentChangedEventArgs> ChangedHighPriority;

        public event EventHandler<TextContentChangedEventArgs> ChangedLowPriority;

        public event EventHandler<TextContentChangingEventArgs> Changing;

        public event EventHandler PostChanged;

        public event EventHandler<SnapshotSpanEventArgs> ReadOnlyRegionsChanged;

        public event EventHandler<ContentTypeChangedEventArgs> ContentTypeChanged;

#pragma warning restore 67

        public bool CheckEditAccess() {
            throw new NotImplementedException();
        }

        public Microsoft.VisualStudio.Utilities.IContentType ContentType {
            get { return CompletionContextTests.PythonContentType; }
        }

        public ITextEdit CreateEdit() {
            if (EditInProgress) {
                throw new InvalidOperationException();
            }
            _edit = new MockTextEdit(CurrentSnapshot);
            return _edit;
        }

        public ITextEdit CreateEdit(EditOptions options, int? reiteratedVersionNumber, object editTag) {
            throw new NotImplementedException();
        }

        public IReadOnlyRegionEdit CreateReadOnlyRegionEdit() {
            throw new NotImplementedException();
        }

        public ITextSnapshot CurrentSnapshot {
            get { return _snapshot; }
        }

        public ITextSnapshot Delete(Span deleteSpan) {
            throw new NotImplementedException();
        }

        public bool EditInProgress {
            get { return _edit != null; }
        }

        public NormalizedSpanCollection GetReadOnlyExtents(Span span) {
            throw new NotImplementedException();
        }

        public ITextSnapshot Insert(int position, string text) {
            throw new NotImplementedException();
        }

        public bool IsReadOnly(Span span, bool isEdit) {
            throw new NotImplementedException();
        }

        public bool IsReadOnly(Span span) {
            throw new NotImplementedException();
        }

        public bool IsReadOnly(int position, bool isEdit) {
            throw new NotImplementedException();
        }

        public bool IsReadOnly(int position) {
            throw new NotImplementedException();
        }

        public ITextSnapshot Replace(Span replaceSpan, string replaceWith) {
            throw new NotImplementedException();
        }

        public void TakeThreadOwnership() {
            throw new NotImplementedException();
        }

        private static readonly PythonClassifierProvider _classProvider = MakeClassifierProvider();

        private static PythonClassifierProvider MakeClassifierProvider() {
            var classReg = new MockClassificationTypeRegistryService();
            
            var provider = new PythonClassifierProvider(new MockContentTypeRegistryService());
            provider._classificationRegistry = classReg;
            return provider;
        }

        public Microsoft.VisualStudio.Utilities.PropertyCollection Properties {
            get {
                if (_properties == null) {
                    _properties = new PropertyCollection();

                    InitProperties();
                }

                return _properties;
            }
        }

        private void InitProperties() {
            _classProvider.GetClassifier(this);

            _properties.AddProperty(typeof(ITextDocument), new MockTextDocument("C:\\foo.py"));
        }

        internal void AddProperty(object key, object value) {
            if (_properties == null) {
                _properties = new PropertyCollection();
            }
            _properties.AddProperty(key, value);
            InitProperties();
        }
    }
}
