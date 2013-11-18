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
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.BuildTasks {
    /// <summary>
    /// Creates an item representing a command.
    /// </summary>
    public sealed class PythonCommand : ITask {
        public const string TargetTypeKey = "TargetType";
        public const string ArgumentsKey = "Arguments";
        public const string WorkingDirectoryKey = "WorkingDirectory";
        public const string EnvironmentKey = "Environment";
        public const string ExecuteInKey = "ExecuteIn";

        private readonly string _projectPath;

        internal PythonCommand(string projectPath, IBuildEngine buildEngine) {
            BuildEngine = buildEngine;
            _projectPath = projectPath;
        }

        /// <summary>
        /// A filename or Python module name.
        /// </summary>
        [Required]
        public string Target { get; set; }

        /// <summary>
        /// One of 'executable' (default), 'script' or 'module'.
        /// </summary>
        public string TargetType { get; set; }

        /// <summary>
        /// The arguments to pass to the command.
        /// </summary>
        public string Arguments { get; set; }

        /// <summary>
        /// The working directory for the command.
        /// </summary>
        public string WorkingDirectory { get; set; }

        /// <summary>
        /// Environment variables for the command. The format is
        /// NAME1=VALUE1;NAME2=VALUE2;'NAME3=VALUE3a;VALUE3b'. Quotes must
        /// precede the variable name and can be single or double quotes.
        /// </summary>
        public string Environment { get; set; }

        /// <summary>
        /// False to execute the command immediately. Defaults to true.
        /// </summary>
        public bool DeferExecution { get; set; }

        /// <summary>
        /// One of 'console' (default), 'repl', 'output' or 'none'.
        /// </summary>
        public string ExecuteIn { get; set; }

        /// <summary>
        /// The created command.
        /// </summary>
        [Output]
        public ITaskItem[] Command { get; private set; }

        private static string SplitEnvironment(string source) {
            var result = new StringBuilder();
            foreach (var line in source.Split('\r', '\n')) {
                if (string.IsNullOrWhiteSpace(line)) {
                    continue;
                }

                result.AppendLine(line.TrimStart());
            }

            return result.ToString();
        }

        public bool Execute() {
            var cmd = new TaskItem(Target, new Dictionary<string, string> {
                { TargetTypeKey, TargetType ?? "" },
                { ArgumentsKey, Arguments ?? "" },
                { WorkingDirectoryKey, WorkingDirectory ?? "" },
                { EnvironmentKey, SplitEnvironment(Environment ?? "") },
                { ExecuteInKey, ExecuteIn ?? "" }
            });

            Command = new[] { cmd };

            if (!DeferExecution) {
                return ExecuteNow();
            }

            return true;
        }

        private bool ExecuteNow() {
            var psi = new ProcessStartInfo();

            if ("module".Equals(TargetType, StringComparison.InvariantCultureIgnoreCase)) {
                // We need the active environment to run these commands.
                var resolver = new ResolveEnvironment(_projectPath, BuildEngine);
                if (!resolver.Execute()) {
                    return false;
                }

                psi.FileName = resolver.InterpreterPath;
                psi.Arguments = string.Format("-m {0} {1}", Target, Arguments);
            } else if ("script".Equals(TargetType, StringComparison.InvariantCultureIgnoreCase)) {
                // We need the active environment to run these commands.
                var resolver = new ResolveEnvironment(_projectPath, BuildEngine);
                if (!resolver.Execute()) {
                    return false;
                }

                psi.FileName = resolver.InterpreterPath;
                psi.Arguments = string.Format("\"{0}\" {1}", Target, Arguments);
            } else {
                psi.FileName = Target;
                psi.Arguments = Arguments;
            }

            psi.WorkingDirectory = WorkingDirectory;
            foreach (var line in Environment.Split('\r', '\n')) {
                int equals = line.IndexOf('=');
                if (equals > 0) {
                    psi.EnvironmentVariables[line.Substring(0, equals)] = line.Substring(equals + 1);
                }
            }

            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            using (var process = Process.Start(psi)) {
                process.OutputDataReceived += Process_OutputDataReceived;
                process.ErrorDataReceived += Process_ErrorDataReceived;
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                return process.ExitCode == 0;
            }
        }

        private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e) {
            if (!string.IsNullOrEmpty(e.Data)) {
                BuildEngine.LogMessageEvent(new BuildMessageEventArgs(
                    e.Data,
                    "",
                    "PythonCommand",
                    MessageImportance.Normal
                ));
            }
        }

        private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e) {
            if (!string.IsNullOrEmpty(e.Data)) {
                BuildEngine.LogMessageEvent(new BuildMessageEventArgs(
                    e.Data,
                    "",
                    "PythonCommand",
                    MessageImportance.High
                ));
            }
        }

        public IBuildEngine BuildEngine { get; set; }
        public ITaskHost HostObject { get; set; }
    }

    /// <summary>
    /// Constructs PythonCommand task objects.
    /// </summary>
    public sealed class PythonCommandFactory : TaskFactory<PythonCommand> {
        public override ITask CreateTask(IBuildEngine taskFactoryLoggingHost) {
            return new PythonCommand(Properties["ProjectPath"], taskFactoryLoggingHost);
        }
    }

}
