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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CookiecutterTools.Infrastructure;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Newtonsoft.Json.Linq;

namespace Microsoft.CookiecutterTools.Model {
    class CookiecutterClient : ICookiecutterClient {
        private readonly IServiceProvider _provider;
        private readonly CookiecutterPythonInterpreter _interpreter;
        private readonly string _expectedEnvFolderPath;
        private string _envFolderPath;
        private string _envInterpreterPath;
        private readonly Redirector _redirector;


        internal string DefaultBasePath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);


        public bool CookiecutterInstalled {
            get {
                return File.Exists(_envInterpreterPath);
            }
        }

        public CookiecutterClient(IServiceProvider provider, CookiecutterPythonInterpreter interpreter, Redirector redirector) {
            _provider = provider;
            _interpreter = interpreter;
            var localAppDataFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            _redirector = redirector;

            // This is where the env *should* be created, but not necessarily where it will live on disk.
            // See the GetRealPath function for more details
            _expectedEnvFolderPath = Path.Combine(localAppDataFolderPath, "Microsoft", "CookiecutterTools", "env");

            // Get the real paths to the env and interpreter, in case they've been redirected
            _envFolderPath = Task.Run(() => GetRealPath(_expectedEnvFolderPath)).Result;
            _envInterpreterPath = GetInterpreterPathFromEnvFolderPath(_envFolderPath);
        }

