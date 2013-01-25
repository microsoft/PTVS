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

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.PythonTools {
    /// <summary>
    /// Implements classification of text by using a ScriptEngine which supports the
    /// TokenCategorizer service.
    /// 
    /// Languages should subclass this type and override the Engine property. They 
    /// should then export the provider using MEF indicating the content type 
    /// which it is applicable to.
    /// </summary>
    [Export(typeof(IClassifierProvider)), ContentType(PythonCoreConstants.ContentType)]
    internal class PythonClassifierProvider : IClassifierProvider {
        private Dictionary<TokenCategory, IClassificationType> _categoryMap;
        private IClassificationType _comment;
        private IClassificationType _stringLiteral;
        private IClassificationType _keyword;
        private IClassificationType _operator;
        private IClassificationType _groupingClassification;
        private IClassificationType _dotClassification;
        private IClassificationType _commaClassification;
        private readonly IContentType _type;

        [ImportingConstructor]
        public PythonClassifierProvider(IContentTypeRegistryService contentTypeRegistryService) {
            _type = contentTypeRegistryService.GetContentType(PythonCoreConstants.ContentType);
        }

        /// <summary>
        /// Import the classification registry to be used for getting a reference
        /// to the custom classification type later.
        /// </summary>
        [Import]
        public IClassificationTypeRegistryService _classificationRegistry = null; // Set via MEF

        #region Python Classification Type Definitions

        [Export]
        [Name(PythonPredefinedClassificationTypeNames.Grouping)]
        [BaseDefinition(PythonPredefinedClassificationTypeNames.Operator)]
        internal static ClassificationTypeDefinition GroupingClassificationDefinition = null; // Set via MEF

        [Export]
        [Name(PythonPredefinedClassificationTypeNames.Dot)]
        [BaseDefinition(PythonPredefinedClassificationTypeNames.Operator)]
        internal static ClassificationTypeDefinition DotClassificationDefinition = null; // Set via MEF

        [Export]
        [Name(PythonPredefinedClassificationTypeNames.Comma)]
        [BaseDefinition(PythonPredefinedClassificationTypeNames.Operator)]
        internal static ClassificationTypeDefinition CommaClassificationDefinition = null; // Set via MEF

        [Export]
        [Name(PythonPredefinedClassificationTypeNames.Operator)]
#if DEV11
        [BaseDefinition(PredefinedClassificationTypeNames.Operator)]
#else
        [BaseDefinition(PredefinedClassificationTypeNames.FormalLanguage)]
#endif
        internal static ClassificationTypeDefinition OperatorClassificationDefinition = null; // Set via MEF

        #endregion

        #region IDlrClassifierProvider

        public IClassifier GetClassifier(ITextBuffer buffer) {
            if (_categoryMap == null) {
                _categoryMap = FillCategoryMap(_classificationRegistry);
            }

            PythonClassifier res;
            if (!buffer.Properties.TryGetProperty<PythonClassifier>(typeof(PythonClassifier), out res) &&
                buffer.ContentType.IsOfType(ContentType.TypeName)) {
                res = new PythonClassifier(this, buffer);
                buffer.Properties.AddProperty(typeof(PythonClassifier), res);
            }

            return res;
        }

        public virtual IContentType ContentType {
            get { return _type; }
        }

        public IClassificationType Comment {
            get { return _comment; }
        }

        public IClassificationType StringLiteral {
            get { return _stringLiteral; }
        }

        public IClassificationType Keyword {
            get { return _keyword; }
        }

        public IClassificationType Operator {
            get { return _operator; }
        }

        public IClassificationType GroupingClassification {
            get { return _groupingClassification; }
        }

        public IClassificationType DotClassification {
            get { return _dotClassification; }
        }

        public IClassificationType CommaClassification {
            get { return _commaClassification; }
        }

        #endregion

        internal Dictionary<TokenCategory, IClassificationType> CategoryMap {
            get { return _categoryMap; }
        }

        private Dictionary<TokenCategory, IClassificationType> FillCategoryMap(IClassificationTypeRegistryService registry) {
            var categoryMap = new Dictionary<TokenCategory, IClassificationType>();

            categoryMap[TokenCategory.DocComment] = _comment = registry.GetClassificationType(PredefinedClassificationTypeNames.Comment);
            categoryMap[TokenCategory.LineComment] = registry.GetClassificationType(PredefinedClassificationTypeNames.Comment);
            categoryMap[TokenCategory.Comment] = registry.GetClassificationType(PredefinedClassificationTypeNames.Comment);
            categoryMap[TokenCategory.NumericLiteral] = registry.GetClassificationType(PredefinedClassificationTypeNames.Literal);
            categoryMap[TokenCategory.CharacterLiteral] = registry.GetClassificationType(PredefinedClassificationTypeNames.Character);
            categoryMap[TokenCategory.StringLiteral] = _stringLiteral = registry.GetClassificationType(PredefinedClassificationTypeNames.String);
            categoryMap[TokenCategory.Keyword] = _keyword = registry.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
            categoryMap[TokenCategory.Directive] = registry.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
            categoryMap[TokenCategory.Identifier] = registry.GetClassificationType(PredefinedClassificationTypeNames.Identifier);
            categoryMap[TokenCategory.Operator] = _operator = registry.GetClassificationType(PythonPredefinedClassificationTypeNames.Operator);
            categoryMap[TokenCategory.Delimiter] = registry.GetClassificationType(PythonPredefinedClassificationTypeNames.Operator);
            categoryMap[TokenCategory.Grouping] = registry.GetClassificationType(PythonPredefinedClassificationTypeNames.Operator);
            categoryMap[TokenCategory.WhiteSpace] = registry.GetClassificationType(PredefinedClassificationTypeNames.WhiteSpace);
            categoryMap[TokenCategory.RegularExpressionLiteral] = registry.GetClassificationType(PredefinedClassificationTypeNames.Literal);
            _groupingClassification = registry.GetClassificationType(PythonPredefinedClassificationTypeNames.Grouping);
            _commaClassification = registry.GetClassificationType(PythonPredefinedClassificationTypeNames.Comma);
            _dotClassification = registry.GetClassificationType(PythonPredefinedClassificationTypeNames.Dot);

            return categoryMap;
        }
    }

    #region Editor Format Definitions

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = PythonPredefinedClassificationTypeNames.Operator)]
    [Name(PythonPredefinedClassificationTypeNames.Operator)]
    [DisplayName(PythonPredefinedClassificationTypeNames.Operator)]
    [UserVisible(true)]
    [Order(After = LanguagePriority.NaturalLanguage, Before = LanguagePriority.FormalLanguage)]
    internal sealed class OperatorFormat : ClassificationFormatDefinition {
        public OperatorFormat() { }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = PythonPredefinedClassificationTypeNames.Grouping)]
    [Name(PythonPredefinedClassificationTypeNames.Grouping)]
    [DisplayName(PythonPredefinedClassificationTypeNames.Grouping)]
    [UserVisible(true)]
    [Order(After = LanguagePriority.NaturalLanguage, Before = LanguagePriority.FormalLanguage)]
    internal sealed class GroupingFormat : ClassificationFormatDefinition {
        public GroupingFormat() { }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = PythonPredefinedClassificationTypeNames.Comma)]
    [Name(PythonPredefinedClassificationTypeNames.Comma)]
    [DisplayName(PythonPredefinedClassificationTypeNames.Comma)]
    [UserVisible(true)]
    [Order(After = LanguagePriority.NaturalLanguage, Before = LanguagePriority.FormalLanguage)]
    internal sealed class CommaFormat : ClassificationFormatDefinition {
        public CommaFormat() { }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = PythonPredefinedClassificationTypeNames.Dot)]
    [Name(PythonPredefinedClassificationTypeNames.Dot)]
    [DisplayName(PythonPredefinedClassificationTypeNames.Dot)]
    [UserVisible(true)]
    [Order(After = LanguagePriority.NaturalLanguage, Before = LanguagePriority.FormalLanguage)]
    internal sealed class DotFormat : ClassificationFormatDefinition {
        public DotFormat() { }
    }

    #endregion
}
