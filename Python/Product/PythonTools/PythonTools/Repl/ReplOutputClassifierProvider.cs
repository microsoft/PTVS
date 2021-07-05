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

namespace Microsoft.PythonTools.Repl {
    /// <summary>
    /// Provides the classifier for our repl error output buffer.
    /// </summary>
    [Export(typeof(IClassifierProvider)), ContentType(PredefinedInteractiveContentTypes.InteractiveOutputContentTypeName)]
    class ReplOutputClassifierProvider : IClassifierProvider {
        internal readonly Dictionary<ConsoleColor, IClassificationType> _classTypes = new Dictionary<ConsoleColor, IClassificationType>();

        [ImportingConstructor]
        public ReplOutputClassifierProvider(IClassificationTypeRegistryService classificationService) {
            _classTypes[ConsoleColor.Black] = classificationService.GetClassificationType(InteractiveBlackFormatDefinition.Name);
            _classTypes[ConsoleColor.DarkBlue] = classificationService.GetClassificationType(InteractiveDarkBlueFormatDefinition.Name);
            _classTypes[ConsoleColor.DarkGreen] = classificationService.GetClassificationType(InteractiveDarkGreenFormatDefinition.Name);
            _classTypes[ConsoleColor.DarkCyan] = classificationService.GetClassificationType(InteractiveDarkCyanFormatDefinition.Name);
            _classTypes[ConsoleColor.DarkRed] = classificationService.GetClassificationType(InteractiveDarkRedFormatDefinition.Name);
            _classTypes[ConsoleColor.DarkMagenta] = classificationService.GetClassificationType(InteractiveDarkMagentaFormatDefinition.Name);
            _classTypes[ConsoleColor.DarkYellow] = classificationService.GetClassificationType(InteractiveDarkYellowFormatDefinition.Name);
            _classTypes[ConsoleColor.Gray] = classificationService.GetClassificationType(InteractiveGrayFormatDefinition.Name);
            _classTypes[ConsoleColor.DarkGray] = classificationService.GetClassificationType(InteractiveDarkGrayFormatDefinition.Name);
            _classTypes[ConsoleColor.Blue] = classificationService.GetClassificationType(InteractiveBlueFormatDefinition.Name);
            _classTypes[ConsoleColor.Green] = classificationService.GetClassificationType(InteractiveGreenFormatDefinition.Name);
            _classTypes[ConsoleColor.Cyan] = classificationService.GetClassificationType(InteractiveCyanFormatDefinition.Name);
            _classTypes[ConsoleColor.Red] = classificationService.GetClassificationType(InteractiveRedFormatDefinition.Name);
            _classTypes[ConsoleColor.Magenta] = classificationService.GetClassificationType(InteractiveMagentaFormatDefinition.Name);
            _classTypes[ConsoleColor.Yellow] = classificationService.GetClassificationType(InteractiveYellowFormatDefinition.Name);
            _classTypes[ConsoleColor.White] = classificationService.GetClassificationType(InteractiveWhiteFormatDefinition.Name);
        }

        public IClassifier GetClassifier(ITextBuffer textBuffer) {
            return new ReplOutputClassifier(this, textBuffer);
        }
    }
}