        public async Task<bool> IsCookiecutterInstalled() {
            if (!CookiecutterInstalled) {
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
            try {
                await CreateVenvWithoutPipThenInstallPip();
            } catch (ProcessException ex) {
                // critical exception is needed for EnsureCookiecutterIsInstalledAsync() to fail properly
                var errMsg = Strings.InstallingCookiecutterCreateEnvFailed.FormatUI(_envFolderPath);
                throw new CriticalException(errMsg, ex);
            }
        }

        private string GetInterpreterPathFromEnvFolderPath(string envFolderPath) {
            return Path.Combine(envFolderPath, "scripts", "python.exe");
        }

        // If the global interpreter comes from the Microsoft Store, and you try to create a venv
        // under %localappdata%, the venv will actually be created under the python install localcache,
        // and a redirect will be put in place that is only understood by the python.exe used to create
        // the venv.
        // Therefore, we have to use the python intepreter to check the real path of the venv,
        // in case it's been redirected.
        private async Task<string> GetRealPath(string path) {

            // if we can see the path, it hasn't been redirected, so no need to call into python
            if (Directory.Exists(path) || File.Exists(path)) {
                return path;
            }

            // get the redirected path from python
            var command = $"import os; print(os.path.realpath(r'{ path }'))";
            var output = ProcessOutput.Run (
                _interpreter.InterpreterExecutablePath,
                new[] { "-c", command },
                null,
                null,
                false,
                null
            );

            var result = await WaitForOutput(_interpreter.InterpreterExecutablePath, output);
            return result.StandardOutputLines.FirstOrDefault();
        }

        private async Task CreateVenvWithoutPipThenInstallPip() {
            
            // The venv is not guaranteed to be created where expected, see GetRealPath() for more information.

            // Also, Python has a bug (https://bugs.python.org/issue45337) where it doesn't
            // keep track of the real location of the redirected venv when creating a venv with pip installed.
            // In this case, the call to python.exe will have an exit code of 106.

            // Therefore, the workaround is the following:
            // 1. Create the venv WITHOUT PIP every time
            // 2. Run python and check os.path.realpath against the expected venv path
            // 3. If the real path comes back different, that's the real venv path.
            // 5. Install pip using python.exe and the real venv path.

            RemoveExistingVenv();

            // create the venv without pip installed
            _redirector.WriteLine(Strings.InstallingCookiecutterCreateEnvWithoutPip.FormatUI(_expectedEnvFolderPath));
            var output = ProcessOutput.Run(
                _interpreter.InterpreterExecutablePath,
                new[] { "-m", "venv", _expectedEnvFolderPath, "--without-pip" },
                null,
                null,
                false,
                _redirector
            );
            await WaitForOutput(_interpreter.InterpreterExecutablePath, output);

            // If we get here, the environment was created successfully.
            // Update the envFolderPath, in case it's been redirected.
            _envFolderPath = await GetRealPath(_expectedEnvFolderPath);
            _envInterpreterPath = GetInterpreterPathFromEnvFolderPath(_envFolderPath);

            // install pip in the new environment, wherever it is
            _redirector.WriteLine(Strings.InstallingCookiecutterInstallPip.FormatUI(_envFolderPath));
            var pipScriptPath = PythonToolsInstallPath.GetFile("pip_downloader.py");
            output = ProcessOutput.Run(
                _envInterpreterPath,
                new[] { pipScriptPath },
                _interpreter.PrefixPath,
                null,
                false,
                _redirector
            );
            await WaitForOutput(_interpreter.InterpreterExecutablePath, output);
        }

        private void RemoveExistingVenv() {
            if (Directory.Exists(_envFolderPath)) {
                _redirector.WriteLine(Strings.InstallingCookiecutterDeleteEnv.FormatUI(_envFolderPath));
                try {
                    Directory.Delete(_envFolderPath, true);
                } catch (DirectoryNotFoundException) {
                }
            }
        }

        public async Task InstallPackage() {
            _redirector.WriteLine(Strings.InstallingCookiecutterInstallPackages.FormatUI(_envFolderPath));
            var output = ProcessOutput.Run(
                _envInterpreterPath,
                new[] { "-m", "pip", "install", "cookiecutter<1.5" },
                null,
                null,
                false,
                _redirector
            );

            await WaitForOutput(_envInterpreterPath, output);
        }

        public async Task<TemplateContext> LoadUnrenderedContextAsync(string localTemplateFolder, string userConfigFilePath) {
            if (localTemplateFolder == null) {
                throw new ArgumentNullException(nameof(localTemplateFolder));
            }

            var unrenderedContext = new TemplateContext();

            var result = await RunGenerateContextScript(_redirector, _envInterpreterPath, localTemplateFolder, userConfigFilePath);
            var contextJson = string.Join(Environment.NewLine, result.StandardOutputLines);
            var context = (JToken)JObject.Parse(contextJson).SelectToken("cookiecutter");
            if (context != null) {
                JProperty vsExtrasProp = null;

                foreach (JProperty prop in context) {
                    // Properties that start with underscore are for internal use,
                    // and cookiecutter doesn't prompt for them.
                    if (!prop.Name.StartsWithOrdinal("_")) {
                        if (prop.Value.Type == JTokenType.String ||
                            prop.Value.Type == JTokenType.Integer ||
                            prop.Value.Type == JTokenType.Float) {
                            unrenderedContext.Items.Add(new ContextItem(prop.Name, Selectors.String, prop.Value.ToString()));
                        } else if (prop.Value.Type == JTokenType.Array) {
                            var elements = new List<string>();
                            JArray ar = prop.Value as JArray;
                            foreach (JToken element in ar) {
                                elements.Add(element.ToString());
                            }
                            unrenderedContext.Items.Add(new ContextItem(prop.Name, Selectors.List, elements[0], elements.ToArray()));
                        } else {
                            throw new InvalidOperationException(Strings.CookiecutterClient_UnsupportedJsonElementTypeForPorperty.FormatUI(prop.Name));
                        }
                    } else if (prop.Name == "_visual_studio") {
                        vsExtrasProp = prop;
                    } else if (prop.Name == "_visual_studio_post_cmds") {
                        ReadCommands(unrenderedContext, prop);
                    }
                }

                if (vsExtrasProp != null) {
                    LoadVisualStudioSpecificContext(unrenderedContext.Items, vsExtrasProp);
                }
            }

            return unrenderedContext;
        }

        public async Task<TemplateContext> LoadRenderedContextAsync(string localTemplateFolder, string userConfigFilePath, string contextPath, string outputFolderPath) {
            if (localTemplateFolder == null) {
                throw new ArgumentNullException(nameof(localTemplateFolder));
            }

            if (contextPath == null) {
                throw new ArgumentNullException(nameof(contextPath));
            }

            if (outputFolderPath == null) {
                throw new ArgumentNullException(nameof(outputFolderPath));
            }

            var renderedContext = new TemplateContext();

            var result = await RunRenderContextScript(_redirector, _envInterpreterPath, localTemplateFolder, userConfigFilePath, outputFolderPath, contextPath);
            var contextJson = string.Join(Environment.NewLine, result.StandardOutputLines);
            var context = (JToken)JObject.Parse(contextJson).SelectToken("cookiecutter");
            if (context != null) {
                foreach (JProperty prop in context) {
                    // Properties that start with underscore are for internal use,
                    // and cookiecutter doesn't prompt for them.
                    if (!prop.Name.StartsWithOrdinal("_")) {
                        if (prop.Value.Type == JTokenType.String ||
                            prop.Value.Type == JTokenType.Integer ||
                            prop.Value.Type == JTokenType.Float) {
                            renderedContext.Items.Add(new ContextItem(prop.Name, Selectors.String, prop.Value.ToString()));
                        } else if (prop.Value.Type == JTokenType.Array) {
                            var elements = new List<string>();
                            JArray ar = prop.Value as JArray;
                            foreach (JToken element in ar) {
                                elements.Add(element.ToString());
                            }
                            renderedContext.Items.Add(new ContextItem(prop.Name, Selectors.List, elements[0], elements.ToArray()));
                        } else {
                            throw new InvalidOperationException(Strings.CookiecutterClient_UnsupportedJsonElementTypeForPorperty.FormatUI(prop.Name));
                        }
                    } else if (prop.Name == "_visual_studio_post_cmds") {
                        // List of commands to run after the folder is opened,
                        // or the files are added to the project.
                        // name and args are the values passed to DTE.ExecuteCommand
                        // args type:
                        //   - it should be of type array if the command is passed multiple arguments
                        //   - it can be of type string for the common single argument scenario,
                        //     this is equivalent to an array of a single element
                        // args value passed to DTE.ExecuteCommand will
                        // be concatenation of all values in the array, each one quoted as necessary
                        // (for example when a value contains a space char)
                        //
                        // cookiecutter._output_folder_path can be used to files inside generated project
                        //
                        // Examples:
                        // "_visual_studio_post_cmds": [
                        // {
                        //   "name": "Cookiecutter.ExternalWebBrowser",
                        //   "args": "https://docs.microsoft.com"
                        // },
                        // {
                        //   "name": "View.WebBrowser",
                        //   "args": "https://docs.microsoft.com"
                        // },
                        // {
                        //   "name": "View.WebBrowser",
                        //   "args": "{{cookiecutter._output_folder_path}}\\readme.html"
                        // },
                        // {
                        //   "name": "File.OpenFile",
                        //   "args": [ "{{cookiecutter._output_folder_path}}\\readme.txt" ]
                        // }
                        // ]
                        //
                        // Special accommodations for switch arguments:
                        // If the switch takes a value, the switch/value pair should be split in 2 array elements
                        // so the value can be properly quoted independently of the switch, whose name isn't quoted
                        // since space is not allowed in the name. The switch name should end with a colon.
                        //
                        // {
                        //   "name": "File.OpenFile",
                        //   "args": ["c:\\my folder\\my file.txt", "/e:", "Source Code (text) Editor"]
                        // }
                        //

                        ReadCommands(renderedContext, prop);
                    }
                }
            }

            return renderedContext;
        }

        private void ReadCommands(TemplateContext renderedContext, JProperty prop) {
            if (prop.Value.Type != JTokenType.Array) {
                WrongJsonType("_visual_studio_post_cmds", JTokenType.Array, prop.Type);
                return;
            }

            foreach (JToken element in (JArray)prop.Value) {
                if (element.Type != JTokenType.Object) {
                    WrongJsonType("_visual_studio_post_cmds element", JTokenType.Object, element.Type);
                    continue;
                }

                var cmd = ReadCommand((JObject)element);
                if (cmd != null) {
                    renderedContext.Commands.Add(cmd);
                }
            }
        }

        private DteCommand ReadCommand(JObject itemObj) {
            string name = null;
            string args = null;

            var nameToken = itemObj.SelectToken("name");
            if (nameToken != null) {
                if (nameToken.Type == JTokenType.String) {
                    name = nameToken.Value<string>();
                } else {
                    WrongJsonType("name", JTokenType.String, nameToken.Type);
                    return null;
                }
            } else {
                MissingProperty("_visual_studio_post_cmds", "name");
                return null;
            }

            var argsToken = itemObj.SelectToken("args");
            if (argsToken != null) {
                if (argsToken.Type == JTokenType.String) {
                    var argValues = new[] { argsToken.Value<string>() };
                    args = BuildArguments(argValues);
                } else if (argsToken.Type == JTokenType.Array) {
                    var argValues = ((JArray)argsToken).Values().Where(t => t.Type == JTokenType.String).Select(t => t.Value<string>());
                    args = BuildArguments(argValues);
                } else {
                    WrongJsonType("args", JTokenType.Array, argsToken.Type);
                    return null;
                }
            }

            return new DteCommand(name, args);
        }

        public async Task<CreateFilesOperationResult> CreateFilesAsync(string localTemplateFolder, string userConfigFilePath, string contextFilePath, string outputFolderPath) {
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
            return await MoveToDesiredFolderAsync(outputFolderPath, tempFolder);
        }

        public Task<string> GetDefaultOutputFolderAsync(string shortName) {
            var shell = _provider?.GetService(typeof(SVsShell)) as IVsShell;
            object o;
            string vspp, baseName;
            if (shell != null &&
                ErrorHandler.Succeeded(shell.GetProperty((int)__VSSPROPID.VSSPROPID_VisualStudioProjDir, out o)) &&
                PathUtils.IsValidPath((vspp = o as string))) {
                baseName = vspp;
            } else {
                baseName = DefaultBasePath;
            }

            var candidate = PathUtils.GetAbsoluteDirectoryPath(baseName, shortName);
            int counter = 1;
            while (Directory.Exists(candidate) || File.Exists(PathUtils.TrimEndSeparator(candidate))) {
                candidate = PathUtils.GetAbsoluteDirectoryPath(baseName, "{0}{1}".FormatInvariant(shortName, ++counter));
            }
            return Task.FromResult(candidate);
        }

        private static string BuildArguments(IEnumerable<string> values) {
            // Examples of valid results:
            // "C:\My Folder\"
            // C:\MyFolder\MyFile.txt /e:"Source Code (text) Editor"
            //
            // Examples of invalid results:
            // C:\My Folder
            // C:\MyFolder\MyFile.txt "/e:Source Code (text) Editor"
            // C:\MyFolder\MyFile.txt /e: "Source Code (text) Editor"
            // C:\MyFolder\MyFile.txt /e:Source Code (text) Editor
            StringBuilder args = new StringBuilder();
            bool insertSpace = false;
            foreach (var val in values) {
                if (insertSpace) {
                    args.Append(" ");
                }

                if (val.EndsWithOrdinal(":")) {
                    args.Append(val);
                    // no space after a switch that takes a value
                    insertSpace = false;
                } else {
                    args.Append(ProcessOutput.QuoteSingleArgument(val));
                    insertSpace = true;
                }
            }

            return args.ToString();
        }

        private void LoadVisualStudioSpecificContext(List<ContextItem> items, JProperty vsExtrasProp) {
            // This section reads additional metadata for Visual Studio.
            // All fields are optional, but if they are specified, they are validated.
            //
            // Example:
            // "_visual_studio" : {
            //   "var1" : {
            //     "label" : "Variable 1",
            //     "description" : "Description for variable 1",
            //     "selector" : "yesno"
            //   },
            //   "var2" : {
            //     "label" : "Variable 2",
            //     "description" : "Description for variable 2",
            //     "url" : "http://azure.microsoft.com",
            //     "selector" : "odbcConnection"
            //   },
            //   "create_vs_project" : {
            //     "visible" : false,
            //     "value_source" : "IsNewProject"
            //   }
            // }
            //
            // Valid values for selector:
            // - string
            // - list
            // - yesno: generates 'y' or 'n'
            // - odbcConnection
            if (vsExtrasProp.Value.Type == JTokenType.Object) {
                var vsExtrasObj = (JObject)vsExtrasProp.Value;
                foreach (JProperty prop in vsExtrasObj.Properties()) {
                    var item = items.SingleOrDefault(ctx => ctx.Name == prop.Name);
                    if (item != null) {
                        if (prop.Value.Type == JTokenType.Object) {
                            var itemObj = (JObject)prop.Value;
                            ReadLabel(item, itemObj);
                            ReadDescription(item, itemObj);
                            ReadUrl(item, itemObj);
                            ReadSelector(item, itemObj);
                            ReadVisible(item, itemObj);
                            ReadValueSource(item, itemObj);
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

        private void ReadLabel(ContextItem item, JObject itemObj) {
            var val = ReadString(itemObj, "label");
            if (val != null) {
                item.Label = val;
            }
        }

        private void ReadDescription(ContextItem item, JObject itemObj) {
            var val = ReadString(itemObj, "description");
            if (val != null) {
                item.Description = val;
            }
        }

        private JToken ReadUrl(ContextItem item, JObject itemObj) {
            var urlToken = itemObj.SelectToken("url");
            if (urlToken != null) {
                if (urlToken.Type == JTokenType.String) {
                    var val = urlToken.Value<string>();
                    Uri uri;
                    if (Uri.TryCreate(val, UriKind.Absolute, out uri)) {
                        if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) {
                            item.Url = val;
                        } else {
                            InvalidUrl(val);
                        }
                    } else {
                        InvalidUrl(val);
                    }
                } else {
                    WrongJsonType("url", JTokenType.String, urlToken.Type);
                }
            }

            return urlToken;
        }

        private void ReadSelector(ContextItem item, JObject itemObj) {
            var val = ReadString(itemObj, "selector");
            if (val != null) {
                item.Selector = val;
            }
        }

        private void ReadValueSource(ContextItem item, JObject itemObj) {
            var val = ReadString(itemObj, "value_source");
            if (val != null) {
                item.ValueSource = val;
            }
        }

        private void ReadVisible(ContextItem item, JObject itemObj) {
            var val = ReadBool(itemObj, "visible");
            if (val != null) {
                item.Visible = val.Value;
            }
        }

        private string ReadString(JObject itemObj, string fieldName) {
            var token = itemObj.SelectToken(fieldName);
            if (token != null) {
                if (token.Type == JTokenType.String) {
                    return token.Value<string>();
                } else {
                    WrongJsonType(fieldName, JTokenType.String, token.Type);
                }
            }

            return null;
        }

        private bool? ReadBool(JObject itemObj, string fieldName) {
            var token = itemObj.SelectToken(fieldName);
            if (token != null) {
                if (token.Type == JTokenType.Boolean) {
                    return token.Value<bool>();
                } else {
                    WrongJsonType(fieldName, JTokenType.Boolean, token.Type);
                }
            }

            return null;
        }

        private void InvalidUrl(string url) {
            _redirector.WriteErrorLine(Strings.CookiecutterClient_Invalidurl.FormatUI(url));
        }

        private void WrongJsonType(string name, JTokenType expected, JTokenType actual) {
            _redirector.WriteErrorLine(Strings.CookiecutterClient_WrongJsonType.FormatUI(name, expected, actual));
        }

        private void ReferenceNotFound(string name) {
            _redirector.WriteErrorLine(Strings.CookiecutterClient_ReferenceNotFound.FormatUI(name));
        }

        private void MissingProperty(string objectName, string propertyName) {
            _redirector.WriteErrorLine(Strings.CookiecutterClient_MissingProperty.FormatUI(propertyName, objectName));
        }

        private static async Task<ProcessOutputResult> RunGenerateContextScript(Redirector redirector, string interpreterPath, string templateFolderPath, string userConfigFilePath) {
            var scriptPath = PythonToolsInstallPath.GetFile("cookiecutter_load.py");
            return await RunPythonScript(redirector, interpreterPath, scriptPath, "\"{0}\" \"{1}\"".FormatInvariant(templateFolderPath, userConfigFilePath));
        }

        private static async Task<ProcessOutputResult> RunRenderContextScript(Redirector redirector, string interpreterPath, string templateFolderPath, string userConfigFilePath, string outputFolderPath, string contextPath) {
            var scriptPath = PythonToolsInstallPath.GetFile("cookiecutter_render.py");
            return await RunPythonScript(redirector, interpreterPath, scriptPath, "\"{0}\" \"{1}\" \"{2}\" \"{3}\"".FormatInvariant(templateFolderPath, userConfigFilePath, PathUtils.TrimEndSeparator(outputFolderPath), contextPath));
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
            return "\"{0}\" \"{1}\" \"{2}\" \"{3}\"".FormatInvariant(contextFilePath, templateFolderPath, outputFolderPath, userConfigFilePath);
        }

        private static async Task<ProcessOutputResult> RunPythonScript(Redirector redirector, string interpreterPath, string script, string parameters) {
            var outputLines = new List<string>();
            var errorLines = new List<string>();

            ProcessOutput output = null;
            var arguments = "\"{0}\" {1}".FormatInvariant(script, parameters);
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

        private async Task<CreateFilesOperationResult> MoveToDesiredFolderAsync(string desiredFolder, string tempFolder) {
            if (!Directory.Exists(desiredFolder)) {
                Directory.CreateDirectory(desiredFolder);
            }

            // Cookiecutter templates generate into a single subfolder that doesn't have a fixed name
            string generatedFolder = tempFolder;
            var subfolders = Directory.GetDirectories(tempFolder);
            if (subfolders.Length == 1) {
                generatedFolder = subfolders[0];
            } else {
                throw new InvalidOperationException(Strings.CookiecutterClient_MoveToDesiredFolderTemplatedFolderNotFound);
            }

            var res = await MoveFilesAndFoldersAsync(generatedFolder, desiredFolder);

            try {
                Directory.Delete(tempFolder, true);
            } catch (IOException) {
            }

            return res;
        }

        private async Task<CreateFilesOperationResult> MoveFilesAndFoldersAsync(string generatedFolder, string targetFolderPath) {
            List<string> createdFolders = new List<string>();
            List<string> createdFiles = new List<string>();
            List<ReplacedFile> replacedFiles = new List<ReplacedFile>();

            Directory.CreateDirectory(targetFolderPath);

            foreach (var folderPath in PathUtils.EnumerateDirectories(generatedFolder, recurse: true, fullPaths: false)) {
                createdFolders.Add(folderPath);

                Directory.CreateDirectory(Path.Combine(targetFolderPath, folderPath));
            }

            foreach (var filePath in PathUtils.EnumerateFiles(generatedFolder, recurse: true, fullPaths: false)) {
                createdFiles.Add(filePath);

                string targetFilePath = Path.Combine(targetFolderPath, filePath);
                string generatedFilePath = Path.Combine(generatedFolder, filePath);

                if (File.Exists(targetFilePath)) {
                    if (!await AreFilesSameAsync(generatedFilePath, targetFilePath)) {
                        // Need to backup the user's file before overwriting it
                        string backupFilePath = GetBackupFilePath(targetFilePath);
                        File.Move(targetFilePath, backupFilePath);
                        File.Move(generatedFilePath, targetFilePath);
                        replacedFiles.Add(new ReplacedFile(filePath, PathUtils.GetRelativeFilePath(targetFolderPath, backupFilePath)));
                    }
                } else {
                    File.Move(generatedFilePath, targetFilePath);
                }
            }

            return new Model.CreateFilesOperationResult(createdFolders.ToArray(), createdFiles.ToArray(), replacedFiles.ToArray());
        }

        private string GetBackupFilePath(string filePath) {
            return PathUtils.GetAvailableFilename(
                Path.GetDirectoryName(filePath),
                Path.GetFileNameWithoutExtension(filePath) + ".bak",
                Path.GetExtension(filePath)
            );
        }

        internal static async Task<bool> AreFilesSameAsync(string file1Path, string file2Path) {
            var length = new FileInfo(file1Path).Length;
            if (length != new FileInfo(file2Path).Length) {
                return false;
            }

            int bufferSize = 32768;
            var buffer1 = new byte[bufferSize];
            var buffer2 = new byte[bufferSize];
            using (var stream1 = new FileStream(file1Path, FileMode.Open, FileAccess.Read))
            using (var stream2 = new FileStream(file2Path, FileMode.Open, FileAccess.Read)) {
                while (length > 0) {
                    var actual1 = await stream1.ReadAsync(buffer1, 0, bufferSize);
                    var actual2 = await stream2.ReadAsync(buffer2, 0, bufferSize);
                    if (actual1 != actual2) {
                        return false;
                    }

                    length -= actual1;

                    if (!buffer1.SequenceEqual(buffer2)) {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
