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

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using TestRunnerInterop;
using TestUtilities.Ben.Demystifier;
using Task = System.Threading.Tasks.Task;

namespace TestUtilities.UI
{
    [ComVisible(true)]
    public sealed class HostedPythonToolsTestResult : IVsHostedPythonToolsTestResult
    {
        public bool IsSuccess { get; set; }
        public string ExceptionType { get; set; }
        public string ExceptionMessage { get; set; }
        public string ExceptionTraceback { get; set; }
    }

    [ComVisible(true)]
    public sealed class HostedPythonToolsTestRunner : IVsHostedPythonToolsTest
    {
        private readonly Assembly _assembly;
        private readonly Guid[] _dependentPackageGuids;

        private readonly Dictionary<Type, object> _activeInstances;

        public HostedPythonToolsTestRunner(Assembly assembly, params Guid[] dependentPackageGuids)
        {
            _assembly = assembly;
            _dependentPackageGuids = dependentPackageGuids ?? new Guid[0];
            _activeInstances = new Dictionary<Type, object>();
        }

        public IVsHostedPythonToolsTestResult Execute(string name, object[] arguments)
        {
            var parts = name.Split('.');
            var type = _assembly.GetType(string.Join(".", parts.Take(parts.Length - 1)));
            if (type != null)
            {
                var method = type.GetMethod(parts.Last(), BindingFlags.Instance | BindingFlags.Public);
                if (method != null)
                {
                    return InvokeTest(type, method, arguments);
                }
            }

            return new HostedPythonToolsTestResult
            {
                IsSuccess = false,
                ExceptionType = typeof(MissingMethodException).FullName,
                ExceptionMessage = $"Failed to find test \"{name}\" in \"{_assembly.FullName}\"",
                ExceptionTraceback = null
            };
        }

        public void Dispose()
        {
            foreach (var inst in _activeInstances.Values)
            {
                (inst as IDisposable)?.Dispose();
            }
            _activeInstances.Clear();
        }

