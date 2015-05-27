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
using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.PythonTools.Project;
#if DEV14_OR_LATER
using Microsoft.VisualStudio.InteractiveWindow;
#else
using Microsoft.VisualStudio.Repl;
#endif

using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.PythonTools {
#if DEV14_OR_LATER
    using IReplEvaluator = IInteractiveEvaluator;
#endif

    [Export(typeof(IClassifierProvider)), ContentType(PythonCoreConstants.ContentType)]
    internal class PythonAnalysisClassifierProvider : IClassifierProvider {
        private Dictionary<string, IClassificationType> _categoryMap;
        private readonly IContentType _type;
        internal readonly IServiceProvider _serviceProvider;

        [ImportingConstructor]
        public PythonAnalysisClassifierProvider(IContentTypeRegistryService contentTypeRegistryService, [Import(typeof(SVsServiceProvider))]IServiceProvider serviceProvider) {
            _type = contentTypeRegistryService.GetContentType(PythonCoreConstants.ContentType);
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Import the classification registry to be used for getting a reference
        /// to the custom classification type later.
        /// </summary>
        [Import]
        public IClassificationTypeRegistryService _classificationRegistry = null; // Set via MEF

        #region Python Classification Type Definitions

        [Export]
        [Name(PythonPredefinedClassificationTypeNames.Class)]
        [BaseDefinition(PredefinedClassificationTypeNames.Identifier)]
        internal static ClassificationTypeDefinition ClassClassificationDefinition = null; // Set via MEF

        [Export]
        [Name(PythonPredefinedClassificationTypeNames.Function)]
        [BaseDefinition(PredefinedClassificationTypeNames.Identifier)]
        internal static ClassificationTypeDefinition FunctionClassificationDefinition = null; // Set via MEF

        [Export]
        [Name(PythonPredefinedClassificationTypeNames.Parameter)]
        [BaseDefinition(PredefinedClassificationTypeNames.Identifier)]
        internal static ClassificationTypeDefinition ParameterClassificationDefinition = null; // Set via MEF

        [Export]
        [Name(PythonPredefinedClassificationTypeNames.Module)]
        [BaseDefinition(PredefinedClassificationTypeNames.Identifier)]
        internal static ClassificationTypeDefinition ModuleClassificationDefinition = null; // Set via MEF

        #endregion

        #region IDlrClassifierProvider

        public IClassifier GetClassifier(ITextBuffer buffer) {
            if (buffer.Properties.ContainsProperty(typeof(IReplEvaluator))) {
                return null;
            }
            
            if (_categoryMap == null) {
                _categoryMap = FillCategoryMap(_classificationRegistry);
            }

            PythonAnalysisClassifier res;
            if (!buffer.Properties.TryGetProperty<PythonAnalysisClassifier>(typeof(PythonAnalysisClassifier), out res) &&
                buffer.ContentType.IsOfType(ContentType.TypeName)) {
                res = new PythonAnalysisClassifier(this, buffer);
                buffer.Properties.AddProperty(typeof(PythonAnalysisClassifier), res);
            }

            return res;
        }

        public virtual IContentType ContentType {
            get { return _type; }
        }

        #endregion

        internal Dictionary<string, IClassificationType> CategoryMap {
            get { return _categoryMap; }
        }

        private Dictionary<string, IClassificationType> FillCategoryMap(IClassificationTypeRegistryService registry) {
            var categoryMap = new Dictionary<string, IClassificationType>();

            categoryMap[PythonPredefinedClassificationTypeNames.Class] = registry.GetClassificationType(PythonPredefinedClassificationTypeNames.Class);
            categoryMap[PythonPredefinedClassificationTypeNames.Parameter] = registry.GetClassificationType(PythonPredefinedClassificationTypeNames.Parameter);
            categoryMap[PythonPredefinedClassificationTypeNames.Module] = registry.GetClassificationType(PythonPredefinedClassificationTypeNames.Module);
            categoryMap[PythonPredefinedClassificationTypeNames.Function] = registry.GetClassificationType(PythonPredefinedClassificationTypeNames.Function);
            // Include keyword for context-sensitive keywords
            categoryMap[PredefinedClassificationTypeNames.Keyword] = registry.GetClassificationType(PredefinedClassificationTypeNames.Keyword);

            return categoryMap;
        }
    }

    #region Editor Format Definitions

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = PythonPredefinedClassificationTypeNames.Class)]
    [Name(PythonPredefinedClassificationTypeNames.Class)]
    [UserVisible(true)]
    [Order(After = LanguagePriority.NaturalLanguage, Before = LanguagePriority.FormalLanguage)]
    internal sealed class ClassFormat : ClassificationFormatDefinition {
        public ClassFormat() {
            DisplayName = SR.GetString(SR.ClassClassificationType);
            // Matches "C++ User Types"
            ForegroundColor = Color.FromArgb(255, 43, 145, 175);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = PythonPredefinedClassificationTypeNames.Module)]
    [Name(PythonPredefinedClassificationTypeNames.Module)]
    [UserVisible(true)]
    [Order(After = LanguagePriority.NaturalLanguage, Before = LanguagePriority.FormalLanguage)]
    internal sealed class ModuleFormat : ClassificationFormatDefinition {
        public ModuleFormat() {
            DisplayName = SR.GetString(SR.ModuleClassificationType);
            // Matches "C++ Macros"
            ForegroundColor = Color.FromArgb(255, 111, 0, 138);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = PythonPredefinedClassificationTypeNames.Parameter)]
    [Name(PythonPredefinedClassificationTypeNames.Parameter)]
    [UserVisible(true)]
    [Order(After = LanguagePriority.NaturalLanguage, Before = LanguagePriority.FormalLanguage)]
    internal sealed class ParameterFormat : ClassificationFormatDefinition {
        public ParameterFormat() {
            DisplayName = SR.GetString(SR.ParameterClassificationType);
            // Matches "C++ Parameters"
            ForegroundColor = Color.FromArgb(255, 128, 128, 128);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = PythonPredefinedClassificationTypeNames.Function)]
    [Name(PythonPredefinedClassificationTypeNames.Function)]
    [UserVisible(true)]
    [Order(After = LanguagePriority.NaturalLanguage, Before = LanguagePriority.FormalLanguage)]
    internal sealed class FunctionFormat : ClassificationFormatDefinition {
        public FunctionFormat() {
            DisplayName = SR.GetString(SR.FunctionClassificationType);
            // Matches "C++ Functions"
            ForegroundColor = Colors.Black;
        }
    }

    #endregion
}
