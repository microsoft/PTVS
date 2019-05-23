
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.TestAdapter.Config;
using Microsoft.PythonTools.TestAdapter.Pytest;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

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


        public List<PytestDiscoveryResults> RunDiscovery(PythonProjectSettings projSettings, IEnumerable<string> sources) {
            var discoveryResults = new List<PytestDiscoveryResults>();
            var arguments = GetArguments(sources);
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
                var json = new StreamReader(outputStream).ReadToEnd();

                try {
                    discoveryResults = JsonConvert.DeserializeObject<List<PytestDiscoveryResults>>(json);
                } catch (InvalidOperationException ex) {
                    Debug.WriteLine("Failed to parse: {0}".FormatInvariant(ex.Message));
                    Debug.WriteLine(json);
                } catch (JsonException ex) {
                    Debug.WriteLine("Failed to parse: {0}".FormatInvariant(ex.Message));
                    Debug.WriteLine(json);
                }
            }

            return discoveryResults;
        }
    }
}
