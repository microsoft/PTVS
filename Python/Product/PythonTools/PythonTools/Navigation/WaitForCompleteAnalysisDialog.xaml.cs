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

using System;
using System.Threading;
using System.Windows;
using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Navigation {
    /// <summary>
    /// Interaction logic for WaitForCompleteAnalysisDialog.xaml
    /// </summary>
    partial class WaitForCompleteAnalysisDialog : DialogWindowVersioningWorkaround {
        private VsProjectAnalyzer _analyzer;

        public WaitForCompleteAnalysisDialog(VsProjectAnalyzer analyzer) {
            _analyzer = analyzer;
            InitializeComponent();

            new Thread(AnalysisComplete).Start();
        }

        private void _cancelButton_Click(object sender, RoutedEventArgs e) {
            this.DialogResult = false;
            this.Close();
        }

        private void AnalysisComplete() {
            _analyzer.WaitForCompleteAnalysis(UpdateItemsRemaining);
        }

        private bool UpdateItemsRemaining(int itemsLeft) {
            if (itemsLeft == 0) {
                try {
                    Dispatcher.Invoke((Action)(() => {
                        this.DialogResult = true;
                        this.Close();
                    }));
                } catch (OperationCanceledException) {
                    // Should only occur if the dialog is closed already, so nothing left to do
                }
                return false;
            }
            
            bool? dialogResult = null;
            try {
                Dispatcher.Invoke((Action)(() => {
                    dialogResult = DialogResult;
                    if (dialogResult == null) {
                        _progress.Maximum = itemsLeft;
                    }
                }));
            } catch (OperationCanceledException) {
            }

            return dialogResult == null;
        }
    }
}
