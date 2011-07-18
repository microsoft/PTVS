using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Interpreter {
    public sealed class PythonTypeDatabaseCreationRequest {
        public PythonTypeDatabaseCreationRequest() {
            DatabaseOptions = GenerateDatabaseOptions.StdLibDatabase;
        }

        public GenerateDatabaseOptions DatabaseOptions { get; set; }

        public IPythonInterpreterFactory Factory { get; set; }

        public string OutputPath { get; set; }
    }
}
