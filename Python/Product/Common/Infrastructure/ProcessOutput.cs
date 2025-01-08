// Visual Studio Shared Project
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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Infrastructure {
    /// <summary>
    /// Base class that can receive output from <see cref="ProcessOutput"/>.
    /// 
    /// If this class implements <see cref="IDisposable"/>, it will be disposed
    /// when the <see cref="ProcessOutput"/> object is disposed.
    /// </summary>
    abstract class Redirector {
        /// <summary>
        /// Called when a line is written to standard output.
        /// </summary>
        /// <param name="line">The line of text, not including the newline. This
        /// is never null.</param>
        public abstract void WriteLine(string line);
        /// <summary>
        /// Called when a line is written to standard error.
        /// </summary>
        /// <param name="line">The line of text, not including the newline. This
        /// is never null.</param>
        public abstract void WriteErrorLine(string line);

        /// <summary>
        /// Called when output is written that should be brought to the user's
        /// attention. The default implementation does nothing.
        /// </summary>
        public virtual void Show() {
        }

        /// <summary>
        /// Called when output is written that should be brought to the user's
        /// immediate attention. The default implementation does nothing.
        /// </summary>
        public virtual void ShowAndActivate() {
        }
    }

    sealed class TeeRedirector : Redirector, IDisposable {
        private readonly Redirector[] _redirectors;

        public TeeRedirector(params Redirector[] redirectors) {
            _redirectors = redirectors;
        }

        public void Dispose() {
            foreach (var redir in _redirectors.OfType<IDisposable>()) {
                redir.Dispose();
            }
        }

        public override void WriteLine(string line) {
            foreach (var redir in _redirectors) {
                redir.WriteLine(line);
            }
        }

        public override void WriteErrorLine(string line) {
            foreach (var redir in _redirectors) {
                redir.WriteErrorLine(line);
            }
        }

        public override void Show() {
            foreach (var redir in _redirectors) {
                redir.Show();
            }
        }

        public override void ShowAndActivate() {
            foreach (var redir in _redirectors) {
                redir.ShowAndActivate();
            }
        }
    }

    sealed class ListRedirector : Redirector {
        private readonly List<string> _output, _error;

        public ListRedirector(List<string> output, List<string> error = null) {
            _output = output;
            _error = error ?? output;
        }

        public override void WriteErrorLine(string line) {
            _error.Add(line);
        }

        public override void WriteLine(string line) {
            _output.Add(line);
        }
    }

    sealed class StreamRedirector : Redirector {
        private readonly StreamWriter _output, _error;
        private readonly string _outputPrefix, _errorPrefix;

        public StreamRedirector(
            StreamWriter output,
            StreamWriter error = null,
            string outputPrefix = null,
            string errorPrefix = null
        ) {
            _output = output;
            _error = error ?? output;
            _outputPrefix = outputPrefix;
            _errorPrefix = errorPrefix;
        }

        private static string WithPrefix(string prefix, string line) {
            if (string.IsNullOrEmpty(prefix)) {
                return line;
            }
            return prefix + line;
        }

        public override void WriteErrorLine(string line) {
            _error.WriteLine(WithPrefix(_errorPrefix, line));
            _error.Flush();
        }

        public override void WriteLine(string line) {
            _output.WriteLine(WithPrefix(_outputPrefix, line));
            _output.Flush();
        }
    }

    /// <summary>
    /// Represents a process and its captured output.
    /// </summary>
    sealed class ProcessOutput : IDisposable {
        private readonly Process _process;
        private readonly string _arguments;
        private readonly List<string> _output, _error;
        private ManualResetEvent _waitHandleEvent;
        private readonly Redirector _redirector;
        private bool _isDisposed;
        private readonly object _seenNullLock = new object();
        private bool _seenNullInOutput, _seenNullInError;
        private bool _haveRaisedExitedEvent;
        private Task<int> _awaiter;

        private static readonly char[] EolChars = new[] { '\r', '\n' };
        private static readonly char[] _needToBeQuoted = new[] { ' ', '"' };

        /// <summary>
        /// Runs the provided executable file and allows the program to display
        /// output to the user.
        /// </summary>
        /// <param name="filename">Executable file to run.</param>
        /// <param name="arguments">Arguments to pass.</param>
        /// <returns>A <see cref="ProcessOutput"/> object.</returns>
        public static ProcessOutput RunVisible(string filename, params string[] arguments) {
            return Run(filename, arguments, null, null, true, null);
        }

        /// <summary>
        /// Runs the provided executable file hidden and captures any output
        /// messages.
        /// </summary>
        /// <param name="filename">Executable file to run.</param>
        /// <param name="arguments">Arguments to pass.</param>
        /// <returns>A <see cref="ProcessOutput"/> object.</returns>
        public static ProcessOutput RunHiddenAndCapture(string filename, params string[] arguments) {
            return Run(filename, arguments, null, null, false, null);
        }

        public static ProcessOutput RunHiddenAndCapture(string filename, Encoding encoding, params string[] arguments) {
            return Run(filename, arguments, null, null, false, null, true, false, encoding, encoding);
        }

        /// <summary>
        /// Runs the file with the provided settings.
        /// </summary>
        /// <param name="filename">Executable file to run.</param>
        /// <param name="arguments">Arguments to pass.</param>
        /// <param name="workingDirectory">Starting directory.</param>
        /// <param name="env">Environment variables to set.</param>
        /// <param name="visible">
        /// False to hide the window and redirect output to
        /// <see cref="StandardOutputLines"/> and
        /// <see cref="StandardErrorLines"/>.
        /// </param>
        /// <param name="redirector">
        /// An object to receive redirected output.
        /// </param>
        /// <param name="quoteArgs">
        /// True to ensure each argument is correctly quoted.
        /// </param>
        /// <param name="elevate">
        /// True to run the process as an administrator. See
        /// <see cref="RunElevated"/>.
        /// </param>
        /// <returns>A <see cref="ProcessOutput"/> object.</returns>
        public static ProcessOutput Run(
            string filename,
            IEnumerable<string> arguments,
            string workingDirectory,
            IEnumerable<KeyValuePair<string, string>> env,
            bool visible,
            Redirector redirector,
            bool quoteArgs = true,
            bool elevate = false,
            Encoding outputEncoding = null,
            Encoding errorEncoding = null
        ) {
            if (string.IsNullOrEmpty(filename)) {
                throw new ArgumentException("Filename required", "filename");
            }
            if (elevate) {
                return RunElevated(
                    filename,
                    arguments,
                    workingDirectory,
                    env,
                    redirector,
                    quoteArgs,
                    elevate,
                    outputEncoding,
                    errorEncoding
                );
            }

            var psi = new ProcessStartInfo(filename); // CodeQL [SM00406] Code ql is complaining, but this is a utility class that is called with many different file names.
            if (quoteArgs) {
                psi.Arguments = string.Join(" ",
                    arguments.Where(a => a != null).Select(QuoteSingleArgument));
            } else {
                psi.Arguments = string.Join(" ", arguments.Where(a => a != null)); // CodeQL [SM00406] Code ql is complaining, but this is a utility class that is called with many different file names.
            }
            psi.WorkingDirectory = workingDirectory;
            psi.CreateNoWindow = !visible;
            psi.UseShellExecute = false;
            psi.RedirectStandardError = !visible || (redirector != null);
            psi.RedirectStandardOutput = !visible || (redirector != null);
            psi.RedirectStandardInput = !visible;
            psi.StandardOutputEncoding = outputEncoding ?? psi.StandardOutputEncoding;
            psi.StandardErrorEncoding = errorEncoding ?? outputEncoding ?? psi.StandardErrorEncoding;
            if (env != null) {
                foreach (var kv in env) {
                    psi.EnvironmentVariables[kv.Key] = kv.Value;
                }
            }

            var process = new Process { StartInfo = psi };
            return new ProcessOutput(process, redirector);
        }

        /// <summary>
        /// Runs the file with the provided settings as a user with
        /// administrative permissions. The window is always hidden and output
        /// is provided to the redirector when the process terminates.
        /// </summary>
        /// <param name="filename">Executable file to run.</param>
        /// <param name="arguments">Arguments to pass.</param>
        /// <param name="workingDirectory">Starting directory.</param>
        /// <param name="redirector">
        /// An object to receive redirected output.
        /// </param>
        /// <param name="quoteArgs"></param>
        /// <returns>A <see cref="ProcessOutput"/> object.</returns>
        public static ProcessOutput RunElevated(
            string filename,
            IEnumerable<string> arguments,
            string workingDirectory,
            IEnumerable<KeyValuePair<string, string>> env,
            Redirector redirector,
            bool quoteArgs = true,
            bool elevate = true,
            Encoding outputEncoding = null,
            Encoding errorEncoding = null
        ) {
            var psi = new ProcessStartInfo(PythonToolsInstallPath.GetFile("Microsoft.PythonTools.RunElevated.exe", typeof(ProcessOutput).Assembly));
            psi.CreateNoWindow = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;

            var utf8 = new UTF8Encoding(false);
            // Send args and env as base64 to avoid newline issues
            string args;
            if (quoteArgs) {
                args = string.Join("|", arguments
                    .Where(a => a != null)
                    .Select(a => Convert.ToBase64String(utf8.GetBytes(QuoteSingleArgument(a))))
                );
            } else {
                args = string.Join("|", arguments
                    .Where(a => a != null)
                    .Select(a => Convert.ToBase64String(utf8.GetBytes(a)))
                );
            }

            var fullEnv = env != null ?
                string.Join("|", env.Select(kv => kv.Key + "=" + Convert.ToBase64String(utf8.GetBytes(kv.Value)))) :
                "";

            TcpListener listener = null;
            Task<TcpClient> clientTask = null;

            try {
                listener = SocketUtils.GetRandomPortListener(IPAddress.Loopback, out int port);
                psi.Arguments = port.ToString();
                clientTask = listener.AcceptTcpClientAsync();
            } catch (Exception ex) {
                listener?.Stop();
                throw new InvalidOperationException(Strings.UnableToElevate, ex);
            }

            var process = new Process();

            clientTask.ContinueWith(t => {
                listener.Stop();
                TcpClient client;
                try {
                    client = t.Result;
                } catch (AggregateException ae) {
                    try {
                        process.Kill();
                    } catch (InvalidOperationException) {
                    } catch (Win32Exception) {
                    }

                    if (redirector != null) {
                        foreach (var ex in ae.InnerExceptions.DefaultIfEmpty(ae)) {
                            using (var reader = new StringReader(ex.ToString())) {
                                for (var line = reader.ReadLine(); line != null; line = reader.ReadLine()) {
                                    redirector.WriteErrorLine(line);
                                }
                            }
                        }
                    }
                    return;
                }
                using (var writer = new StreamWriter(client.GetStream(), utf8, 4096, true)) {
                    writer.WriteLine(filename);
                    writer.WriteLine(args);
                    writer.WriteLine(workingDirectory);
                    writer.WriteLine(fullEnv);
                    writer.WriteLine(outputEncoding?.WebName ?? "");
                    writer.WriteLine(errorEncoding?.WebName ?? "");
                }

                if (redirector != null) {
                    Task.Run(() => {
                        using (var reader = new StreamReader(client.GetStream(), utf8, false, 4096, true)) {
                            try {
                                string line;
                                while ((line = reader.ReadLine()) != null) { 
                                
                                    if (line.StartsWithOrdinal("OUT:")) {
                                        redirector.WriteLine(line.Substring(4));
                                    } else if (line.StartsWithOrdinal("ERR:")) {
                                        redirector.WriteErrorLine(line.Substring(4));
                                    } else {
                                        redirector.WriteLine(line);
                                    }
                                }
                            } catch (IOException) {
                            } catch (ObjectDisposedException) {
                            }
                        }
                    });
                }
            });

            process.StartInfo = psi;

            return new ProcessOutput(process, redirector);
        }

        internal static IEnumerable<string> SplitLines(string source) {
            int start = 0;
            int end = source.IndexOfAny(EolChars);
            while (end >= start) {
                yield return source.Substring(start, end - start);
                start = end + 1;
                if (source[start - 1] == '\r' && start < source.Length && source[start] == '\n') {
                    start += 1;
                }

                if (start < source.Length) {
                    end = source.IndexOfAny(EolChars, start);
                } else {
                    end = -1;
                }
            }
            if (start <= 0) {
                yield return source;
            } else if (start < source.Length) {
                yield return source.Substring(start);
            }
        }

        public static string QuoteSingleArgument(string arg) {
            if (string.IsNullOrEmpty(arg)) {
                return "\"\"";
            }
            if (arg.IndexOfAny(_needToBeQuoted) < 0) {
                return arg;
            }

            if (arg.StartsWithOrdinal("\"") && arg.EndsWithOrdinal("\"")) {
                bool inQuote = false;
                int consecutiveBackslashes = 0;
                foreach (var c in arg) {
                    if (c == '"') {
                        if (consecutiveBackslashes % 2 == 0) {
                            inQuote = !inQuote;
                        }
                    }

                    if (c == '\\') {
                        consecutiveBackslashes += 1;
                    } else {
                        consecutiveBackslashes = 0;
                    }
                }
                if (!inQuote) {
                    return arg;
                }
            }

            var newArg = arg.Replace("\"", "\\\"");
            if (newArg.EndsWithOrdinal("\\")) {
                newArg += "\\";
            }
            return "\"" + newArg + "\"";
        }

        private ProcessOutput(Process process, Redirector redirector) {
            _arguments = QuoteSingleArgument(process.StartInfo.FileName) + " " + process.StartInfo.Arguments;
            _redirector = redirector;
            if (_redirector == null) {
                _output = new List<string>();
                _error = new List<string>();
                _redirector = new ListRedirector(_output, _error);
            }

            _process = process;
            if (_process.StartInfo.RedirectStandardOutput) {
                _process.OutputDataReceived += OnOutputDataReceived;
            }
            if (_process.StartInfo.RedirectStandardError) {
                _process.ErrorDataReceived += OnErrorDataReceived;
            }

            if (!_process.StartInfo.RedirectStandardOutput && !_process.StartInfo.RedirectStandardError) {
                // If we are receiving output events, we signal that the process
                // has exited when one of them receives null. Otherwise, we have
                // to listen for the Exited event.
                // If we just listen for the Exited event, we may receive it
                // before all the output has arrived.
                _process.Exited += OnExited;
            }
            _process.EnableRaisingEvents = true;

            try {
                _process.Start();
            } catch (Win32Exception ex) {
                _redirector.WriteErrorLine(ex.Message);
                _process = null;
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                foreach (var line in SplitLines(ex.ToString())) {
                    _redirector.WriteErrorLine(line);
                }
                _process = null;
            }

            if (_process != null) {
                if (_process.StartInfo.RedirectStandardOutput) {
                    _process.BeginOutputReadLine();
                }
                if (_process.StartInfo.RedirectStandardError) {
                    _process.BeginErrorReadLine();
                }

                if (_process.StartInfo.RedirectStandardInput) {
                    // Close standard input so that we don't get stuck trying to read input from the user.
                    try {
                        _process.StandardInput.Close();
                    } catch (InvalidOperationException) {
                        // StandardInput not available
                    }
                }
            }
        }

        private void OnOutputDataReceived(object sender, DataReceivedEventArgs e) {
            if (_isDisposed) {
                return;
            }

            if (e.Data == null) {
                bool shouldExit;
                lock (_seenNullLock) {
                    _seenNullInOutput = true;
                    shouldExit = _seenNullInError || !_process.StartInfo.RedirectStandardError;
                }
                if (shouldExit) {
                    OnExited(_process, EventArgs.Empty);
                }
            } else if (!string.IsNullOrEmpty(e.Data)) {
                foreach (var line in SplitLines(e.Data)) {
                    if (_redirector != null) {
                        _redirector.WriteLine(line);
                    }
                }
            }
        }

        private void OnErrorDataReceived(object sender, DataReceivedEventArgs e) {
            if (_isDisposed) {
                return;
            }

            if (e.Data == null) {
                bool shouldExit;
                lock (_seenNullLock) {
                    _seenNullInError = true;
                    shouldExit = _seenNullInOutput || !_process.StartInfo.RedirectStandardOutput;
                }
                if (shouldExit) {
                    OnExited(_process, EventArgs.Empty);
                }
            } else if (!string.IsNullOrEmpty(e.Data)) {
                foreach (var line in SplitLines(e.Data)) {
                    if (_redirector != null) {
                        _redirector.WriteErrorLine(line);
                    }
                }
            }
        }

        public int? ProcessId {
            get {
                return _process != null ? _process.Id : (int?)null;
            }
        }

        /// <summary>
        /// The arguments that were originally passed, including the filename.
        /// </summary>
        public string Arguments {
            get {
                return _arguments;
            }
        }

        /// <summary>
        /// True if the process started. False if an error occurred.
        /// </summary>
        public bool IsStarted {
            get {
                return _process != null;
            }
        }

        /// <summary>
        /// The exit code or null if the process never started or has not
        /// exited.
        /// </summary>
        public int? ExitCode {
            get {
                if (_process == null || !_process.HasExited) {
                    return null;
                }
                return _process.ExitCode;
            }
        }

        /// <summary>
        /// Gets or sets the priority class of the process.
        /// </summary>
        public ProcessPriorityClass PriorityClass {
            get {
                if (_process != null && !_process.HasExited) {
                    try {
                        return _process.PriorityClass;
                    } catch (Win32Exception) {
                    } catch (InvalidOperationException) {
                        // Return Normal if we've raced with the process
                        // exiting.
                    }
                }
                return ProcessPriorityClass.Normal;
            }
            set {
                if (_process != null && !_process.HasExited) {
                    try {
                        _process.PriorityClass = value;
                    } catch (Win32Exception) {
                    } catch (InvalidOperationException) {
                        // Silently fail if we've raced with the process
                        // exiting.
                    }
                }
            }
        }

        /// <summary>
        /// The redirector that was originally passed.
        /// </summary>
        public Redirector Redirector {
            get { return _redirector; }
        }

        private void FlushAndCloseOutput() {
            if (_process == null) {
                return;
            }

            if (_process.StartInfo.RedirectStandardOutput) {
                try {
                    _process.CancelOutputRead();
                } catch (InvalidOperationException) {
                    // Reader has already been cancelled
                }
            }
            if (_process.StartInfo.RedirectStandardError) {
                try {
                    _process.CancelErrorRead();
                } catch (InvalidOperationException) {
                    // Reader has already been cancelled
                }
            }

            if (_waitHandleEvent != null) {
                try {
                    _waitHandleEvent.Set();
                } catch (ObjectDisposedException) {
                }
            }
        }

        /// <summary>
        /// The lines of text sent to standard output. These do not include
        /// newline characters.
        /// </summary>
        public IEnumerable<string> StandardOutputLines {
            get {
                return _output;
            }
        }

        /// <summary>
        /// The lines of text sent to standard error. These do not include
        /// newline characters.
        /// </summary>
        public IEnumerable<string> StandardErrorLines {
            get {
                return _error;
            }
        }

        /// <summary>
        /// A handle that can be waited on. It triggers when the process exits.
        /// </summary>
        public WaitHandle WaitHandle {
            get {
                if (_process == null) {
                    return null;
                }
                if (_waitHandleEvent == null) {
                    _waitHandleEvent = new ManualResetEvent(_haveRaisedExitedEvent);
                }
                return _waitHandleEvent;
            }
        }

        /// <summary>
        /// Waits until the process exits.
        /// </summary>
        public void Wait() {
            if (_process != null) {
                _process.WaitForExit();
                // Should have already been called, in which case this is a no-op
                OnExited(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Waits until the process exits or the timeout expires.
        /// </summary>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <returns>
        /// True if the process exited before the timeout expired.
        /// </returns>
        public bool Wait(TimeSpan timeout) {
            if (_process != null) {
                bool exited = _process.WaitForExit((int)timeout.TotalMilliseconds);
                if (exited) {
                    // Should have already been called, in which case this is a no-op
                    OnExited(this, EventArgs.Empty);
                }
                return exited;
            }
            return true;
        }

        /// <summary>
        /// Enables using 'await' on this object.
        /// </summary>
        public TaskAwaiter<int> GetAwaiter() {
            if (_awaiter == null) {
                if (_process == null) {
                    var tcs = new TaskCompletionSource<int>();
                    tcs.SetCanceled();
                    _awaiter = tcs.Task;
                } else if (_process.HasExited) {
                    // Should have already been called, in which case this is a no-op
                    OnExited(this, EventArgs.Empty);
                    var tcs = new TaskCompletionSource<int>();
                    tcs.SetResult(_process.ExitCode);
                    _awaiter = tcs.Task;
                } else {
                    _awaiter = Task.Run(() => {
                        try {
                            Wait();
                        } catch (Win32Exception) {
                            throw new OperationCanceledException();
                        }
                        return _process.ExitCode;
                    });
                }
            }

            return _awaiter.GetAwaiter();
        }

        /// <summary>
        /// Immediately stops the process.
        /// </summary>
        public void Kill() {
            if (_process != null && !_process.HasExited) {
                _process.Kill();
                // Should have already been called, in which case this is a no-op
                OnExited(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Raised when the process exits.
        /// </summary>
        public event EventHandler Exited;

        private void OnExited(object sender, EventArgs e) {
            if (_isDisposed || _haveRaisedExitedEvent) {
                return;
            }
            _haveRaisedExitedEvent = true;
            FlushAndCloseOutput();
            var evt = Exited;
            if (evt != null) {
                evt(this, e);
            }
        }

        /// <summary>
        /// Called to dispose of unmanaged Strings.
        /// </summary>
        public void Dispose() {
            if (!_isDisposed) {
                _isDisposed = true;
                if (_process != null) {
                    if (_process.StartInfo.RedirectStandardOutput) {
                        _process.OutputDataReceived -= OnOutputDataReceived;
                    }
                    if (_process.StartInfo.RedirectStandardError) {
                        _process.ErrorDataReceived -= OnErrorDataReceived;
                    }
                    _process.Dispose();
                }
                var disp = _redirector as IDisposable;
                if (disp != null) {
                    disp.Dispose();
                }
                if (_waitHandleEvent != null) {
                    _waitHandleEvent.Set();
                    _waitHandleEvent.Dispose();
                }
            }
        }
    }
}
