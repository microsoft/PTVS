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
    /// <summary>
    /// Classifier for projection buffers - used simply to raise that the tags have
    /// changed in the overall buffer.
    /// </summary>
    class ProjectionClassifier : IClassifier {
        #region IClassifier Members

        public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;

        public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span) {
            return new ClassificationSpan[0];
        }

        #endregion

        internal void RaiseClassificationChanged(SnapshotPoint start, SnapshotPoint end) {
            var classChanged = ClassificationChanged;
            if (classChanged != null) {
                classChanged(this, new ClassificationChangedEventArgs(new SnapshotSpan(start, end)));
            }
        }
    }
}
