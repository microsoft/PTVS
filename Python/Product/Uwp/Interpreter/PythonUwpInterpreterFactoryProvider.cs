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

        public event EventHandler InterpreterFactoriesChanged;

        public PythonUwpInterpreterFactoryProvider() {
        }

        private void DiscoverFactories() {
            if (_factories == null) {
                var userSdkInstallDir = new DirectoryInfo(Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Microsoft\VisualStudio\%VISUALSTUDIOVERSION%Exp\Extensions\Microsoft\Python UWP"));
                var sdkInstallDir = new DirectoryInfo(Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\Microsoft SDKs\Windows\v10.0\ExtensionSDKs\Python UWP"));

                _factories = new HashSet<IPythonInterpreterFactory>();

                if (userSdkInstallDir.Exists) {
                    foreach (var dirInfo in userSdkInstallDir.EnumerateDirectories()) {
                        var targetsFile = dirInfo.GetFiles("CPython.targets", SearchOption.AllDirectories).FirstOrDefault();

                        if (targetsFile != null) {
                            _factories.Add(
                                new PythonUwpInterpreterFactory(
                                    new InterpreterConfiguration(
                                        dirInfo.FullName,
                                        targetsFile.FullName,
                                        null,
                                        null,
                                        null,
                                        ProcessorArchitecture.None,
                                        new Version(dirInfo.Name),
                                        InterpreterUIMode.CannotBeDefault
                                        ), 
                                    string.Join(" ", userSdkInstallDir.Name, dirInfo.Name)));
                        }
                    }
                }

                if (sdkInstallDir.Exists) {
                    foreach (var dirInfo in sdkInstallDir.EnumerateDirectories()) {
                        var targetsFile = dirInfo.GetFiles("CPython.targets", SearchOption.AllDirectories).FirstOrDefault();

                        if (targetsFile != null) {
                            _factories.Add(
                                new PythonUwpInterpreterFactory(
                                    new InterpreterConfiguration(
                                        dirInfo.FullName,
                                        targetsFile.FullName,
                                        null,
                                        null,
                                        null,
                                        ProcessorArchitecture.None,
                                        new Version(dirInfo.Name),
                                        InterpreterUIMode.CannotBeDefault
                                        ),
                                    string.Join(" ", sdkInstallDir.Name, dirInfo.Name)));
                        }
                    }
                }

                if (_factories.Count > 0) {
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