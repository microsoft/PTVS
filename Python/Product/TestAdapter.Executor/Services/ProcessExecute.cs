using System;
using System.Collections.Generic;
using Microsoft.PythonTools.Infrastructure;

namespace Microsoft.PythonTools.TestAdapter.Services {
    static class ProcessExecute {

    static public string RunWithTimeout(
          string filename,
          Dictionary<string, string> env,
          IEnumerable<string> arguments,
          string workingDirectory,
          string pathEnv,
          int timeoutInSeconds
          ) {
            using (var proc = ProcessOutput.Run(
               filename,
               arguments,
               workingDirectory,
               env,
               false,
               null
           )) {
                if (!proc.ExitCode.HasValue) {
                    if (!proc.Wait(TimeSpan.FromSeconds(timeoutInSeconds))) {
                        try {
                            proc.Kill();
                        } catch (InvalidOperationException) {
                            // Process has already exited
                        }
                        throw new TimeoutException();
                    }
                }

                return string.Join(Environment.NewLine, proc.StandardOutputLines);
            }
        }
    }
}
