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

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.PythonTools.Django.TemplateParsing {
    [Export(typeof(IClassifierProvider)), ContentType(TemplateContentType.ContentTypeName)]
    class TemplateClassifierProvider : IClassifierProvider {
        private readonly IContentType _type;
        internal readonly IClassificationType _classType;

        [ImportingConstructor]
        public TemplateClassifierProvider(IContentTypeRegistryService contentTypeRegistryService, IClassificationTypeRegistryService classificationRegistry) {
            _type = contentTypeRegistryService.GetContentType(TemplateContentType.ContentTypeName);
            _classType = classificationRegistry.GetClassificationType(PredefinedClassificationTypeNames.Operator);
        }

        #region IClassifierProvider Members

        public IClassifier GetClassifier(ITextBuffer textBuffer) {
            TemplateClassifier res;
            if (!textBuffer.Properties.TryGetProperty<TemplateClassifier>(typeof(TemplateClassifier), out res) &&
                textBuffer.ContentType.IsOfType(_type.TypeName)) {
                res = new TemplateClassifier(this, textBuffer);
                textBuffer.Properties.AddProperty(typeof(TemplateClassifier), res);
            }
            return res;
        }

        #endregion
    }
}
