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

namespace Microsoft.PythonTools.EnvironmentsList
{
    sealed class PipExtensionProvider : IEnvironmentViewExtension, IPackageManagerUI, IDisposable
    {
        private readonly IPythonInterpreterFactory _factory;
        internal readonly IPackageManager _packageManager;
        private FrameworkElement _wpfObject;

        private readonly CancellationTokenSource _cancelAll = new CancellationTokenSource();

        private static readonly KeyValuePair<string, string>[] UnbufferedEnv = new[] {
            new KeyValuePair<string, string>("PYTHONUNBUFFERED", "1")
        };

        /// <summary>
        /// Creates a provider for managing packages through pip.
        /// </summary>
        public PipExtensionProvider(
            IPythonInterpreterFactory factory,
            IPackageManager packageManager
        )
        {
            _factory = factory;
            _packageManager = packageManager ?? throw new NotSupportedException();
            _packageManager.EnableNotifications();
        }

        public void Dispose()
        {
            _cancelAll.Cancel();
            _cancelAll.Dispose();
        }

        public int SortPriority
        {
            get { return -8; }
        }

        public string LocalizedDisplayName
        {
            get { return _packageManager.ExtensionDisplayName; }
        }

        public string IndexName
        {
            get { return _packageManager.IndexDisplayName; }
        }

        public object HelpContent
        {
            get { return Resources.PipExtensionHelpContent; }
        }

        public string HelpText
        {
            get { return Resources.PipExtensionHelpContent; }
        }

        public FrameworkElement WpfObject
        {
            get
            {
                if (_wpfObject == null)
                {
                    _wpfObject = new PipExtension(this);
                }
                return _wpfObject;
            }
        }

        internal async Task<IList<PipPackageView>> GetInstalledPackagesAsync()
        {
            if (_packageManager == null)
            {
                return Array.Empty<PipPackageView>();
            }

            return (await _packageManager.GetInstalledPackagesAsync(_cancelAll.Token))
                .Where(p => p.IsValid)
                .Select(p => new PipPackageView(_packageManager, p, true))
                .ToArray();
        }

        internal async Task<IList<PipPackageView>> GetAvailablePackagesAsync()
        {
            if (_packageManager == null)
            {
                return Array.Empty<PipPackageView>();
            }

            return (await _packageManager.GetInstallablePackagesAsync(_cancelAll.Token))
                .Where(p => p.IsValid)
                .Select(p => new PipPackageView(_packageManager, p, false))
                .ToArray();
        }

        internal bool? IsPipInstalled => _packageManager?.IsReady;

        internal event EventHandler IsPipInstalledChanged
        {
            add
            {
                if (_packageManager != null)
                {
                    _packageManager.IsReadyChanged += value;
                }
            }
            remove
            {
                if (_packageManager != null)
                {
                    _packageManager.IsReadyChanged -= value;
                }
            }
        }

        public event EventHandler<QueryShouldElevateEventArgs> QueryShouldElevate;

        public Task<bool> ShouldElevateAsync(IPackageManager sender, string operation)
        {
            var e = new QueryShouldElevateEventArgs(_factory);
            QueryShouldElevate?.Invoke(this, e);
            if (e.ElevateAsync != null)
            {
                return e.ElevateAsync;
            }
            if (e.Cancel)
            {
                throw new OperationCanceledException();
            }
            return Task.FromResult(e.Elevate);
        }

        public bool CanExecute => _packageManager != null;

        private void AbortOnInvalidConfiguration()
        {
            if (_packageManager == null)
            {
                throw new InvalidOperationException(Resources.MisconfiguredEnvironment);
            }
        }

        public async Task InstallPip()
        {
            AbortOnInvalidConfiguration();
            await _packageManager.PrepareAsync(this, _cancelAll.Token);
        }


        public async Task InstallPackage(PackageSpec package)
        {
            AbortOnInvalidConfiguration();
            await _packageManager.InstallAsync(package, this, _cancelAll.Token);
        }

        public async Task UninstallPackage(PackageSpec package)
        {
            AbortOnInvalidConfiguration();
            await _packageManager.UninstallAsync(package, this, _cancelAll.Token);
        }

        public event EventHandler<OutputEventArgs> OutputTextReceived;

        public void OnOutputTextReceived(IPackageManager sender, string text)
        {
            OutputTextReceived?.Invoke(this, new OutputEventArgs(text));
        }

        public event EventHandler<OutputEventArgs> ErrorTextReceived;

        public void OnErrorTextReceived(IPackageManager sender, string text)
        {
            ErrorTextReceived?.Invoke(this, new OutputEventArgs(text));
        }

        public event EventHandler<OutputEventArgs> OperationStarted;

        public void OnOperationStarted(IPackageManager sender, string operation)
        {
            OperationStarted?.Invoke(this, new OutputEventArgs(operation));
        }

        public event EventHandler<OperationFinishedEventArgs> OperationFinished;

        public void OnOperationFinished(IPackageManager sender, string operation, bool success)
        {
            OperationFinished?.Invoke(this, new OperationFinishedEventArgs(operation, success));
        }

        public event EventHandler InstalledPackagesChanged
        {
            add { _packageManager.InstalledPackagesChanged += value; }
            remove { _packageManager.InstalledPackagesChanged -= value; }
        }

        sealed class CallOnDispose : IDisposable
        {
            private readonly Action _action;
            public CallOnDispose(Action action) { _action = action; }
            public void Dispose() { _action(); }
        }
    }

    public sealed class QueryShouldElevateEventArgs : EventArgs
    {
        /// <summary>
        /// On return, if this is true then the operation is aborted.
        /// </summary>
        /// <remarks>
        /// If <see cref="ElevateAsync"/> is set this value is ignored.
        /// </remarks>
        public bool Cancel { get; set; }

        /// <summary>
        /// On return, if this is true then the operation will continue with
        /// elevation.
        /// </summary>
        /// <remarks>
        /// If <see cref="ElevateAsync"/> is set this value is ignored.
        /// </remarks>
        public bool Elevate { get; set; }

        /// <summary>
        /// On return, if this is not null then the task is awaited and the
        /// result is used for <see cref="Elevate"/>. If the task is cancelled,
        /// the operation is cancelled.
        /// </summary>
        public Task<bool> ElevateAsync { get; set; }

        /// <summary>
        /// The configuration of the interpreter that may require elevation.
        /// </summary>
        public IPythonInterpreterFactory Factory { get; }

        public QueryShouldElevateEventArgs(IPythonInterpreterFactory factory)
        {
            Factory = factory;
        }
    }

    public sealed class OutputEventArgs : EventArgs
    {
        public string Data { get; }

        public OutputEventArgs(string data)
        {
            Data = data;
        }
    }

    public sealed class OperationFinishedEventArgs : EventArgs
    {
        public string Operation { get; }
        public bool Success { get; }

        public OperationFinishedEventArgs(string operation, bool success)
        {
            Operation = operation;
            Success = success;
        }
    }
}
