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

#if !DEV12_OR_LATER

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.PythonTools.Django.TemplateParsing {
    /// <summary>
    /// Provides a classifier for our HTML projection buffers so we can raise that the tags
    /// have changed on the full buffer when we switch between portions being HTML and portions
    /// being Django tags.
    /// </summary>
    [Export(typeof(IClassifierProvider)), ContentType("projection")]
    class ProjectionClassifierProvider : IClassifierProvider {

        #region IClassifierProvider Members

        public IClassifier GetClassifier(ITextBuffer textBuffer) {
            ProjectionClassifier res;
            if (!textBuffer.Properties.TryGetProperty<ProjectionClassifier>(typeof(ProjectionClassifier), out res) &&
                textBuffer.Properties.ContainsProperty(typeof(TemplateProjectionBuffer))) {
                res = new ProjectionClassifier();
                textBuffer.Properties.AddProperty(typeof(ProjectionClassifier), res);
            }
            return res;
        }

        #endregion
    }
}

#endif