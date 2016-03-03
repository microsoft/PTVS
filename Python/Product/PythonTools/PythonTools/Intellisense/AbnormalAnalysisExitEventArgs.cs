using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Intellisense {
    internal sealed class AbnormalAnalysisExitEventArgs {
        public readonly string StdErr;
        public readonly int ExitCode;        

        public AbnormalAnalysisExitEventArgs(string stdOut, int exitCode) {
            StdErr = stdOut;
            ExitCode = exitCode;
        }
    }
}
