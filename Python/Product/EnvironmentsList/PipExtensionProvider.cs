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
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.PythonTools.EnvironmentsList.Properties;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.EnvironmentsList {
    public sealed class PipExtensionProvider : IEnvironmentViewExtension, IPackageManagerUI, IDisposable {
        private readonly IPythonInterpreterFactory _factory;
        private readonly IPackageManager _packageManager;
        private readonly Uri _index;
        private readonly string _indexName;
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
            _packageManager = _factory.PackageManager;
            if (_packageManager == null) {
                throw new NotSupportedException();
            }

            if (!string.IsNullOrEmpty(index) && Uri.TryCreate(index, UriKind.Absolute, out _index)) {
                _indexName = string.IsNullOrEmpty(indexName) ? _index.Host : indexName;
            }
            _cache = PipPackageCache.GetCache(_index, _indexName);
        }

        public void Dispose() {
            _cancelAll.Cancel();
            _cancelAll.Dispose();
            _pipLock.Dispose();
            var pipCancel = _pipCancel;
            if (pipCancel != null) {
                pipCancel.Dispose();
            }
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
                    UpdateStarted?.Invoke(this, EventArgs.Empty);
                }
                result = new CallOnDispose(() => {
                    try {
                        if (Interlocked.Decrement(ref _pipLockWaitCount) == 0) {
                            UpdateComplete?.Invoke(this, EventArgs.Empty);
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

            if (!CanExecute) {
                // Invalid configuration, so assume no packages
                return null;
            }

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
                if (!CanExecute) {
                    return false;
                }
                return _isPipInstalled;
            }
            private set {
                if (_isPipInstalled != value) {
                    _isPipInstalled = value;

                    IsPipInstalledChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        internal event EventHandler IsPipInstalledChanged;

        internal async Task CheckPipInstalledAsync() {
            if (!CanExecute) {
                // Don't cache the result in case our configuration gets fixed
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

        public event EventHandler<QueryShouldElevateEventArgs> QueryShouldElevate;

        public bool ShouldElevate() {
            var e = new QueryShouldElevateEventArgs(_factory.Configuration.PrefixPath);
            QueryShouldElevate?.Invoke(this, e);
            if (e.Cancel) {
                throw new OperationCanceledException();
            }
            return e.Elevate;
        }

        public bool CanExecute => _packageManager != null;

        private void AbortOnInvalidConfiguration() {
            if (_packageManager == null) {
                throw new InvalidOperationException(Resources.MisconfiguredEnvironment);
            }
        }

        public async Task InstallPip() {
            AbortOnInvalidConfiguration();
            await _packageManager.PrepareAsync(this, _cancelAll.Token);
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
                        PackageManagerUIRedirector.Get(this),
                        quoteArgs: false,
                        elevate: ShouldElevate()
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
                        PackageManagerUIRedirector.Get(this),
                        quoteArgs: false,
                        elevate: ShouldElevate()
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

        public event EventHandler<OutputEventArgs> OutputTextReceived;

        public void OnOutputTextReceived(string text) {
            OutputTextReceived?.Invoke(this, new OutputEventArgs(text));
        }

        public event EventHandler<OutputEventArgs> ErrorTextReceived;

        public void OnErrorTextReceived(string text) {
            ErrorTextReceived?.Invoke(this, new OutputEventArgs(text));
        }

        public event EventHandler<OutputEventArgs> OperationStarted;

        public void OnOperationStarted(string operation) {
            OperationStarted?.Invoke(this, new OutputEventArgs(operation));
        }

        public event EventHandler<OutputEventArgs> OperationFinished;

        public void OnOperationFinished(string operation) {
            OperationFinished?.Invoke(this, new OutputEventArgs(operation));
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

    public sealed class QueryShouldElevateEventArgs : EventArgs {
        public bool Cancel { get; set; }
        public bool Elevate { get; set; }
        public string TargetDirectory { get; }

        public QueryShouldElevateEventArgs(string targetDirectory) {
            TargetDirectory = targetDirectory;
        }
    }

    public sealed class OutputEventArgs : EventArgs {
        public string Data { get; }

        public OutputEventArgs(string data) {
            Data = data;
        }
    }
}
