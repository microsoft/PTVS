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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project {
    class PythonProjectLaunchProperties : IPythonProjectLaunchProperties {
        private readonly string _arguments;
        private readonly string _workingDir;
        private readonly Dictionary<string, string> _environment;
        private readonly Dictionary<string, string> _environmentWithPaths;

        private readonly string _interpreterPath;
        private readonly string _interpreterArguments;
        private readonly bool? _isWindowsApplication;
        private readonly bool? _isNativeDebugging;

        public static IPythonProjectLaunchProperties Create(IPythonProject2 project) {
            return Create(project, project.Site, null);
        }

        public static IPythonProjectLaunchProperties Create(
            IPythonProject project,
            IServiceProvider site,
            IProjectLaunchProperties properties
        ) {
            var res = properties as IPythonProjectLaunchProperties;
            if (res != null) {
                return res;
            }
            
            res = project as IPythonProjectLaunchProperties;
            if (res != null) {
                // This should be the common case, as we implement
                // IPythonProjectLaunchProperties on our project.
                return res;
            }

            // Backwards compatibility shim to handle project implementations
            // that omit IPythonProjectLaunchProperties.

            string arguments, workingDir;
            Dictionary<string, string> environment, environmentWithPaths;
            properties = properties ?? (project as IProjectLaunchProperties);
            if (properties != null) {
                arguments = properties.GetArguments();
                workingDir = properties.GetWorkingDirectory();
                environment = new Dictionary<string, string>(properties.GetEnvironment(false));
                environmentWithPaths = new Dictionary<string, string>(properties.GetEnvironment(true));
            } else {
                arguments = project.GetProperty(CommonConstants.CommandLineArguments);
                workingDir = project.GetWorkingDirectory();

                environment = ParseEnvironment(project.GetProperty(PythonConstants.EnvironmentSetting));
                environmentWithPaths = new Dictionary<string, string>(environment);
                AddSearchPaths(environmentWithPaths, project, site);
            }

            string strValue;
            bool boolValue;
            bool? isWindowsApplication = null;
            strValue = project.GetProperty(PythonConstants.IsWindowsApplicationSetting);
            if (bool.TryParse(strValue, out boolValue)) {
                isWindowsApplication = boolValue;
            }

            IPythonInterpreterFactory interpreter;

            var ipp3 = project as IPythonProject3;
            if (ipp3 != null) {
                interpreter = ipp3.GetInterpreterFactoryOrThrow();
            } else {
                interpreter = project.GetInterpreterFactory();
                var service = site.GetComponentModel().GetService<IInterpreterOptionsService>();
                if (service == null || interpreter == service.NoInterpretersValue) {
                    throw new NoInterpretersException();
                }
            }

            var interpreterPath = (isWindowsApplication ?? false) ?
                interpreter.Configuration.WindowsInterpreterPath :
                interpreter.Configuration.InterpreterPath;

            var interpreterArguments = project.GetProperty(PythonConstants.InterpreterArgumentsSetting);

            bool? isNativeDebugging = null;
            strValue = project.GetProperty(PythonConstants.EnableNativeCodeDebugging);
            if (bool.TryParse(strValue, out boolValue)) {
                isNativeDebugging = boolValue;
            }

            return new PythonProjectLaunchProperties(
                arguments,
                workingDir,
                environment,
                environmentWithPaths,
                interpreterPath,
                interpreterArguments,
                isWindowsApplication,
                isNativeDebugging
            );
        }

        /// <summary>
        /// Merges two sets of properties.
        /// </summary>
        public static IProjectLaunchProperties Merge(
            IProjectLaunchProperties highPriority,
            IProjectLaunchProperties lowPriority,
            params string[] joinEnviromentValues
        ) {
            var args = highPriority.GetArguments();
            if (string.IsNullOrEmpty(args)) {
                args = lowPriority.GetArguments();
            }

            var workingDir = highPriority.GetWorkingDirectory();
            if (string.IsNullOrEmpty(workingDir)) {
                workingDir = lowPriority.GetWorkingDirectory();
            }

            var joinEnv = new HashSet<string>(joinEnviromentValues, StringComparer.OrdinalIgnoreCase);
            joinEnv.Add("PATH");

            var origHighEnv = highPriority.GetEnvironment(false);
            var origLowEnv = lowPriority.GetEnvironment(false);
            var env = new Dictionary<string, string>(origLowEnv);
            if (origHighEnv != null && origHighEnv.Any()) {
                foreach (var kv in origHighEnv) {
                    string existing;
                    if (joinEnv.Contains(kv.Key) && env.TryGetValue(kv.Key, out existing)) {
                        env[kv.Key] = kv.Value + Path.PathSeparator.ToString() + existing;
                    } else {
                        env[kv.Key] = kv.Value;
                    }
                }
            }

            var origHighSearchEnv = highPriority.GetEnvironment(true);
            var origLowSearchEnv = lowPriority.GetEnvironment(true);
            var envWithPath = new Dictionary<string, string>(env);
            if (origLowSearchEnv != null && origLowSearchEnv.Any()) {
                string existing;
                var newKeys = origLowSearchEnv.Keys.AsEnumerable();
                if (origLowEnv != null) {
                    newKeys = newKeys.Except(origLowEnv.Keys);
                }

                foreach (var key in newKeys) {
                    if (envWithPath.TryGetValue(key, out existing) && !string.IsNullOrEmpty(existing)) {
                        envWithPath[key] = origLowSearchEnv[key] + Path.PathSeparator.ToString() + existing;
                    } else {
                        envWithPath[key] = origLowSearchEnv[key];
                    }
                }

                newKeys = origHighSearchEnv.Keys.AsEnumerable();
                if (origHighEnv != null) {
                    newKeys = newKeys.Except(origHighEnv.Keys);
                }

                foreach (var key in newKeys) {
                    if (envWithPath.TryGetValue(key, out existing) && !string.IsNullOrEmpty(existing)) {
                        envWithPath[key] = origHighSearchEnv[key] + Path.PathSeparator.ToString() + existing;
                    } else {
                        envWithPath[key] = origHighSearchEnv[key];
                    }
                }
            }

            var highPython = highPriority as IPythonProjectLaunchProperties;
            var lowPython = lowPriority as IPythonProjectLaunchProperties;

            string interpreterPath = null, interpreterArgs = null;
            bool? isWindowsApp = null, isNativeDebug = null;

            if (highPython != null) {
                interpreterPath = highPython.GetInterpreterPath();
                interpreterArgs = highPython.GetInterpreterArguments();
                isWindowsApp = highPython.GetIsWindowsApplication();
                isNativeDebug = highPython.GetIsNativeDebuggingEnabled();
            }
            if (lowPython != null) {
                if (string.IsNullOrEmpty(interpreterPath)) {
                    interpreterPath = lowPython.GetInterpreterPath();
                }
                if (string.IsNullOrEmpty(interpreterArgs)) {
                    interpreterArgs = lowPython.GetInterpreterArguments();
                }
                if (isWindowsApp == null) {
                    isWindowsApp = lowPython.GetIsWindowsApplication();
                }
                if (isNativeDebug == null) {
                    isNativeDebug = lowPython.GetIsNativeDebuggingEnabled();
                }
            }

            return new PythonProjectLaunchProperties(
                args,
                workingDir,
                env,
                envWithPath,
                interpreterPath,
                interpreterArgs,
                isWindowsApp,
                isNativeDebug
            );
        }

        private PythonProjectLaunchProperties(
            string arguments,
            string workingDir,
            Dictionary<string, string> environment,
            Dictionary<string, string> environmentWithPaths,
            string interpreterPath,
            string interpreterArguments,
            bool? isWindowsApplication,
            bool? isNativeDebugging
        ) {
            _arguments = arguments;
            _workingDir = workingDir;
            _environment = environment;
            _environmentWithPaths = environmentWithPaths;
            _interpreterPath = interpreterPath;
            _interpreterArguments = interpreterArguments;
            _isWindowsApplication = isWindowsApplication;
            _isNativeDebugging = isNativeDebugging;
        }


        internal static Dictionary<string, string> ParseEnvironment(string userEnv) {
            var res = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(userEnv)) {
                foreach (var envVar in userEnv.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
                    var nameValue = envVar.Split(new[] { '=' }, 2);
                    if (nameValue.Length == 2) {
                        res[nameValue[0]] = nameValue[1];
                    }
                }
            }

            return res;
        }

        internal static void MergeEnvironmentBelow(
            Dictionary<string, string> env,
            IEnumerable<KeyValuePair<string, string>> envBelow,
            bool mergeCurrentEnv = false,
            IEnumerable<string> joinEnvironmentValues = null
        ) {
            var joinEnv = new HashSet<string>(
                joinEnvironmentValues ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase
            );
            joinEnv.Add("PATH");

            if (envBelow != null) {
                foreach (var kv in envBelow) {
                    string existing;
                    if (env.TryGetValue(kv.Key, out existing) && !string.IsNullOrEmpty(existing)) {
                        if (joinEnv.Contains(kv.Key)) {
                            env[kv.Key] = existing + Path.PathSeparator.ToString() + kv.Value;
                        }
                    } else {
                        env[kv.Key] = kv.Value;
                    }
                }
            }

            if (mergeCurrentEnv) {
                foreach (DictionaryEntry kv in Environment.GetEnvironmentVariables()) {
                    string existing;
                    if (env.TryGetValue((string)kv.Key, out existing) && !string.IsNullOrEmpty(existing)) {
                        if (joinEnv.Contains((string)kv.Key)) {
                            env[(string)kv.Key] = existing + Path.PathSeparator.ToString() + (string)kv.Value;
                        }
                    } else {
                        env[(string)kv.Key] = (string)kv.Value;
                    }
                }
            }
        }

        internal static void AddSearchPaths(
            Dictionary<string, string> env,
            IPythonProject project,
            IServiceProvider provider = null
        ) {
            var pathEnv = project.GetProjectAnalyzer().InterpreterFactory.Configuration.PathEnvironmentVariable;
            if (string.IsNullOrEmpty(pathEnv)) {
                return;
            }

            var paths = new List<string>();
            
            string path;
            if (env.TryGetValue(pathEnv, out path)) {
                paths.AddRange(path.Split(Path.PathSeparator));
            }

            paths.AddRange(EnumerateSearchPaths(project));

            if (provider != null && !provider.GetPythonToolsService().GeneralOptions.ClearGlobalPythonPath) {
                paths.AddRange((Environment.GetEnvironmentVariable(pathEnv) ?? "")
                    .Split(Path.PathSeparator)
                    .Where(p => !paths.Contains(p))
                    .ToList());
            }

            env[pathEnv] = string.Join(
                Path.PathSeparator.ToString(),
                paths
            );
        }

        internal static IEnumerable<string> EnumerateSearchPaths(IPythonProject project) {
            var seen = new HashSet<string>();

            var paths = project.GetProperty(PythonConstants.SearchPathSetting);
            if (!string.IsNullOrEmpty(paths)) {
                foreach (var path in paths.Split(';')) {
                    if (string.IsNullOrEmpty(path)) {
                        continue;
                    }

                    var absPath = CommonUtils.GetAbsoluteFilePath(project.ProjectDirectory, path);
                    if (seen.Add(absPath)) {
                        yield return absPath;
                    }
                }
            }

            var interp = project.GetProjectAnalyzer().Interpreter as IPythonInterpreterWithProjectReferences2;
            if (interp != null) {
                foreach (var r in interp.GetReferences()) {
                    if (r.Kind == ProjectReferenceKind.ExtensionModule) {
                        string absPath;
                        try {
                            absPath = CommonUtils.GetAbsoluteFilePath(project.ProjectDirectory, r.Name);
                        } catch (InvalidOperationException) {
                            continue;
                        }

                        if (!string.IsNullOrEmpty(absPath)) {
                            var parentPath = CommonUtils.GetParent(absPath);
                            if (!string.IsNullOrEmpty(parentPath) && seen.Add(parentPath)) {
                                yield return parentPath;
                            }
                        }
                    }
                }
            }
        }

        public string GetArguments() {
            return _arguments;
        }

        public string GetWorkingDirectory() {
            return _workingDir;
        }

        public IDictionary<string, string> GetEnvironment(bool includeSearchPaths) {
            return includeSearchPaths ? _environmentWithPaths : _environment;
        }

        public bool? GetIsWindowsApplication() {
            return _isWindowsApplication;
        }

        public bool? GetIsNativeDebuggingEnabled() {
            return _isNativeDebugging;
        }

        public string GetInterpreterPath() {
            return _interpreterPath;
        }

        public string GetInterpreterArguments() {
            return _interpreterArguments;
        }
    }
}
