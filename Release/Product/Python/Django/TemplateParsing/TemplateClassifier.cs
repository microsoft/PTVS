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
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;

namespace Microsoft.PythonTools.Django.TemplateParsing {
    class TemplateClassifier : IClassifier {
        private readonly ITextBuffer _textBuffer;
        private readonly TemplateClassifierProvider _classifierProvider;

        public TemplateClassifier(TemplateClassifierProvider provider, ITextBuffer textBuffer) {
            _textBuffer = textBuffer;
            _classifierProvider = provider;
        }

        #region IClassifier Members

        event EventHandler<ClassificationChangedEventArgs> IClassifier.ClassificationChanged {
            add { }
            remove {  }
        }

        IList<ClassificationSpan> IClassifier.GetClassificationSpans(SnapshotSpan span) {
            return new[] { new ClassificationSpan(span, _classifierProvider._classType) };
        }

        #endregion
    }
}
