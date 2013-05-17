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
using System.Windows.Forms;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Options {
    partial class GenerateIntellisenseDbDialog : Form {
        private readonly InterpreterOptions _interpreter;
        private readonly Action _completed;

        public GenerateIntellisenseDbDialog(InterpreterOptions interpreter, Action completed) {
            InitializeComponent();

            _interpreter = interpreter;
            _completed = completed;
        }

        private void OkButtonClick(object sender, EventArgs e) {
            bool async;
            if (_fullDb.Checked) {
                async = ((IInterpreterWithCompletionDatabase)_interpreter.Factory).GenerateCompletionDatabase(GenerateDatabaseOptions.StdLibDatabase | GenerateDatabaseOptions.BuiltinDatabase, _completed);
            } else {
                async = ((IInterpreterWithCompletionDatabase)_interpreter.Factory).GenerateCompletionDatabase(GenerateDatabaseOptions.BuiltinDatabase, _completed);
            }


            if (async) {
                DialogResult = DialogResult.Ignore;
            } else {
                DialogResult = DialogResult.OK;
            }
            Close();
        }

        private void _cancelButton_Click(object sender, EventArgs e) {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
