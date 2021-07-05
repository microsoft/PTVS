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

using Microsoft.PythonTools.Editor;

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
    [Export(typeof(PythonClassifierProvider))]
    internal class PythonClassifierProvider : IClassifierProvider {
        private Dictionary<TokenCategory, IClassificationType> _categoryMap;
        private IClassificationType _comment;
        private IClassificationType _stringLiteral;
        private IClassificationType _keyword;
        private IClassificationType _operator;
        private IClassificationType _groupingClassification;
        private IClassificationType _dotClassification;
        private IClassificationType _commaClassification;
        private readonly PythonEditorServices _services;
        private readonly IContentType _type;

        [ImportingConstructor]
        public PythonClassifierProvider(PythonEditorServices services) {
            _services = services;
            _type = _services.ContentTypeRegistryService.GetContentType(PythonCoreConstants.ContentType);
        }

        internal PythonEditorServices Services => _services;

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
        [BaseDefinition(PredefinedClassificationTypeNames.Operator)]
        internal static ClassificationTypeDefinition OperatorClassificationDefinition = null; // Set via MEF

        [Export]
        [Name(PythonPredefinedClassificationTypeNames.Builtin)]
        [BaseDefinition(PredefinedClassificationTypeNames.Identifier)]
        internal static ClassificationTypeDefinition BuiltinClassificationDefinition = null; // Set via MEF

        #endregion

        #region IDlrClassifierProvider

        public IClassifier GetClassifier(ITextBuffer buffer) {
            if (_categoryMap == null) {
                _categoryMap = FillCategoryMap(_services.ClassificationTypeRegistryService);
            }

            if (buffer.ContentType.IsOfType(CodeRemoteContentDefinition.CodeRemoteContentTypeName)) {
                return null;
            }

            return _services.GetBufferInfo(buffer)
                .GetOrCreateSink(typeof(PythonClassifier), _ => new PythonClassifier(this));
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

            categoryMap[TokenCategory.DocComment] = _comment = registry.GetClassificationType(PythonPredefinedClassificationTypeNames.Documentation);
            categoryMap[TokenCategory.LineComment] = registry.GetClassificationType(PredefinedClassificationTypeNames.Comment);
            categoryMap[TokenCategory.Comment] = registry.GetClassificationType(PredefinedClassificationTypeNames.Comment);
            categoryMap[TokenCategory.NumericLiteral] = registry.GetClassificationType(PredefinedClassificationTypeNames.Number);
            categoryMap[TokenCategory.CharacterLiteral] = registry.GetClassificationType(PredefinedClassificationTypeNames.Character);
            categoryMap[TokenCategory.StringLiteral] = _stringLiteral = registry.GetClassificationType(PredefinedClassificationTypeNames.String);
            categoryMap[TokenCategory.IncompleteMultiLineStringLiteral] = _stringLiteral;
            categoryMap[TokenCategory.Keyword] = _keyword = registry.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
            categoryMap[TokenCategory.Directive] = registry.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
            categoryMap[TokenCategory.Identifier] = registry.GetClassificationType(PredefinedClassificationTypeNames.Identifier);
            categoryMap[TokenCategory.Operator] = _operator = registry.GetClassificationType(PythonPredefinedClassificationTypeNames.Operator);
            categoryMap[TokenCategory.Delimiter] = registry.GetClassificationType(PythonPredefinedClassificationTypeNames.Operator);
            categoryMap[TokenCategory.Grouping] = registry.GetClassificationType(PythonPredefinedClassificationTypeNames.Operator);
            categoryMap[TokenCategory.WhiteSpace] = registry.GetClassificationType(PredefinedClassificationTypeNames.WhiteSpace);
            categoryMap[TokenCategory.RegularExpressionLiteral] = registry.GetClassificationType(PredefinedClassificationTypeNames.Literal);
            categoryMap[TokenCategory.BuiltinIdentifier] = registry.GetClassificationType(PythonPredefinedClassificationTypeNames.Builtin);
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
    [UserVisible(true)]
    [Order(After = LanguagePriority.NaturalLanguage, Before = LanguagePriority.FormalLanguage)]
    internal sealed class OperatorFormat : ClassificationFormatDefinition {
        public OperatorFormat() {
            DisplayName = Strings.OperatorClassificationType;
            // Matches "Operator"
            ForegroundColor = Colors.Black;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = PythonPredefinedClassificationTypeNames.Grouping)]
    [Name(PythonPredefinedClassificationTypeNames.Grouping)]
    [UserVisible(true)]
    [Order(After = LanguagePriority.NaturalLanguage, Before = LanguagePriority.FormalLanguage)]
    internal sealed class GroupingFormat : ClassificationFormatDefinition {
        public GroupingFormat() {
            DisplayName = Strings.GroupingClassificationType;
            // Matches "Operator"
            ForegroundColor = Colors.Black;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = PythonPredefinedClassificationTypeNames.Comma)]
    [Name(PythonPredefinedClassificationTypeNames.Comma)]
    [UserVisible(true)]
    [Order(After = LanguagePriority.NaturalLanguage, Before = LanguagePriority.FormalLanguage)]
    internal sealed class CommaFormat : ClassificationFormatDefinition {
        public CommaFormat() {
            DisplayName = Strings.CommaClassificationType;
            // Matches "Operator"
            ForegroundColor = Colors.Black;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = PythonPredefinedClassificationTypeNames.Dot)]
    [Name(PythonPredefinedClassificationTypeNames.Dot)]
    [UserVisible(true)]
    [Order(After = LanguagePriority.NaturalLanguage, Before = LanguagePriority.FormalLanguage)]
    internal sealed class DotFormat : ClassificationFormatDefinition {
        public DotFormat() {
            DisplayName = Strings.DotClassificationType;
            // Matches "Operator"
            ForegroundColor = Colors.Black;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = PythonPredefinedClassificationTypeNames.Builtin)]
    [Name(PythonPredefinedClassificationTypeNames.Builtin)]
    [UserVisible(true)]
    [Order(After = LanguagePriority.NaturalLanguage, Before = LanguagePriority.FormalLanguage)]
    internal sealed class BuiltinFormat : ClassificationFormatDefinition {
        public BuiltinFormat() {
            DisplayName = Strings.BuiltinClassificationType;
            // Matches "Keyword"
            ForegroundColor = Colors.Blue;
        }
    }

    #endregion
}
