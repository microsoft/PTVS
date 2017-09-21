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
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using TestRunnerInterop;
using Task = System.Threading.Tasks.Task;

namespace TestUtilities.UI {
    [ComVisible(true)]
    public sealed class HostedPythonToolsTestResult : IVsHostedPythonToolsTestResult {
        public bool IsSuccess { get; set; }
        public string ExceptionType { get; set; }
        public string ExceptionMessage { get; set; }
        public string ExceptionTraceback { get; set; }
    }

    [ComVisible(true)]
    public sealed class HostedPythonToolsTestRunner : IVsHostedPythonToolsTest {
        private readonly Assembly _assembly;

        private readonly Dictionary<Type, object> _activeInstances;

        public HostedPythonToolsTestRunner(Assembly assembly) {
            _assembly = assembly;
            _activeInstances = new Dictionary<Type, object>();
        }

        public IVsHostedPythonToolsTestResult Execute(string name, object[] arguments) {
            var parts = name.Split(":".ToCharArray(), 2);
            if (parts.Length == 2) {
                var type = _assembly.GetType(parts[0]);
                if (type != null) {
                    var method = type.GetMethod(parts[1], BindingFlags.Instance | BindingFlags.Public);
                    if (method != null) {
                        return InvokeTest(type, method);
                    }
                }
            }

            return new HostedPythonToolsTestResult {
                IsSuccess = false,
                ExceptionType = typeof(MissingMethodException).FullName,
                ExceptionMessage = $"Failed to find test \"{name}\" in \"{_assembly.FullName}\"",
                ExceptionTraceback = null
            };
        }

        public void Dispose() {
            foreach (var inst in _activeInstances.Values) {
                (inst as IDisposable)?.Dispose();
            }
            _activeInstances.Clear();
        }

        private IVsHostedPythonToolsTestResult InvokeTest(Type type, MethodInfo method) {
            object instance;
            if (!_activeInstances.TryGetValue(type, out instance)) {
                instance = Activator.CreateInstance(type);
                _activeInstances[type] = instance;
            }

            var args = new List<object>();
            var sp = ServiceProvider.GlobalProvider;
            var dte = ServiceProvider.GlobalProvider.GetService(typeof(EnvDTE.DTE));
            try {
                foreach (var a in method.GetParameters()) {
                    if (typeof(IServiceProvider).IsAssignableFrom(a.ParameterType)) {
                        args.Add(sp);
                    } else if (typeof(EnvDTE.DTE).IsAssignableFrom(a.ParameterType)) {
                        args.Add(dte);
                    } else {
                        args.Add(ConstructParameter(a.ParameterType, sp, dte));
                    }
                }
            } catch (Exception ex) {
                return new HostedPythonToolsTestResult {
                    IsSuccess = false,
                    ExceptionType = ex.GetType().FullName,
                    ExceptionMessage = "Failed to invoke test method with correct arguments." + Environment.NewLine + ex.Message,
                    ExceptionTraceback = ex.StackTrace
                };
            }

            try {
                try {
                    if (typeof(Task).IsAssignableFrom(method.ReturnType)) {
                        ThreadHelper.JoinableTaskFactory.Run(() => (Task)method.Invoke(instance, args.ToArray()));
                    } else {
                        method.Invoke(instance, args.ToArray());
                    }
                } finally {
                    foreach (var a in args) {
                        if (a == sp || a == dte) {
                            continue;
                        }
                        (a as IDisposable)?.Dispose();
                    }
                }
            } catch (Exception ex) {
                return new HostedPythonToolsTestResult {
                    IsSuccess = false,
                    ExceptionType = ex.GetType().FullName,
                    ExceptionMessage = ex.Message,
                    ExceptionTraceback = ex.StackTrace
                };
            }

            return new HostedPythonToolsTestResult {
                IsSuccess = true
            };
        }

        private static object ConstructParameter(Type target, object serviceProvider, object dte) {
            bool hasDefaultConstructor = false;

            foreach (var constructor in target.GetConstructors()) {
                var args = constructor.GetParameters();
                if (args.Length == 0) {
                    hasDefaultConstructor = true;
                }
                if (args.Length != 1) {
                    continue;
                }
                if (args[0].ParameterType.IsAssignableFrom(typeof(IServiceProvider))) {
                    return target.InvokeMember(null, BindingFlags.CreateInstance, null, null, new[] { serviceProvider });
                }
                if (args[0].ParameterType.IsAssignableFrom(typeof(EnvDTE.DTE))) {
                    return target.InvokeMember(null, BindingFlags.CreateInstance, null, null, new[] { dte });
                }
            }

            if (hasDefaultConstructor) {
                return target.InvokeMember(null, BindingFlags.CreateInstance, null, null, null);
            }

            throw new ArgumentException($"Cannot instantiate {target.FullName}");
        }
    }
}
