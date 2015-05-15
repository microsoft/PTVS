/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Uwp.Interpreter {
    [Export(typeof(IPythonInterpreterFactoryProvider))]
    class PythonUwpInterpreterFactoryProvider : IPythonInterpreterFactoryProvider {
        private HashSet<IPythonInterpreterFactory> _factories = null;

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
                                                    dirInfo.FullName,
                                                    targetsFile.FullName,
                                                    null,
                                                    null,
                                                    null,
                                                    ProcessorArchitecture.None,
                                                    pythonUwpVersion,
                                                    InterpreterUIMode.CannotBeDefault
                                                    ),
                                                factoryName);
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
    }
}