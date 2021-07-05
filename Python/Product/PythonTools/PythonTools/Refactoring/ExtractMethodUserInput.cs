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

namespace Microsoft.PythonTools.Refactoring {
    class ExtractMethodUserInput : IExtractMethodInput {
        private readonly IServiceProvider _serviceProvider;

        public ExtractMethodUserInput(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
        }

        public bool ShouldExpandSelection() {
            var res = MessageBox.Show(Strings.ExtractMethod_ShouldExpandSelection,
                        Strings.ExtractMethod_ShouldExpandSelectionTitle,
                        MessageBoxButton.YesNo
                    );

            return res == MessageBoxResult.Yes;
        }


        public ExtractMethodRequest GetExtractionInfo(ExtractedMethodCreator previewer) {
            var requestView = new ExtractMethodRequestView(_serviceProvider, previewer);
            var dialog = new ExtractMethodDialog(requestView);

            bool res = dialog.ShowModal() ?? false;
            if (res) {
                return requestView.GetRequest();
            }

            return null;
        }

        public void CannotExtract(string reason) {
            MessageBox.Show(reason, Strings.ExtractMethod_CannotExtractMethod, MessageBoxButton.OK);
        }
    }
}
