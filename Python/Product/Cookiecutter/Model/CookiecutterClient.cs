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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CookiecutterTools.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Microsoft.CookiecutterTools.Model {
    class CookiecutterClient : ICookiecutterClient {
        private readonly CookiecutterPythonInterpreter _interpreter;
        private readonly string _envFolderPath;
        private readonly string _envInterpreterPath;
        private readonly Redirector _redirector;

        public bool CookiecutterInstalled {
            get {
                if (!File.Exists(_envInterpreterPath)) {
                    return false;
                }

                return true;
            }
        }

        public CookiecutterClient(CookiecutterPythonInterpreter interpreter, Redirector redirector) {
            _interpreter = interpreter;
            var localAppDataFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _envFolderPath = Path.Combine(localAppDataFolderPath, "Microsoft", "CookiecutterTools", "env");
            _envInterpreterPath = Path.Combine(_envFolderPath, "scripts", "python.exe");
            _redirector = redirector;
        }

        public async Task<bool> IsCookiecutterInstalled() {
            if (!File.Exists(_envInterpreterPath)) {
                return false;
            }

            try {
                var result = await RunCheckScript(_envInterpreterPath);
                return result.StandardOutputLines.FirstOrDefault() == "ok";
            } catch (Exception e) {
                Trace.WriteLine(e.Message);
                return false;
            }
        }

        public async Task CreateCookiecutterEnv() {
            // Create a virtual environment using the global interpreter
            var interpreterPath = _interpreter.InterpreterExecutablePath;
            var output = ProcessOutput.RunHiddenAndCapture(interpreterPath, "-m", "venv", _envFolderPath);

            await WaitForOutput(interpreterPath, output);
        }

        public async Task InstallPackage() {
            // Install the package into the virtual environment
            var output = ProcessOutput.RunHiddenAndCapture(_envInterpreterPath, "-m", "pip", "install", "cookiecutter<1.5");

            await WaitForOutput(_envInterpreterPath, output);
        }

        public async Task<ContextItem[]> LoadContextAsync(string localTemplateFolder, string userConfigFilePath) {
            if (localTemplateFolder == null) {
                throw new ArgumentNullException(nameof(localTemplateFolder));
            }

            var items = new List<ContextItem>();

            var result = await RunGenerateContextScript(_redirector, _envInterpreterPath, localTemplateFolder, userConfigFilePath);
            var contextJson = string.Join(Environment.NewLine, result.StandardOutputLines);
            var context = (JToken)JObject.Parse(contextJson).SelectToken("cookiecutter");
            if (context != null) {
                JProperty vsExtrasProp = null;

                foreach (JProperty prop in context) {
                    // Properties that start with underscore are for internal use,
                    // and cookiecutter doesn't prompt for them.
                    if (!prop.Name.StartsWith("_")) {
                        if (prop.Value.Type == JTokenType.String ||
                            prop.Value.Type == JTokenType.Integer ||
                            prop.Value.Type == JTokenType.Float) {
                            items.Add(new ContextItem(prop.Name, Selectors.String, prop.Value.ToString()));
                        } else if (prop.Value.Type == JTokenType.Array) {
                            var elements = new List<string>();
                            JArray ar = prop.Value as JArray;
                            foreach (JToken element in ar) {
                                elements.Add(element.ToString());
                            }
                            items.Add(new ContextItem(prop.Name, Selectors.List, elements[0], elements.ToArray()));
                        } else {
                            throw new InvalidOperationException(string.Format("Unsupported json element type in context file for property '{0}'.", prop.Name));
                        }
                    } else if (prop.Name == "_visual_studio") {
                        vsExtrasProp = prop;
                    }
                }

                if (vsExtrasProp != null) {
                    LoadVisualStudioSpecificContext(items, vsExtrasProp);
                }
            }

            return items.ToArray();
        }

        public async Task GenerateProjectAsync(string localTemplateFolder, string userConfigFilePath, string contextFilePath, string outputFolderPath) {
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

            var result = await RunRunScript(_redirector, _envInterpreterPath, localTemplateFolder, userConfigFilePath, tempFolder, contextFilePath);
            MoveToDesiredFolder(outputFolderPath, tempFolder);
        }

        private void LoadVisualStudioSpecificContext(List<ContextItem> items, JProperty vsExtrasProp) {
            // This section reads additional metadata for Visual Studio.
            // All fields are optional, but if they are specified, they are validated.
            //
            // Example:
            // "_visual_studio" : {
            //   "var1" : {
            //     "description" : "description for var 1",
            //     "selector" : "yesno"
            //   },
            //   "var2" : {
            //     "description" : "description for var 2",
            //     "selector" : "connection"
            //   }
            // }
            //
            // Valid values for selector:
            // - string
            // - list
            // - yesno: generates 'y' or 'n'
            // - connection
            if (vsExtrasProp.Value.Type == JTokenType.Object) {
                var vsExtrasObj = (JObject)vsExtrasProp.Value;
                foreach (JProperty prop in vsExtrasObj.Properties()) {
                    var item = items.SingleOrDefault(ctx => ctx.Name == prop.Name);
                    if (item != null) {
                        if (prop.Value.Type == JTokenType.Object) {
                            var itemObj = (JObject)prop.Value;
                            ReadDescription(item, itemObj);
                            ReadSelector(item, itemObj);
                        } else {
                            WrongJsonType(prop.Name, JTokenType.Object, prop.Value.Type);
                        }
                    } else {
                        ReferenceNotFound(prop.Name);
                    }
                }
            } else {
                WrongJsonType("_visual_studio", JTokenType.Object, vsExtrasProp.Value.Type);
            }
        }

        private JToken ReadDescription(ContextItem item, JObject itemObj) {
            var descriptionToken = itemObj.SelectToken("description");
            if (descriptionToken != null) {
                if (descriptionToken.Type == JTokenType.String) {
                    item.Description = descriptionToken.Value<string>();
                } else {
                    WrongJsonType("description", JTokenType.String, descriptionToken.Type);
                }
            }

            return descriptionToken;
        }

        private void ReadSelector(ContextItem item, JObject itemObj) {
            var selectorToken = itemObj.SelectToken("selector");
            if (selectorToken != null) {
                if (selectorToken.Type == JTokenType.String) {
                    item.Selector = selectorToken.Value<string>();
                } else {
                    WrongJsonType("selector", JTokenType.String, selectorToken.Type);
                }
            }
        }

        private void WrongJsonType(string name, JTokenType expected, JTokenType actual) {
            _redirector.WriteErrorLine(string.Format("'{0}' from _visual_studio section in context file should be of type '{1}', instead of '{2}'.", name, expected, actual));
        }

        private void ReferenceNotFound(string name) {
            _redirector.WriteErrorLine(string.Format("'{0}' is referenced from _visual_studio section in context file but was not found.", name));
        }

        private static async Task<ProcessOutputResult> RunGenerateContextScript(Redirector redirector, string interpreterPath, string templateFolderPath, string userConfigFilePath) {
            var scriptPath = PythonToolsInstallPath.GetFile("cookiecutter_load.py");
            return await RunPythonScript(redirector, interpreterPath, scriptPath, string.Format("\"{0}\" \"{1}\"", templateFolderPath, userConfigFilePath));
        }

        private static async Task<ProcessOutputResult> RunCheckScript(string interpreterPath) {
            var scriptPath = PythonToolsInstallPath.GetFile("cookiecutter_check.py");
            var output = ProcessOutput.RunHiddenAndCapture(interpreterPath, scriptPath);
            return await WaitForOutput(interpreterPath, output);
        }

        private static async Task<ProcessOutputResult> RunRunScript(Redirector redirector, string interpreterPath, string templateFolderPath, string userConfigFilePath, string outputFolderPath, string contextPath) {
            var scriptPath = PythonToolsInstallPath.GetFile("cookiecutter_run.py");
            return await RunPythonScript(redirector, interpreterPath, scriptPath, GetRunArguments(templateFolderPath, userConfigFilePath, outputFolderPath, contextPath));
        }

        private static string GetRunArguments(string templateFolderPath, string userConfigFilePath, string outputFolderPath, string contextFilePath) {
            return string.Format("\"{0}\" \"{1}\" \"{2}\" \"{3}\"", contextFilePath, templateFolderPath, outputFolderPath, userConfigFilePath);
        }

        private static async Task<ProcessOutputResult> RunPythonScript(Redirector redirector, string interpreterPath, string script, string parameters) {
            var outputLines = new List<string>();
            var errorLines = new List<string>();

            ProcessOutput output = null;
            var arguments = string.Format("\"{0}\" {1}", script, parameters);
            var listRedirector = new ListRedirector(outputLines, errorLines);
            var outerRedirector = new TeeRedirector(redirector, listRedirector);

            output = ProcessOutput.Run(interpreterPath, new string[] { arguments }, null, null, false, outerRedirector);

            var result = await WaitForOutput(interpreterPath, output);
            result.StandardOutputLines = outputLines.ToArray();
            result.StandardErrorLines = errorLines.ToArray();

            return result;
        }

        private static async Task<ProcessOutputResult> WaitForOutput(string interpreterPath, ProcessOutput output) {
            using (output) {
                await output;

                var r = new ProcessOutputResult() {
                    ExeFileName = interpreterPath,
                    ExitCode = output.ExitCode,
                    StandardOutputLines = output.StandardOutputLines?.ToArray(),
                    StandardErrorLines = output.StandardErrorLines?.ToArray(),
                };

                // All our python scripts will return 0 if successful
                if (r.ExitCode != 0) {
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

            foreach (var sourceFilePath in PathUtils.EnumerateFiles(sourceFolderPath, recurse: false, fullPaths: true)) {
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
