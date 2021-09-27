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

namespace Microsoft.PythonTools.BuildTasks
{
    /// <summary>
    /// Creates an item representing a command.
    /// </summary>
    public abstract class PythonCommandTask : Task
    {
        public const string TargetTypeExecutable = "executable";
        public const string TargetTypeScript = "script";
        public const string TargetTypeModule = "module";
        public const string TargetTypeCode = "code";
        public const string TargetTypePip = "pip";

        private static readonly string[] _targetTypes = { TargetTypeExecutable, TargetTypeScript, TargetTypeModule, TargetTypeCode, TargetTypePip };

        public const string ExecuteInConsole = "console";
        public const string ExecuteInConsolePause = "consolepause";
        public const string ExecuteInNone = "none";

        private static readonly string[] _executeIns = { ExecuteInNone, ExecuteInConsole, ExecuteInConsolePause };

        internal PythonCommandTask(string projectPath, IBuildEngine buildEngine)
        {
            BuildEngine = buildEngine;
            ProjectPath = projectPath;
        }

        protected string ProjectPath { get; private set; }

        /// <summary>
        /// A filename or Python module name.
        /// </summary>
        [Required]
        public string Target { get; set; }

        private string _targetType = TargetTypeExecutable;

        /// <summary>
        /// One of 'executable' (default), 'script', 'module', 'code' or 'pip'.
        /// </summary>
        public string TargetType
        {
            get
            {
                return _targetType;
            }
            set
            {
                if (!_targetTypes.Contains(value, StringComparer.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("TargetType must be one of: " + string.Join(", ", _targetTypes.Select(s => '"' + s + '"')));
                }
                _targetType = value;
            }
        }

        private string _executeIn = ExecuteInConsole;

        protected virtual bool IsValidExecuteInValue(string value, out string message)
        {
            message = "ExecuteIn must be one of: " + string.Join(", ", _executeIns.Select(s => '"' + s + '"')); ;
            return _executeIns.Any(s => s.Equals(value, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// One of 'console' (default), 'consolepause', 'repl', 'output' or 'none'.
        /// </summary>
        public string ExecuteIn
        {
            get
            {
                return _executeIn;
            }
            set
            {
                string errorMessage;
                if (value != null && !IsValidExecuteInValue(value, out errorMessage))
                {
                    throw new ArgumentException(errorMessage);
                }
                _executeIn = value;
            }
        }

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
        /// A regular expression used to detect error messages in the output. If not set, error detection is disabled.
        /// </summary>
        /// <remarks>
        /// Only valid when <see cref="ExecuteIn"/> is set to <c>"output"</c>.
        /// The regular expression should use named capture groups to extract and present information about the exception.
        /// The following named groups will be queried:
        /// <list type="bullet">
        ///     <item>
        ///         <term>(?&lt;message&gt;...)</term>
        ///         <description>Text of the error.</description>
        ///     </item>
        ///     <item>
        ///         <term>(?&lt;code&gt;...)</term>
        ///         <description>Error code.</description>
        ///     </item>
        ///     <item>
        ///         <term>(?&lt;filename&gt;...)</term>
        ///         <description>Name of the file for which the error is reported.</description>
        ///     </item>
        ///     <item>
        ///         <term>(?&lt;line&gt;...)</term>
        ///         <description>Line number of the location in the file for which the error reported.</description>
        ///     </item>
        ///     <item>
        ///         <term>(?&lt;column&gt;...)</term>
        ///         <description>Column number of the location in the file for which the error reported.</description>
        ///     </item>
        /// </list>
        /// Any of these can be omitted if absent from a particular error format.
        /// </remarks>
        public string ErrorRegex { get; set; }

        /// <summary>
        /// A regular expression used to detect warning messages in the output. If not set, warning detection is disabled.
        /// </summary>
        /// <remarks>
        /// Only valid when <see cref="ExecuteIn"/> is set to <c>"output"</c>.
        /// See documentation for <see cref="ErrorRegex"/> for a detailed description of the regular expression format.
        /// </remarks>
        public string WarningRegex { get; set; }

        /// <summary>
        /// A regular expression used to detect message messages in the output. If not set, message detection is disabled.
        /// </summary>
        /// <remarks>
        /// Only valid when <see cref="ExecuteIn"/> is set to <c>"output"</c>.
        /// See documentation for <see cref="ErrorRegex"/> for a detailed description of the regular expression format.
        /// </remarks>
        public string MessageRegex { get; set; }

        /// <summary>
        /// A list of package requirements for this command, in setuptools format. If any package from this list is not installed,
        /// pip will be used to install it before running the command.
        /// </summary>
        public string[] RequiredPackages { get; set; }
    }

    public class CreatePythonCommandItem : PythonCommandTask
    {
        public const string TargetTypeKey = "TargetType";
        public const string ArgumentsKey = "Arguments";
        public const string WorkingDirectoryKey = "WorkingDirectory";
        public const string EnvironmentKey = "Environment";
        public const string ExecuteInKey = "ExecuteIn";
        public const string ErrorRegexKey = "ErrorRegex";
        public const string WarningRegexKey = "WarningRegex";
        public const string MessageRegexKey = "MessageRegex";
        public const string RequiredPackagesKey = "RequiredPackages";

        public const string ExecuteInRepl = "repl";
        public const string ExecuteInOutput = "output";
        private static readonly string[] _executeIns = { ExecuteInConsole, ExecuteInConsolePause, ExecuteInRepl, ExecuteInOutput, ExecuteInNone };

        internal CreatePythonCommandItem(string projectPath, IBuildEngine buildEngine)
            : base(projectPath, buildEngine)
        {
        }

        protected override bool IsValidExecuteInValue(string value, out string message)
        {
            message = "ExecuteIn must be one of: " + string.Join(", ", _executeIns.Select(s => '"' + s + '"')); ;
            return _executeIns.Any(s => s.Equals(value, StringComparison.OrdinalIgnoreCase)) ||
                value.StartsWithOrdinal(ExecuteInRepl, ignoreCase: true);
        }

        /// <summary>
        /// The created command.
        /// </summary>
        [Output]
        public ITaskItem[] Command { get; private set; }

        public override bool Execute()
        {
            if (!ExecuteInOutput.Equals(ExecuteIn, StringComparison.OrdinalIgnoreCase) &&
                !(string.IsNullOrEmpty(ErrorRegex) && string.IsNullOrEmpty(WarningRegex) && string.IsNullOrEmpty(MessageRegex)))
            {
                Log.LogError("ErrorRegex and WarningRegex are only valid for ExecuteIn='output'");
                return false;
            }

            var cmd = new TaskItem(Target, new Dictionary<string, string> {
                { TargetTypeKey, TargetType ?? "" },
                { ArgumentsKey, Arguments ?? "" },
                { WorkingDirectoryKey, WorkingDirectory ?? "" },
                { EnvironmentKey, SplitEnvironment(Environment ?? "") },
                { ExecuteInKey, ExecuteIn ?? "" },
                { ErrorRegexKey, ErrorRegex ?? "" },
                { WarningRegexKey, WarningRegex ?? "" },
                { MessageRegexKey, MessageRegex ?? "" },
                { RequiredPackagesKey, RequiredPackages != null ? string.Join(";", RequiredPackages) : "" }
            });

            Command = new[] { cmd };
            return true;
        }

        private static string SplitEnvironment(string source)
        {
            var result = new StringBuilder();
            foreach (var line in source.Split('\r', '\n'))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                result.AppendLine(line.TrimStart());
            }

            return result.ToString();
        }
    }

    public class RunPythonCommand : PythonCommandTask
    {
        private readonly Lazy<List<string>> _consoleOutput = new Lazy<List<string>>();
        private readonly Lazy<List<string>> _consoleError = new Lazy<List<string>>();

        internal RunPythonCommand(string projectPath, IBuildEngine buildEngine)
            : base(projectPath, buildEngine)
        {
        }

        public bool ConsoleToMSBuild
        {
            get;
            set;
        }

        [Output]
        public string ConsoleOutput
        {
            get
            {
                return _consoleOutput.IsValueCreated ?
                    string.Join(System.Environment.NewLine, _consoleOutput.Value) :
                    string.Empty;
            }
        }

        [Output]
        public string ConsoleError
        {
            get
            {
                return _consoleError.IsValueCreated ?
                    string.Join(System.Environment.NewLine, _consoleError.Value) :
                    string.Empty;
            }
        }

        public override bool Execute()
        {
            var psi = new ProcessStartInfo();
            psi.UseShellExecute = false;

            if (TargetTypeExecutable.Equals(TargetType, StringComparison.OrdinalIgnoreCase))
            {
                psi.FileName = Target;
                psi.Arguments = Arguments;
            }
            else
            {
                // We need the active environment to run these commands.
                var resolver = new ResolveEnvironment(ProjectPath, BuildEngine);
                if (!resolver.Execute())
                {
                    return false;
                }
                psi.FileName = resolver.InterpreterPath;

                if (TargetTypeModule.Equals(TargetType, StringComparison.OrdinalIgnoreCase))
                {
                    psi.Arguments = string.Format("-m {0} {1}", Target, Arguments);
                }
                else if (TargetTypeScript.Equals(TargetType, StringComparison.OrdinalIgnoreCase))
                {
                    psi.Arguments = string.Format("\"{0}\" {1}", Target, Arguments);
                }
                else if (TargetTypeCode.Equals(TargetType, StringComparison.OrdinalIgnoreCase))
                {
                    psi.Arguments = string.Format("-c \"{0}\"", Target);
                }

                // If no search paths are read, this is set to an empty string
                // to mask any global setting.
                psi.EnvironmentVariables[resolver.PathEnvironmentVariable] = string.Join(";", resolver.SearchPaths);
            }

            psi.WorkingDirectory = WorkingDirectory;

            if (!string.IsNullOrEmpty(Environment))
            {
                foreach (var line in Environment.Split('\r', '\n'))
                {
                    int equals = line.IndexOf('=');
                    if (equals > 0)
                    {
                        psi.EnvironmentVariables[line.Substring(0, equals)] = line.Substring(equals + 1);
                    }
                }
            }

            if (ExecuteInNone.Equals(ExecuteIn, StringComparison.OrdinalIgnoreCase))
            {
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
            }
            else if (ExecuteInConsolePause.Equals(ExecuteIn, StringComparison.OrdinalIgnoreCase))
            {
                psi.Arguments = string.Format(
                    "/C \"\"{0}\" {1}\" & pause",
                    psi.FileName,
                    psi.Arguments
                );
                psi.FileName = PathUtils.GetAbsoluteFilePath(System.Environment.SystemDirectory, "cmd.exe");
            }

            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            using (var process = Process.Start(psi))
            {
                process.OutputDataReceived += Process_OutputDataReceived;
                process.ErrorDataReceived += Process_ErrorDataReceived;
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                return process.ExitCode == 0;
            }
        }

        private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                if (ConsoleToMSBuild)
                {
                    _consoleOutput.Value.Add(e.Data);
                }
                else
                {
                    BuildEngine.LogMessageEvent(new BuildMessageEventArgs(
                        e.Data,
                        "",
                        "PythonCommand",
                        MessageImportance.Normal
                    ));
                }
            }
        }

        private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                if (ConsoleToMSBuild)
                {
                    _consoleError.Value.Add(e.Data);
                }
                else
                {
                    BuildEngine.LogMessageEvent(new BuildMessageEventArgs(
                        e.Data,
                        "",
                        "PythonCommand",
                        MessageImportance.High
                    ));
                }
            }
        }
    }

    /// <summary>
    /// Constructs CreatePythonCommandItem task objects.
    /// </summary>
    public sealed class CreatePythonCommandItemFactory : TaskFactory<CreatePythonCommandItem>
    {
        public override ITask CreateTask(IBuildEngine taskFactoryLoggingHost)
        {
            return new CreatePythonCommandItem(Properties["ProjectPath"], taskFactoryLoggingHost);
        }
    }

    /// <summary>
    /// Constructs RunPythonCommand task objects.
    /// </summary>
    public sealed class RunPythonCommandFactory : TaskFactory<RunPythonCommand>
    {
        public override ITask CreateTask(IBuildEngine taskFactoryLoggingHost)
        {
            return new RunPythonCommand(Properties["ProjectPath"], taskFactoryLoggingHost);
        }
    }
}
