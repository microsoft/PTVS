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
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;

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
    internal class PythonClassifierProvider : IPythonClassifierProvider {
        private Dictionary<TokenCategory, IClassificationType> _categoryMap;
        private IClassificationType _comment;
        private IClassificationType _stringLiteral;
        private IClassificationType _keyword;
        private IClassificationType _operator;
        private IClassificationType _openGroupingClassification;
        private IClassificationType _loseGroupingClassification;
        private IClassificationType _dotClassification;
        private IClassificationType _commaClassification;
        private readonly IContentType _type;
        public static PythonClassifierProvider Instance;

        [ImportingConstructor]
        public PythonClassifierProvider(IContentTypeRegistryService contentTypeRegistryService) {
            _type = contentTypeRegistryService.GetContentType(PythonCoreConstants.ContentType);
            Instance = this;
        }

        /// <summary>
        /// Import the classification registry to be used for getting a reference
        /// to the custom classification type later.
        /// </summary>
        [Import]
        public IClassificationTypeRegistryService _classificationRegistry = null; // Set via MEF

        #region DLR Classification Type Definitions

        [Export]
        [Name(PythonPredefinedClassificationTypeNames.OpenGrouping)]
        [BaseDefinition(PredefinedClassificationTypeNames.Operator)]
        internal static ClassificationTypeDefinition OpenGroupingClassificationDefinition = null; // Set via MEF

        [Export]
        [Name(PythonPredefinedClassificationTypeNames.CloseGrouping)]
        [BaseDefinition(PredefinedClassificationTypeNames.Operator)]
        internal static ClassificationTypeDefinition CloseGroupingClassificationDefinition = null; // Set via MEF

        [Export]
        [Name(PythonPredefinedClassificationTypeNames.Dot)]
        [BaseDefinition(PredefinedClassificationTypeNames.Operator)]
        internal static ClassificationTypeDefinition DotClassificationDefinition = null; // Set via MEF

        [Export]
        [Name(PythonPredefinedClassificationTypeNames.Comma)]
        [BaseDefinition(PredefinedClassificationTypeNames.Operator)]
        internal static ClassificationTypeDefinition CommaClassificationDefinition = null; // Set via MEF

        #endregion

        #region IDlrClassifierProvider

        public IClassifier GetClassifier(ITextBuffer buffer) {
            if (_categoryMap == null) {
                _categoryMap = FillCategoryMap(_classificationRegistry);
            }

            IPythonClassifier res;
            if (!buffer.Properties.TryGetProperty<IPythonClassifier>(typeof(IPythonClassifier), out res) &&
                buffer.ContentType.IsOfType(ContentType.TypeName)) {
                res = new PythonClassifier(this, buffer);
                buffer.Properties.AddProperty(typeof(IPythonClassifier), res);
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

        public IClassificationType OpenGroupingClassification {
            get { return _openGroupingClassification; }
        }

        public IClassificationType CloseGroupingClassification {
            get { return _loseGroupingClassification; }
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
            categoryMap[TokenCategory.Operator] = _operator = registry.GetClassificationType(PredefinedClassificationTypeNames.Operator);
            categoryMap[TokenCategory.Delimiter] = registry.GetClassificationType(PredefinedClassificationTypeNames.Operator);
            categoryMap[TokenCategory.Grouping] = registry.GetClassificationType(PredefinedClassificationTypeNames.Operator);
            categoryMap[TokenCategory.WhiteSpace] = registry.GetClassificationType(PredefinedClassificationTypeNames.WhiteSpace);
            categoryMap[TokenCategory.RegularExpressionLiteral] = registry.GetClassificationType(PredefinedClassificationTypeNames.Literal);
            _openGroupingClassification = registry.GetClassificationType(PythonPredefinedClassificationTypeNames.OpenGrouping);
            _loseGroupingClassification = registry.GetClassificationType(PythonPredefinedClassificationTypeNames.CloseGrouping);
            _commaClassification = registry.GetClassificationType(PythonPredefinedClassificationTypeNames.Comma);
            _dotClassification = registry.GetClassificationType(PythonPredefinedClassificationTypeNames.Dot);

            return categoryMap;
        }
    }

    #region Editor Format Definitions

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = PythonPredefinedClassificationTypeNames.OpenGrouping)]
    [Name(PythonPredefinedClassificationTypeNames.OpenGrouping)]
    [DisplayName("Open grouping character")]
    [UserVisible(true)]
    [Order(After = Priority.Default, Before = Priority.High)]
    internal sealed class OpenGroupingFormat : ClassificationFormatDefinition { }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = PythonPredefinedClassificationTypeNames.CloseGrouping)]
    [Name(PythonPredefinedClassificationTypeNames.CloseGrouping)]
    [DisplayName("Close grouping character")]
    [UserVisible(true)]
    [Order(After = Priority.Default, Before = Priority.High)]
    internal sealed class CloseGroupingFormat : ClassificationFormatDefinition { }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = PythonPredefinedClassificationTypeNames.Comma)]
    [Name(PythonPredefinedClassificationTypeNames.Comma)]
    [DisplayName("Comma character")]
    [UserVisible(true)]
    [Order(After = Priority.Default, Before = Priority.High)]
    internal sealed class CommaFormat : ClassificationFormatDefinition { }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = PythonPredefinedClassificationTypeNames.Dot)]
    [Name(PythonPredefinedClassificationTypeNames.Dot)]
    [DisplayName("Dot character")]
    [UserVisible(true)]
    [Order(After = Priority.Default, Before = Priority.High)]
    internal sealed class DotFormat : ClassificationFormatDefinition { }

    #endregion
}
