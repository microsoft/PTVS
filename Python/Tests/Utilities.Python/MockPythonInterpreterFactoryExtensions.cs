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
using System.Threading;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.InterpreterList;

namespace TestUtilities.Python {
    public static class MockPythonInterpreterFactoryExtensions {
        public static void EndGenerateCompletionDatabase(
            this MockPythonInterpreterFactory factory,
            object interpreterList,
            string id,
            bool success
        ) {
            factory._success = success;

            // Because InterpreterList is InternalsVisibleTo, we have to smuggle
            // it in as an object.
            var list = (InterpreterList)interpreterList;
            if (list != null) {
                // No need to sleep between updates because we're going directly
                // to the control.
                for (int i = 0; i < 100; i += 30) {
                    list.Update(new Dictionary<string, AnalysisProgress> { { id, new AnalysisProgress { Progress = i, Maximum = 100 } } });
                }

                list.Update(new Dictionary<string, AnalysisProgress>());
            } else {
                factory.NotifyNewDatabase();
            }
        }
    }
}
