// Python Tools for Visual Studio
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

using Microsoft.PythonTools.Django.Intellisense;

namespace Microsoft.PythonTools.Django.TemplateParsing
{
    internal abstract class TemplateClassifierProviderBase : IClassifierProvider
    {
        private readonly IContentType _contentType;

        internal readonly IClassificationType _classType, _templateClassType, _commentClassType,
                                              _identifierType, _literalType, _numberType, _dot,
                                              _keywordType, _excludedCode;

        protected TemplateClassifierProviderBase(string contentTypeName, IContentTypeRegistryService contentTypeRegistryService, IClassificationTypeRegistryService classificationRegistry)
        {
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

        public IClassifier GetClassifier(ITextBuffer textBuffer)
        {
            TemplateClassifier res;
            if (!textBuffer.Properties.TryGetProperty<TemplateClassifier>(typeof(TemplateClassifier), out res) &&
                textBuffer.ContentType.IsOfType(_contentType.TypeName))
            {
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
