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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.PythonTools.Django.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.PythonTools.Django.TemplateParsing {
    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = DjangoPredefinedClassificationTypeNames.TemplateTag)]
    [Name(DjangoPredefinedClassificationTypeNames.TemplateTag)] // TODO: Localization - this string appears in fonts page in tools options
    [DisplayName(DjangoPredefinedClassificationTypeNames.TemplateTag)] // TODO: Localization - not sure if this is used, does not affect fonts page
    [UserVisible(true)]
    [Order(After = LanguagePriority.NaturalLanguage, Before = LanguagePriority.FormalLanguage)]
    internal sealed class OperatorFormat : ClassificationFormatDefinition {
        public OperatorFormat() {
            ForegroundColor = Color.FromRgb(0x00, 0x80, 0x80);
        }
    }
}
