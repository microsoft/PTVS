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
using Microsoft.PythonTools.Django.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.PythonTools.Django.TemplateParsing {
    internal abstract class TemplateClassifierProviderBase : IClassifierProvider {
        private readonly IContentType _contentType;

        internal readonly IClassificationType _classType, _templateClassType, _commentClassType, 
                                              _identifierType, _literalType, _numberType, _dot, 
                                              _keywordType, _excludedCode;

        protected TemplateClassifierProviderBase(string contentTypeName, IContentTypeRegistryService contentTypeRegistryService, IClassificationTypeRegistryService classificationRegistry) {
            _contentType = contentTypeRegistryService.GetContentType(contentTypeName);
            _classType = classificationRegistry.GetClassificationType(PredefinedClassificationTypeNames.Operator);
            _templateClassType = classificationRegistry.GetClassificationType(DjangoPredefinedClassificationTypeNames.TemplateTag);
            _commentClassType = classificationRegistry.GetClassificationType(PredefinedClassificationTypeNames.Comment);
            _identifierType = classificationRegistry.GetClassificationType(PredefinedClassificationTypeNames.Identifier);
            _literalType = classificationRegistry.GetClassificationType(PredefinedClassificationTypeNames.Literal);
            _numberType = classificationRegistry.GetClassificationType(PredefinedClassificationTypeNames.Number);
            _dot = classificationRegistry.GetClassificationType(PythonPredefinedClassificationTypeNames.Dot);
            _keywordType = classificationRegistry.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
            _excludedCode = classificationRegistry.GetClassificationType(PredefinedClassificationTypeNames.ExcludedCode);
        }

        #region IClassifierProvider Members

        public IClassifier GetClassifier(ITextBuffer textBuffer) {
            TemplateClassifier res;
            if (!textBuffer.Properties.TryGetProperty<TemplateClassifier>(typeof(TemplateClassifier), out res) &&
                textBuffer.ContentType.IsOfType(_contentType.TypeName)) {
                res = new TemplateClassifier(this, textBuffer);
                textBuffer.Properties.AddProperty(typeof(TemplateClassifier), res);
            }
            return res;
        }

        #endregion

        [Export]
        [Name(DjangoPredefinedClassificationTypeNames.TemplateTag)]
        [BaseDefinition(PredefinedClassificationTypeNames.FormalLanguage)]
        internal static ClassificationTypeDefinition TemplateTag = null; // Set via MEF
    }
}
