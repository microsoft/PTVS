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
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;

namespace Microsoft.VisualStudioTools.MockVsTests {
    [Export(typeof(IClassifierAggregatorService))]
    public class MockClassifierAggregatorService : IClassifierAggregatorService {
        [ImportMany]
        internal IEnumerable<Lazy<IClassifierProvider, IContentTypeMetadata>> _providers = null;
        
        public IClassifier GetClassifier(ITextBuffer textBuffer) {
            if (_providers == null) {
                return null;
            }

            var contentType = textBuffer.ContentType;
            return new AggregatedClassifier(
                textBuffer,
                _providers.Where(e => e.Metadata.ContentTypes.Any(c => contentType.IsOfType(c)))
                    .Select(e => e.Value)
            );
        }

        sealed class AggregatedClassifier : IClassifier, IDisposable {
            private readonly ITextBuffer _buffer;
            private readonly List<IClassifier> _classifiers;

            public AggregatedClassifier(ITextBuffer textBuffer, IEnumerable<IClassifierProvider> providers) {
                _buffer = textBuffer;
                _classifiers = providers.Select(p => p.GetClassifier(_buffer)).ToList();
            }

            public void Dispose() {
                foreach (var c in _classifiers.OfType<IDisposable>()) {
                    c.Dispose();
                }
            }

            public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged {
                add {
                    foreach (var c in _classifiers) {
                        c.ClassificationChanged += value;
                    }
                }
                remove {
                    foreach (var c in _classifiers) {
                        c.ClassificationChanged -= value;
                    }
                }
            }

            public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span) {
                return _classifiers.SelectMany(c => c.GetClassificationSpans(span)).ToList();
            }
        }
    }
}
