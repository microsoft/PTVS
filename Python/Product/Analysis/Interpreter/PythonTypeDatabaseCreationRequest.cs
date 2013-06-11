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

namespace Microsoft.PythonTools.Interpreter {
    public sealed class PythonTypeDatabaseCreationRequest {
        public PythonTypeDatabaseCreationRequest() {
            ExtraInputDatabases = new List<string>();
        }

        public IPythonInterpreterFactory Factory { get; set; }

        public List<string> ExtraInputDatabases { get; private set; }

        public string OutputPath { get; set; }

        public bool SkipUnchanged { get; set; }

        public Action<int> OnExit { get; set; }
    }
}