        private IVsHostedPythonToolsTestResult InvokeTest(Type type, MethodInfo method, object[] arguments)
        {
            if (!_activeInstances.TryGetValue(type, out var instance))
            {
                instance = Activator.CreateInstance(type);
                _activeInstances[type] = instance;
            }

            var args = new List<object>();
            var inputArgs = arguments.ToList();
            var sp = ServiceProvider.GlobalProvider;
            var dte = ServiceProvider.GlobalProvider.GetService(typeof(EnvDTE.DTE));
            try
            {
                try
                {
                    var shell = (IVsShell)ServiceProvider.GlobalProvider.GetService(typeof(SVsShell));
                    foreach (var guid in _dependentPackageGuids)
                    {
                        int installed;
                        var pkgGuid = guid;
                        ErrorHandler.ThrowOnFailure(
                            shell.IsPackageInstalled(ref pkgGuid, out installed)
                        );
                        if (installed == 0)
                        {
                            throw new NotSupportedException($"Package {pkgGuid} is not installed");
                        }
                        ErrorHandler.ThrowOnFailure(shell.LoadPackage(pkgGuid, out _));
                    }
                }
                catch (Exception ex)
                {
                    return new HostedPythonToolsTestResult
                    {
                        IsSuccess = false,
                        ExceptionType = ex.GetType().FullName,
                        ExceptionMessage = "Failed to load a dependent VS package." + Environment.NewLine + ex.Message,
                        ExceptionTraceback = new StringBuilder().AppendException(ex).ToString()
                    };
                }

                foreach (var a in method.GetParameters())
                {
                    if (a.ParameterType.IsAssignableFrom(typeof(IServiceProvider)))
                    {
                        args.Add(sp);
                    }
                    else if (a.ParameterType.IsAssignableFrom(typeof(EnvDTE.DTE)))
                    {
                        args.Add(dte);
                    }
                    else if (inputArgs.Count > 0 && a.ParameterType.IsInstanceOfType(inputArgs[0]))
                    {
                        args.Add(inputArgs[0]);
                        inputArgs.RemoveAt(0);
                    }
                    else
                    {
                        args.Add(ConstructParameter(a.ParameterType, sp, dte, inputArgs));
                    }
                }
            }
            catch (Exception ex)
            {
                ex = ExtractRealException(ex);

                return new HostedPythonToolsTestResult
                {
                    IsSuccess = false,
                    ExceptionType = ex.GetType().FullName,
                    ExceptionMessage = "Failed to invoke test method with correct arguments." + Environment.NewLine + ex.Message,
                    ExceptionTraceback = new StringBuilder().AppendException(ex).ToString()
                };
            }

            TestEnvironmentImpl.TestInitialize(30);

            try
            {
                try
                {
                    if (typeof(Task).IsAssignableFrom(method.ReturnType))
                    {
                        ThreadHelper.JoinableTaskFactory.Run(() => (Task)method.Invoke(instance, args.ToArray()));
                    }
                    else
                    {
                        method.Invoke(instance, args.ToArray());
                    }
                }
                finally
                {
                    foreach (var a in args)
                    {
                        if (a == sp || a == dte)
                        {
                            continue;
                        }
                        (a as IDisposable)?.Dispose();
                    }

                    // Do TestCleanup after the arguments are disposed, because
                    // for arguments like VisualStudioApp, that closes
                    // the current project, which cleans up remaining tasks
                    // running for that project, and unfinished tasks are
                    // detected in TestCleanup.
                    TestEnvironmentImpl.TestCleanup();
                }
            }
            catch (Exception ex)
            {
                ex = ExtractRealException(ex);

                return new HostedPythonToolsTestResult
                {
                    IsSuccess = false,
                    ExceptionType = ex.GetType().FullName,
                    ExceptionMessage = ex.Message,
                    ExceptionTraceback = new StringBuilder().AppendException(ex).ToString()
                };
            }

            return new HostedPythonToolsTestResult
            {
                IsSuccess = true
            };
        }

        private static Exception ExtractRealException(Exception ex)
        {
            while (true)
            {
                switch (ex)
                {
                    case TargetInvocationException _ when ex.InnerException != null:
                        ex = ex.InnerException;
                        continue;
                    case AggregateException aggregateException when aggregateException.InnerExceptions.Count > 0:
                        ex = aggregateException.InnerExceptions[0];
                        continue;
                    default:
                        return ex;
                }
            }
        }

        private static object ConstructParameter(Type target, object serviceProvider, object dte, List<object> inputArgs)
        {
            bool hasDefaultConstructor = false;

            foreach (var constructor in target.GetConstructors())
            {
                var args = constructor.GetParameters();
                if (args.Length == 0)
                {
                    hasDefaultConstructor = true;
                }
                if (args.Length != 1)
                {
                    continue;
                }
                if (args[0].ParameterType.IsAssignableFrom(typeof(IServiceProvider)))
                {
                    return target.InvokeMember(null, BindingFlags.CreateInstance, null, null, new[] { serviceProvider });
                }
                if (args[0].ParameterType.IsAssignableFrom(typeof(EnvDTE.DTE)))
                {
                    return target.InvokeMember(null, BindingFlags.CreateInstance, null, null, new[] { dte });
                }
                if (inputArgs.Count > 0)
                {
                    var firstArg = inputArgs[0];
                    if (args[0].ParameterType.IsInstanceOfType(firstArg))
                    {
                        inputArgs.RemoveAt(0);
                        return target.InvokeMember(null, BindingFlags.CreateInstance, null, null, new[] { firstArg });
                    }
                }
            }

            if (hasDefaultConstructor)
            {
                return target.InvokeMember(null, BindingFlags.CreateInstance, null, null, null);
            }

            throw new ArgumentException($"Cannot instantiate {target.FullName}");
        }
    }
}
