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
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.PythonTools.EnvironmentsList.Properties;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.EnvironmentsList {
    public sealed class PipExtensionProvider : IEnvironmentViewExtension {
        private readonly IPythonInterpreterFactory _factory;
        private readonly Uri _index;
        private readonly string _indexName;
        private readonly Redirector _output;
        private FrameworkElement _wpfObject;

        private bool? _isPipInstalled;
        private PipPackageCache _cache;

        private readonly CancellationTokenSource _cancelAll = new CancellationTokenSource();

        private static readonly KeyValuePair<string, string>[] UnbufferedEnv = new[] { 
            new KeyValuePair<string, string>("PYTHONUNBUFFERED", "1")
        };

        private static readonly Version SupportsDashMPip = new Version(2, 7);

        private int _pipLockWaitCount;
        private readonly SemaphoreSlim _pipLock = new SemaphoreSlim(1);
        private CancellationTokenSource _pipCancel = new CancellationTokenSource();

        /// <summary>
        /// Creates a provider for managing packages through pip.
        /// </summary>
        /// <param name="factory">The associated interpreter.</param>
        /// <param name="index">
        /// The index URL. Defaults to https://pypi.python.org/pypi/
        /// </param>
        /// <param name="indexName">
        /// Display name of the index. Defaults to PyPI.
        /// </param>
        public PipExtensionProvider(
            IPythonInterpreterFactory factory,
            string index = null,
            string indexName = null
        ) {
            _factory = factory;
            _output = new PipRedirector(this);
            if (!string.IsNullOrEmpty(index) && Uri.TryCreate(index, UriKind.Absolute, out _index)) {
                _indexName = string.IsNullOrEmpty(indexName) ? _index.Host : indexName;
            }
            _cache = PipPackageCache.GetCache(_index, _indexName);
        }

        public int SortPriority {
            get { return -8; }
        }

        public string LocalizedDisplayName {
            get { return _indexName ?? Resources.PipExtensionDisplayName; }
        }

        public string IndexName {
            get { return _indexName ?? Resources.PipDefaultIndexName; }
        }

        public object HelpContent {
            get { return Resources.PipExtensionHelpContent; }
        }

        public FrameworkElement WpfObject {
            get {
                if (_wpfObject == null) {
                    _wpfObject = new PipExtension(this);
                }
                return _wpfObject;
            }
        }

        #region Pip Locking

        public bool IsWorking {
            get { return Volatile.Read(ref _pipLockWaitCount) == 0; }
        }

        private async Task<IDisposable> WaitAndLockPip() {
            int newValue = Interlocked.Increment(ref _pipLockWaitCount);

            await _pipLock.WaitAsync(_pipCancel.Token);

            IDisposable result = null;
            try {
                if (newValue == 1) {
                    var evt = UpdateStarted;
                    if (evt != null) {
                        evt(this, EventArgs.Empty);
                    }
                }
                result = new CallOnDispose(() => {
                    try {
                        if (Interlocked.Decrement(ref _pipLockWaitCount) == 0) {
                            var evt = UpdateComplete;
                            if (evt != null) {
                                evt(this, EventArgs.Empty);
                            }
                        }
                    } finally {
                        _pipLock.Release();
                    }
                });
            } finally {
                if (result == null) {
                    _pipLock.Release();
                }
            }
            return result;
        }

        public event EventHandler UpdateStarted;
        public event EventHandler UpdateComplete;

        #endregion

        internal async Task<IList<PipPackageView>> GetInstalledPackagesAsync() {
            string[] args;

            if (_factory.Configuration.Version < SupportsDashMPip) {
                args = new [] { "-c", "import pip; pip.main()", "list", "--no-index" };
            } else {
                args = new [] { "-m", "pip", "list", "--no-index" };
            }

            PipPackageView[] packages = null;

            try {
                using (var output = ProcessOutput.RunHiddenAndCapture(_factory.Configuration.InterpreterPath, args)) {
                    if ((await output) != 0) {
                        throw new PipException(Resources.ListFailed);
                    }

                    packages = output.StandardOutputLines
                        .Select(s => new PipPackageView(_cache, s))
                        .ToArray();
                }
            } catch (IOException) {
            } finally {
                if (packages == null) {
                    // pip is obviously not installed
                    IsPipInstalled = false;
                } else {
                    // pip is obviously installed
                    IsPipInstalled = true;
                }
            }

            return packages;
        }

        internal async Task<IList<PipPackageView>> GetAvailablePackagesAsync() {
            return await _cache.GetAllPackagesAsync(_cancelAll.Token);
        }

        internal bool? IsPipInstalled {
            get {
                return _isPipInstalled;
            }
            private set {
                if (_isPipInstalled != value) {
                    _isPipInstalled = value;

                    var evt = IsPipInstalledChanged;
                    if (evt != null) {
                        evt(this, EventArgs.Empty);
                    }
                }
            }
        }

        internal event EventHandler IsPipInstalledChanged;

        internal async Task CheckPipInstalledAsync() {
            if (!CanExecute) {
                // Don't cache the result in case our configuration gets fixed
                IsPipInstalled = false;
                return;
            }

            if (!_isPipInstalled.HasValue) {
                try {
                    using (var output = ProcessOutput.RunHiddenAndCapture(
                        _factory.Configuration.InterpreterPath,
                        "-E", "-c", "import pip"
                    )) {
                        IsPipInstalled = (await output) == 0;
                    }
                } catch (IOException) {
                    IsPipInstalled = false;
                } catch (OperationCanceledException) {
                    IsPipInstalled = false;
                }
            }
        }

        public event EventHandler<ValueEventArgs<bool>> GetElevateSetting;

        private bool ShouldElevate {
            get {
                var evt = GetElevateSetting;
                if (evt != null) {
                    var e = new ValueEventArgs<bool>();
                    evt(this, e);
                    return e.Value;
                }
                return false;
            }
        }

        public bool CanExecute {
            get {
                if (_factory == null || _factory.Configuration == null ||
                    string.IsNullOrEmpty(_factory.Configuration.InterpreterPath)) {
                    return false;
                }

                return true;
            }
        }

        private void AbortOnInvalidConfiguration() {
            if (_factory == null || _factory.Configuration == null ||
                string.IsNullOrEmpty(_factory.Configuration.InterpreterPath)) {
                throw new InvalidOperationException(Resources.MisconfiguredEnvironment);
            }
        }

        public async Task InstallPip() {
            AbortOnInvalidConfiguration();
            
            using (await WaitAndLockPip()) {
                OnOperationStarted(Resources.InstallingPipStarted);
                using (var output = ProcessOutput.Run(
                    _factory.Configuration.InterpreterPath,
                    new[] { PythonToolsInstallPath.GetFile("pip_downloader.py") },
                    _factory.Configuration.PrefixPath,
                    UnbufferedEnv,
                    false,
                    _output,
                    elevate: ShouldElevate
                )) {
                    bool success = true;
                    try {
                        var exitCode = await output;
                        if (exitCode != 0) {
                            success = false;
                            throw new PipException(Resources.InstallationFailed);
                        }
                    } catch (OperationCanceledException) {
                        success = false;
                    } catch (Exception ex) {
                        success = false;
                        if (ex.IsCriticalException()) {
                            throw;
                        }
                        ToolWindow.UnhandledException.Execute(ExceptionDispatchInfo.Capture(ex), WpfObject);
                    } finally {
                        if (success) {
                            IsPipInstalled = true;
                        }

                        OnOperationFinished(
                            success ? Resources.InstallingPipSuccess : Resources.InstallingPipFailed
                        );
                    }
                }
            }
        }

        private static IEnumerable<string> QuotedArgumentsWithPackageName(IEnumerable<string> args, string package) {
            var quotedArgs = string.Join(" ", args.Select(a => ProcessOutput.QuoteSingleArgument(a))) + " ";

            if (Directory.Exists(package) || File.Exists(package)) {
                yield return quotedArgs + ProcessOutput.QuoteSingleArgument(package);
            } else {
                yield return quotedArgs + package;
            }
        }

        public async Task InstallPackage(string package, bool upgrade) {
            List<string> args;

            if (_factory.Configuration.Version < SupportsDashMPip) {
                args = new List<string> { "-c", "import pip; pip.main()", "install" };
            } else {
                args = new List<string> { "-m", "pip", "install" };
            }

            if (upgrade) {
                args.Add("-U");
            }

            if (_index != null) {
                args.Add("--index-url");
                args.Add(_index.AbsoluteUri);
            }

            using (await WaitAndLockPip()) {
                bool success = false;
                OnOperationStarted(string.Format(Resources.InstallingPackageStarted, package));
                try {
                    using (var output = ProcessOutput.Run(
                        _factory.Configuration.InterpreterPath,
                        QuotedArgumentsWithPackageName(args, package),
                        _factory.Configuration.PrefixPath,
                        UnbufferedEnv,
                        false,
                        _output,
                        quoteArgs: false,
                        elevate: ShouldElevate
                    )) {
                        if (!output.IsStarted) {
                            return;
                        }
                        var exitCode = await output;
                        if (exitCode != 0) {
                            throw new PipException(Resources.InstallationFailed);
                        }
                        success = true;
                    }
                } catch (IOException) {
                } finally {
                    if (!success) {
                        // Check whether we failed because pip is missing
                        CheckPipInstalledAsync().DoNotWait();
                    }

                    OnOperationFinished(string.Format(
                        success ? Resources.InstallingPackageSuccess : Resources.InstallingPackageFailed,
                        package
                    ));
                }
            }
        }

        public async Task UninstallPackage(string package) {
            List<string> args;

            if (_factory.Configuration.Version < SupportsDashMPip) {
                args = new List<string> { "-c", "import pip; pip.main()", "uninstall", "-y" };
            } else {
                args = new List<string> { "-m", "pip", "uninstall", "-y" };
            }

            using (await WaitAndLockPip()) {
                OnOperationStarted(string.Format(Resources.UninstallingPackageStarted, package));
                bool success = false;
                try {
                    using (var output = ProcessOutput.Run(
                        _factory.Configuration.InterpreterPath,
                        QuotedArgumentsWithPackageName(args, package),
                        _factory.Configuration.PrefixPath,
                        UnbufferedEnv,
                        false,
                        _output,
                        quoteArgs: false,
                        elevate: ShouldElevate
                    )) {
                        if (!output.IsStarted) {
                            return;
                        }
                        var exitCode = await output;
                        if (exitCode != 0) {
                            // Double check whether the package has actually
                            // been uninstalled, to avoid reporting errors 
                            // where, for all practical purposes, there is no
                            // error.
                            if ((await GetInstalledPackagesAsync()).Any(p => p.Name == package)) {
                                throw new PipException(Resources.UninstallationFailed);
                            }
                        }
                        success = true;
                    }
                } catch (IOException) {
                } finally {
                    if (!success) {
                        // Check whether we failed because pip is missing
                        CheckPipInstalledAsync().DoNotWait();
                    }

                    OnOperationFinished(string.Format(
                        success ? Resources.UninstallingPackageSuccess : Resources.UninstallingPackageFailed,
                        package
                    ));
                }
            }
        }

        public event EventHandler<ValueEventArgs<string>> OutputTextReceived;

        private void OnOutputTextReceived(string text) {
            var evt = OutputTextReceived;
            if (evt != null) {
                evt(this, new ValueEventArgs<string>(text));
            }
        }

        public event EventHandler<ValueEventArgs<string>> ErrorTextReceived;

        private void OnErrorTextReceived(string text) {
            var evt = ErrorTextReceived;
            if (evt != null) {
                evt(this, new ValueEventArgs<string>(text));
            }
        }

        public event EventHandler<ValueEventArgs<string>> OperationStarted;

        private void OnOperationStarted(string operation) {
            var evt = OperationStarted;
            if (evt != null) {
                evt(this, new ValueEventArgs<string>(operation));
            }
        }

        public event EventHandler<ValueEventArgs<string>> OperationFinished;

        private void OnOperationFinished(string operation) {
            var evt = OperationFinished;
            if (evt != null) {
                evt(this, new ValueEventArgs<string>(operation));
            }
        }

        sealed class PipRedirector : Redirector {
            private readonly PipExtensionProvider _provider;

            public PipRedirector(PipExtensionProvider provider) {
                _provider = provider;
            }

            public override void WriteLine(string line) {
                _provider.OnOutputTextReceived(line);
            }

            public override void WriteErrorLine(string line) {
                _provider.OnErrorTextReceived(line);
            }
        }

        sealed class CallOnDispose : IDisposable {
            private readonly Action _action;
            public CallOnDispose(Action action) { _action = action; }
            public void Dispose() { _action(); }
        }
    }

    [Serializable]
    public class PipException : Exception {
        public PipException() { }
        public PipException(string message) : base(message) { }
        public PipException(string message, Exception inner) : base(message, inner) { }
        protected PipException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
    }

    public class ValueEventArgs<T> : EventArgs {
        public T Value { get; set; }

        public ValueEventArgs() {
            Value = default(T);
        }

        public ValueEventArgs(T value) {
            Value = value;
        }
    }
}
