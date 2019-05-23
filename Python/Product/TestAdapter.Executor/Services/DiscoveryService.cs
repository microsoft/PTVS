
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Projects;
using Microsoft.PythonTools.TestAdapter.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.TestAdapter.Services {
    internal class DiscoveryService {

        private static readonly string DiscoveryAdapterPath = PythonToolsInstallPath.GetFile("PythonFiles\\testing_tools\\run_adapter.py");

        public DiscoveryService() {
            
        }

        public string[] GetArguments(IEnumerable<string> sources) {
            var arguments = new List<string>();
            arguments.Add(DiscoveryAdapterPath);
            arguments.Add("discover");
            arguments.Add("pytest");
            arguments.Add("--");

            foreach( var s in sources) {
                arguments.Add(s);
            }
            return arguments.ToArray();
        }


        public Dictionary<string, string> GetEnvironment() {
            var env = new Dictionary<string, string>();
            //var pythonPathVar = _config.Interpreter.PathEnvironmentVariable;
            //var paths = _config.SearchPaths;

            //paths.Insert(0, _config.WorkingDirectory);

            //string pythonPath = string.Join(
            //        ";",
            //        paths.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase)
            //    );

            //if (!string.IsNullOrWhiteSpace(pythonPathVar)) {
            //    env[pythonPathVar] = pythonPath;
            //}

            //foreach (var envVar in _config.Environment) {
            //    env[envVar.Key] = envVar.Value;
            //}
            return env;
        }


        public List<PytestDiscoveryResults> RunDiscovery(PythonProjectSettings projSettings) {
            var arguments = GetArguments(projSettings.Sources);
            var utf8 = new UTF8Encoding(false);

            using (var outputStream = new MemoryStream())
            using (var writer = new StreamWriter(outputStream, utf8, 4096, true))
            using (var proc = ProcessOutput.Run(
                projSettings.InterpreterPath, 
                arguments,
                projSettings.WorkingDirectory,
                projSettings.Environment,
                visible: false,
                new StreamRedirector(writer)
            )) {
                // If there's an error in the launcher script,
                // it will terminate without connecting back.
                WaitHandle.WaitAny(new WaitHandle[] { proc.WaitHandle });

                outputStream.Flush();
                outputStream.Seek(0, SeekOrigin.Begin);
                var jsonStr = new StreamReader(outputStream).ReadToEnd();

                var discoveryResults = JsonConvert.DeserializeObject<List<PytestDiscoveryResults>>(jsonStr);
                return discoveryResults;
            }
        }




        //private string[] GetArguments(LaunchConfiguration config) {
        //    var arguments = new List<string>();
        //    arguments.Add(TestLauncherPath);
        //    arguments.Add(config.WorkingDirectory);
        //    arguments.Add("pytest");
        //    arguments.Add("--collect-only");
        //    arguments.Add("test4.py");

        //    return arguments.ToArray();
        //}

        //public TestCaseInfo[] GetTestCases() {
        //    var testCases = new List<TestCaseInfo>();
        //    return testCases.ToArray();
        //}



    }
}
