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

        private PipPackageCache _cache;

        private readonly CancellationTokenSource _cancelAll = new CancellationTokenSource();

        private static readonly KeyValuePair<string, string>[] UnbufferedEnv = new[] { 
            new KeyValuePair<string, string>("PYTHONUNBUFFERED", "1")
        };

        private static readonly Version SupportsDashMPip = new Version(2, 7);

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

        internal async Task<IList<PipPackageView>> GetInstalledPackagesAsync() {
            if (_packageManager == null) {
                return Array.Empty<PipPackageView>();
            }

            return (await _packageManager.GetInstalledPackagesAsync(_cancelAll.Token))
                .Where(p => p.IsValid)
                .Select(p => new PipPackageView(_packageManager, p, true))
                .ToArray();
        }

        internal async Task<IList<PipPackageView>> GetAvailablePackagesAsync() {
            if (_packageManager == null) {
                return Array.Empty<PipPackageView>();
            }

            return (await _cache.GetAllPackagesAsync(_cancelAll.Token))
                .Where(p => p.IsValid)
                .Select(p => new PipPackageView(_packageManager, p, false))
                .ToArray();
        }

        internal bool? IsPipInstalled => _packageManager?.IsReady;

        internal event EventHandler IsPipInstalledChanged {
            add {
                if (_packageManager != null) {
                    _packageManager.IsReadyChanged += value;
                }
            }
            remove {
                if (_packageManager != null) {
                    _packageManager.IsReadyChanged -= value;
                }
            }
        }

        public event EventHandler<QueryShouldElevateEventArgs> QueryShouldElevate;

        public bool ShouldElevate() {
            var e = new QueryShouldElevateEventArgs(_factory.Configuration);
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


        public async Task InstallPackage(PackageSpec package) {
            AbortOnInvalidConfiguration();
            await _packageManager.InstallAsync(package, this, _cancelAll.Token);
        }

        public async Task UninstallPackage(PackageSpec package) {
            AbortOnInvalidConfiguration();
            await _packageManager.UninstallAsync(package, this, _cancelAll.Token);
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
        public InterpreterConfiguration Configuration { get; }

        public QueryShouldElevateEventArgs(InterpreterConfiguration configuration) {
            Configuration = configuration;
        }
    }

    public sealed class OutputEventArgs : EventArgs {
        public string Data { get; }

        public OutputEventArgs(string data) {
            Data = data;
        }
    }
}
