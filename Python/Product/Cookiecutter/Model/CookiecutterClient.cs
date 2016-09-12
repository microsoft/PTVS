// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CookiecutterTools.Infrastructure;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json.Linq;

namespace Microsoft.CookiecutterTools.Model {
    class CookiecutterClient : ICookiecutterClient {
        private readonly CookiecutterPythonInterpreter _interpreter;

        public CookiecutterClient(CookiecutterPythonInterpreter interpreter) {
            _interpreter = interpreter;
        }

        public async Task<Tuple<ContextItem[], ProcessOutputResult>> LoadContextAsync(string localTemplateFolder, string userConfigFilePath) {
            if (localTemplateFolder == null) {
                throw new ArgumentNullException(nameof(localTemplateFolder));
            }

            var items = new List<ContextItem>();

            var result = await RunGenerateContextScript(_interpreter.InterpreterExecutablePath, localTemplateFolder, userConfigFilePath);
            var contextJson = string.Join(Environment.NewLine, result.StandardOutputLines);
            var context = (JToken)JObject.Parse(contextJson).SelectToken("cookiecutter");
            if (context != null) {
                foreach (JProperty prop in context) {
                    // Properties that start with underscore are for internal use,
                    // and cookiecutter doesn't prompt for them.
                    if (!prop.Name.StartsWith("_")) {
                        if (prop.Value.Type == JTokenType.String || prop.Value.Type == JTokenType.Integer || prop.Value.Type == JTokenType.Float) {
                            items.Add(new ContextItem(prop.Name, prop.Value.ToString()));
                        } else if (prop.Value.Type == JTokenType.Array) {
                            var elements = new List<string>();
                            JArray ar = prop.Value as JArray;
                            foreach (JToken element in ar) {
                                elements.Add(element.ToString());
                            }
                            items.Add(new ContextItem(prop.Name, elements[0], elements.ToArray()));
                        } else {
                            throw new InvalidOperationException(string.Format("Unsupported json element type in context file for property '{0}'.", prop.Name));
                        }
                    }
                }
            }

            return Tuple.Create(items.ToArray(), result);
        }

        public async Task<ProcessOutputResult> GenerateProjectAsync(string localTemplateFolder, string userConfigFilePath, string contextFilePath, string outputFolderPath) {
            if (localTemplateFolder == null) {
                throw new ArgumentNullException(nameof(localTemplateFolder));
            }

            if (contextFilePath == null) {
                throw new ArgumentNullException(nameof(contextFilePath));
            }

            if (outputFolderPath == null) {
                throw new ArgumentNullException(nameof(outputFolderPath));
            }

            string tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempFolder);

            var result = await RunRunScript(_interpreter.InterpreterExecutablePath, localTemplateFolder, userConfigFilePath, tempFolder, contextFilePath);
            MoveToDesiredFolder(outputFolderPath, tempFolder);

            return result;
        }

        private async Task<ProcessOutputResult> RunGenerateContextScript(string interpreterPath, string templateFolderPath, string userConfigFilePath) {
            var scriptPath = PythonToolsInstallPath.GetFile("cookiecutter_load.py");
            return await RunPythonScript(interpreterPath, scriptPath, string.Format("\"{0}\" \"{1}\"", templateFolderPath, userConfigFilePath));
        }

        private async Task<ProcessOutputResult> RunCheckScript(string interpreterPath) {
            var scriptPath = PythonToolsInstallPath.GetFile("cookiecutter_check.py");
            return await RunPythonScript(interpreterPath, scriptPath, "");
        }

        private async Task<ProcessOutputResult> RunRunScript(string interpreterPath, string templateFolderPath, string userConfigFilePath, string outputFolderPath, string contextPath) {
            var scriptPath = PythonToolsInstallPath.GetFile("cookiecutter_run.py");
            return await RunPythonScript(interpreterPath, scriptPath, GetRunArguments(templateFolderPath, userConfigFilePath, outputFolderPath, contextPath));
        }

        private static string GetRunArguments(string templateFolderPath, string userConfigFilePath, string outputFolderPath, string contextFilePath) {
            return string.Format("\"{0}\" \"{1}\" \"{2}\" \"{3}\"", contextFilePath, templateFolderPath, outputFolderPath, userConfigFilePath);
        }

        private async Task<ProcessOutputResult> RunPythonScript(string interpreterPath, string script, string parameters, bool showWindow = false) {
            ProcessOutput output = null;
            var arguments = string.Format("\"{0}\" {1}", script, parameters);
            if (showWindow) {
                output = ProcessOutput.RunVisible(interpreterPath, arguments);
            } else {
                output = ProcessOutput.RunHiddenAndCapture(interpreterPath, arguments);
            }

            using (output) {
                await output;

                var r = new ProcessOutputResult() {
                    ExitCode = output.ExitCode,
                    StandardOutputLines = output.StandardOutputLines.ToArray(),
                    StandardErrorLines = output.StandardErrorLines.ToArray(),
                };

                if (r.ExitCode < 0) {
                    throw new ProcessException(r);
                }

                return r;
            }
        }

        private void MoveToDesiredFolder(string desiredFolder, string tempFolder) {
            if (!Directory.Exists(desiredFolder)) {
                Directory.CreateDirectory(desiredFolder);
            }

            // Cookiecutter templates generate into a single subfolder that doesn't have a fixed name
            string generatedFolder = tempFolder;
            var subfolders = Directory.GetDirectories(tempFolder);
            if (subfolders.Length == 1) {
                generatedFolder = subfolders[0];
            } else {
                throw new InvalidOperationException("Cookiecutter generated files must have a templated folder.");
            }

            MoveFilesAndFolders(generatedFolder, desiredFolder);

            try {
                Directory.Delete(tempFolder);
            } catch (IOException) {
            }
        }

        private void CopyFiles(string sourceFolderPath, string targetFolderPath) {
            Directory.CreateDirectory(targetFolderPath);

            foreach (var sourceFilePath in PathUtils.EnumerateFiles(sourceFolderPath)) {
                var fileName = PathUtils.GetFileOrDirectoryName(sourceFilePath);
                var targetFilePath = Path.Combine(targetFolderPath, fileName);
                File.Copy(sourceFilePath, targetFilePath, true);
            }
        }

        private void MoveFilesAndFolders(string sourceFolderPath, string targetFolderPath) {
            var subFolderRelativePaths = PathUtils.EnumerateDirectories(sourceFolderPath, recurse: true, fullPaths: false).Concat(new string[] { "." });
            foreach (var subFolderRelativePath in subFolderRelativePaths) {
                CopyFiles(Path.Combine(sourceFolderPath, subFolderRelativePath), Path.Combine(targetFolderPath, subFolderRelativePath));
            }

            Directory.Delete(sourceFolderPath, true);
        }
    }
}
