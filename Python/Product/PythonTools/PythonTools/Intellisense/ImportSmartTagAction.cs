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
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;

namespace Microsoft.PythonTools.Intellisense {
#if !DEV14_OR_LATER
    /// <summary>
    /// Provides the smart tag action for adding missing import statements.
    /// </summary>
    class ImportSmartTagAction : SmartTagAction {
        public readonly string Name, FromName;
        private readonly ITextBuffer _buffer;
        private readonly ITextView _view;

        /// <summary>
        /// Creates a new smart tag action for an "import fob" smart tag.
        /// </summary>
        public ImportSmartTagAction(string name, ITextBuffer buffer, ITextView view, IServiceProvider serviceProvider)
            : base(serviceProvider, RefactoringIconKind.AddUsing) {
            Name = name;
            _buffer = buffer;
            _view = view;
        }

        /// <summary>
        /// Creates a new smart tag action for a "from fob import oar" smart tag.
        /// </summary>
        public ImportSmartTagAction(string fromName, string name, ITextBuffer buffer, ITextView view, IServiceProvider serviceProvider)
            : base(serviceProvider, RefactoringIconKind.AddUsing) {
            FromName = fromName;
            Name = name;
            _buffer = buffer;
            _view = view;
        }

        public override void Invoke() {
            MissingImportAnalysis.AddImport(
                _serviceProvider,
                _buffer,
                _view,
                FromName,
                Name
            );
        }

        public override string DisplayText {
            get {
                return InsertionText.Replace("_", "__");
            }
        }

        private string InsertionText {
            get {
                return MissingImportAnalysis.MakeImportCode(FromName, Name);
            }
        }
    }
#endif
}
