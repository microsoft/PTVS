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
using System.Threading;
using System.Windows;
using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio.PlatformUI;

namespace Microsoft.PythonTools.Navigation {
    /// <summary>
    /// Interaction logic for WaitForCompleteAnalysisDialog.xaml
    /// </summary>
    partial class WaitForCompleteAnalysisDialog : DialogWindow {
        private ProjectAnalyzer _analyzer;

        public WaitForCompleteAnalysisDialog(ProjectAnalyzer analyzer) {
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
                Dispatcher.Invoke((Action)(() => {
                    this.DialogResult = true;
                    this.Close();
                }));
                return false;
            }
            
            bool? dialogResult = null;
            Dispatcher.Invoke((Action)(() => {
                dialogResult = DialogResult;
                if (dialogResult == null) {
                    _progress.Maximum = itemsLeft;
                }
            }));
            

            return dialogResult == null;
        }
    }
}
