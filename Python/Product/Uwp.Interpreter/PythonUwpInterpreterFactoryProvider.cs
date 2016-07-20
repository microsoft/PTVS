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
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Uwp.Interpreter {
    [InterpreterFactoryId(InterpreterFactoryProviderId)]
    [Export(typeof(IPythonInterpreterFactoryProvider))]
    class PythonUwpInterpreterFactoryProvider : IPythonInterpreterFactoryProvider {
        private HashSet<IPythonInterpreterFactory> _factories = null;
        public const string InterpreterFactoryProviderId = "Uwp";

        private const string PythonUwpSdkTargetsFile = @"DesignTime\CommonConfiguration\Neutral\CPython.targets";

        public event EventHandler InterpreterFactoriesChanged;

        public PythonUwpInterpreterFactoryProvider() {
        }

        private IDictionary<string, IPythonInterpreterFactory> FindFactoriesFromDirectories(params string[] directories) {
            var factoryMap = new Dictionary<string, IPythonInterpreterFactory>();

            try {
                if (directories != null) {
                    for (int i = 0; i < directories.Length; i++) {
                        var rootDirectoryInfo = new DirectoryInfo(directories[i]);

                        if (rootDirectoryInfo.Exists) {
                            foreach (var dirInfo in rootDirectoryInfo.EnumerateDirectories()) {
                                Version pythonUwpVersion;

                                if (Version.TryParse(dirInfo.Name, out pythonUwpVersion)) {
                                    var targetsFile = dirInfo.GetFiles(PythonUwpSdkTargetsFile).FirstOrDefault();
                                    var factoryName = string.Join(" ", rootDirectoryInfo.Name, dirInfo.Name);

                                    if (targetsFile != null) {
                                        // This will add a new or overwrite factory with new factory found
                                        // Ordering of the directories means that the last directory specified will 
                                        // win the battle of conflicting factory names
                                        factoryMap[factoryName] = new PythonUwpInterpreterFactory(
                                            new InterpreterConfiguration(
                                                pythonUwpVersion.ToString(),
                                                factoryName,
                                                dirInfo.FullName,
                                                targetsFile.FullName,
                                                null,
                                                null,
                                                null,
                                                ProcessorArchitecture.None,
                                                pythonUwpVersion,
                                                InterpreterUIMode.CannotBeDefault | InterpreterUIMode.SupportsDatabase
                                            )
                                        );
                                    }
                                }
                            }
                        }
                    }
                }
            } catch (IOException) {
                // IOException is not critical here, just means we cannot interrogate for factories at this point
            }

            return factoryMap;
        }

        private void DiscoverFactories() {
            if (_factories == null) {
                _factories = new HashSet<IPythonInterpreterFactory>();

                var userSdkInstallDir = Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Microsoft\VisualStudio\%VISUALSTUDIOVERSION%Exp\Extensions\Microsoft\Python UWP");
                var sdkInstallDir = Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\Microsoft SDKs\Windows\v10.0\ExtensionSDKs\Python UWP");

                var factoryMap = FindFactoriesFromDirectories(sdkInstallDir, userSdkInstallDir);

                if (factoryMap.Count > 0) {
                    foreach (var factory in factoryMap.Values) {
                        _factories.Add(factory);
                    }

                    var factoriesChanged = InterpreterFactoriesChanged;

                    if (factoriesChanged != null) {
                        factoriesChanged(this, new EventArgs());
                    }
                }
            }
        }

        public IEnumerable<IPythonInterpreterFactory> GetInterpreterFactories() {
            DiscoverFactories();
            return _factories;
        }

        public IEnumerable<InterpreterConfiguration> GetInterpreterConfigurations() {
            return GetInterpreterFactories().Select(x => x.Configuration);
        }

        public IPythonInterpreterFactory GetInterpreterFactory(string id) {
            return GetInterpreterFactories()
                .Where(x => x.Configuration.Id == id)
                .FirstOrDefault();
        }

        public object GetProperty(string id, string propName) => null;
    }
}