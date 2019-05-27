using Microsoft.PythonTools.Infrastructure;
using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using System.IO;
using Microsoft.PythonTools.TestAdapter.Config;
using System.Diagnostics;

namespace Microsoft.PythonTools.TestAdapter.Services {
    public class ExecutorService {

        private static readonly string TestLauncherPath = PythonToolsInstallPath.GetFile("testlauncher.py");

        public string[] GetArguments(IEnumerable<TestCase> tests, PythonProjectSettings projSettings, string outputfile) {
            var arguments = new List<string>();
            arguments.Add(TestLauncherPath);
            arguments.Add(projSettings.WorkingDirectory);
            arguments.Add("pytest");
            arguments.Add(String.Format("--junitxml={0}", outputfile));

            foreach (var test in tests) {
                var pytestId = test.GetPropertyValue<string>(Pytest.Constants.PytestIdProperty, default(string));

                if (String.IsNullOrEmpty(pytestId)) {
                    Debug.WriteLine("PytestId missing for testcase {0}", test.FullyQualifiedName);
                    continue;
                }
                arguments.Add(pytestId);
            }
            return arguments.ToArray();
        }


        public string Run(PythonProjectSettings projSettings, IEnumerable<TestCase> tests) {

            var ouputFile = GetJunitXmlFile();
            var arguments = GetArguments(tests, projSettings, ouputFile);

            using (var proc = ProcessOutput.RunHiddenAndCapture(
                projSettings.InterpreterPath,
                arguments            
                
            )) {
                // If there's an error in the launcher script,
                // it will terminate without connecting back.
                WaitHandle.WaitAny(new WaitHandle[] { proc.WaitHandle });
            }

            return ouputFile;
        }

        private string GetJunitXmlFile() {
            var tempFolder = Path.Combine(Path.GetTempPath(), "pytest");
            Directory.CreateDirectory(tempFolder);

            string baseName = "junitresults_";
            string outPath = Path.Combine(tempFolder, baseName + Guid.NewGuid().ToString() + ".xml");
            return outPath;
        }

    }
}
