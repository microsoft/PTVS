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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.PythonTools.EnvironmentsList.Properties;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.EnvironmentsList {
    public sealed class PipExtensionProvider : IEnvironmentViewExtension {
        private readonly IPythonInterpreterFactory _factory;
        private readonly string _index;
        private readonly string _indexName;
        private readonly Redirector _output;
        private FrameworkElement _wpfObject;

        private readonly SemaphoreSlim _memoryCacheLock = new SemaphoreSlim(1);
        private Dictionary<string, object> _memoryCache;
        private DateTime _memoryCacheAge;

        private static readonly KeyValuePair<string, string>[] UnbufferedEnv = new[] { 
            new KeyValuePair<string, string>("PYTHONUNBUFFERED", "1")
        };

        private static readonly Version SupportsDashMPip = new Version(2, 7);

        private int _pipLockWaitCount;
        private readonly SemaphoreSlim _pipLock = new SemaphoreSlim(1);
        private CancellationTokenSource _pipCancel = new CancellationTokenSource();

        public PipExtensionProvider(
            IPythonInterpreterFactory factory,
            string index = null,
            string indexName = null
        ) {
            _factory = factory;
            _output = new PipRedirector(this);
            if (Uri.IsWellFormedUriString(index, UriKind.Absolute)) {
                _index = index;
                _indexName = string.IsNullOrEmpty(indexName) ? _index : indexName;
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

        internal async Task<IList<PipPackageView>> EnumeratePackages(bool includeUpdateVersion = true) {
            string[] args;

            if (_factory.Configuration.Version < SupportsDashMPip) {
                args = new [] { "-c", "import pip; pip.main()", "list", "--no-index" };
            } else {
                args = new [] { "-m", "pip", "list", "--no-index" };
            }

            PipPackageView[] packages;

            using (var output = ProcessOutput.RunHiddenAndCapture(_factory.Configuration.InterpreterPath, args)) {
                if ((await output) != 0) {
                    throw new PipException(Resources.ListFailed);
                }

                packages = output.StandardOutputLines
                    .Select(s => new PipPackageView(s))
                    .ToArray();
            }

            if (includeUpdateVersion) {
                var context = TaskScheduler.FromCurrentSynchronizationContext();
                foreach (var p in packages) {
                    GetLatestPackage(p.Name).ContinueWith(t => {
                        var latest = t.Result;
                        if (latest != null && CompareVersions(p.Version, latest.Version) < 0) {
                            p.UpgradeVersion = latest.Version;
                        } else {
                            p.UpgradeVersion = null;
                        }
                    }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, context).DoNotWait();
                }
            }

            return packages;
        }

        private static string BasePackageCachePath {
            get {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Python Tools",
                    "PipCache",
#if DEBUG
                    "Debug",
#endif
                    AssemblyVersionInfo.VSVersion
                );
            }
        }

        private string PackageCachePath {
            get {
                return Path.Combine(
                    BasePackageCachePath,
                    string.Format("{0}_{1}.cache", _factory.Id, _factory.Configuration.Version)
                );
            }
        }

        private static string UpdatePipCachePy {
            get {
                return PythonToolsInstallPath.GetFile("update_pip_cache.py");
            }
        }

        private static async Task<IDisposable> LockFile(string filename, CancellationToken cancel) {
            FileStream stream = null;

            while (stream == null) {
                cancel.ThrowIfCancellationRequested();
                if (!File.Exists(filename)) {
                    try {
                        stream = new FileStream(
                            filename,
                            FileMode.CreateNew,
                            FileAccess.ReadWrite,
                            FileShare.None,
                            8,
                            FileOptions.DeleteOnClose
                        );
                    } catch (DirectoryNotFoundException) {
                        throw;
                    } catch (IOException) {
                    }
                }
                await Task.Delay(100);
            }

            return stream;
        }

        private static bool IsFileOld(string path, TimeSpan age) {
            try {
                return File.GetLastWriteTime(path) < DateTime.Now.Subtract(age);
            } catch (IOException) {
                return true;
            }
        }


        private async Task<Dictionary<string, object>> GetPackageCache(bool forceUpdate) {
            var cachePath = PackageCachePath;

            if (forceUpdate) {
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath));

                using (var fileLock = await LockFile(cachePath + ".lock", CancellationToken.None)) {
                    // Don't force an update if it was only just updated
                    if (IsFileOld(cachePath, TimeSpan.FromSeconds(30.0))) {
                        using (var output = ProcessOutput.RunHiddenAndCapture(
                            _factory.Configuration.InterpreterPath,
                            UpdatePipCachePy,
                            cachePath,
                            string.Format("Programming Language :: Python :: {0}", _factory.Configuration.Version)
                        )) {
                            await output;
                        }
                    }
                }
            }

            try {
                using (var fileLock = await LockFile(cachePath + ".lock", CancellationToken.None))
                using (var stream = new FileStream(cachePath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                    return Unpickle.Load(stream) as Dictionary<string, object>;
                }
            } catch (IOException) {
            }
            return null;
        }

        internal async Task<object> GetLatestCache() {
            Dictionary<string, object> cache = null;

            await _memoryCacheLock.WaitAsync();
            try {
                if (_memoryCache == null || DateTime.UtcNow.Subtract(_memoryCacheAge) > TimeSpan.FromHours(1.0)) {
                    _memoryCache = await GetPackageCache(false) ?? await GetPackageCache(true);
                    _memoryCacheAge = DateTime.UtcNow;
                }
                cache = _memoryCache;
            } finally {
                _memoryCacheLock.Release();
            }

            if (cache == null) {
                return null;
            }

            object timestampObj;
            string timestampString;
            DateTime timestamp;
            if (!cache.TryGetValue("LastUpdate", out timestampObj) ||
                string.IsNullOrEmpty(timestampString = timestampObj as string) ||
                !DateTime.TryParse(timestampString, out timestamp) ||
                timestamp.AddDays(1.0) < DateTime.Now
            ) {
                await _memoryCacheLock.WaitAsync();
                try {
                    _memoryCache = cache = await GetPackageCache(true);
                    _memoryCacheAge = DateTime.UtcNow;
                } finally {
                    _memoryCacheLock.Release();
                }
            }

            return cache;
        }

        private static Dictionary<string, object> GetPackageDataFromCache(object cache) {
            object obj;
            return ((Dictionary<string, object>)cache).TryGetValue("Packages", out obj) ?
                obj as Dictionary<string, object> :
                null;
        }

        private static HashSet<string> GetPreferredPackagesFromCache(object cache) {
            object obj;
            IEnumerable<object> enm;
            if (((Dictionary<string, object>)cache).TryGetValue("Preferred", out obj) &&
                (enm = obj as IEnumerable<object>) != null) {
                return new HashSet<string>(enm.OfType<string>());
            } else {
                return new HashSet<string>();
            }
        }

        internal static PipPackageView GetPackageFromPackageData(string name, Dictionary<string, object> packageData) {
            object obj;
            IList<object> data;
            if (!packageData.TryGetValue(name, out obj) ||
                (data = obj as IList<object>) == null) {
                return null;
            }

            return new PipPackageView(
                name,
                data.ElementAtOrDefault(0) as string ?? "",
                data.ElementAtOrDefault(1) as string ?? ""
            );
        }

        internal static IList<PipPackageView> GetPackagesFromPackageData(
            IEnumerable<string> names,
            Dictionary<string, object> packageData
        ) {
            return (names ?? packageData.Keys)
                .Select(n => GetPackageFromPackageData(n, packageData))
                .Where(ppv => ppv != null)
                .ToArray();
        }

        internal async Task<PipPackageView> GetLatestPackage(string name) {
            var cache = await GetLatestCache();
            if (cache == null) {
                return null;
            }

            var data = GetPackageDataFromCache(cache);
            if (data == null) {
                return null;
            }

            return GetPackageFromPackageData(name, data);
        }

        internal async Task<IList<PipPackageView>> EnumeratePreferredPackages() {
            var cache = await GetLatestCache();
            if (cache == null) {
                return new PipPackageView[0];
            }

            var names = GetPreferredPackagesFromCache(cache);
            if (names == null) {
                return new PipPackageView[0];
            }

            var data = GetPackageDataFromCache(cache);
            if (data == null) {
                return new PipPackageView[0];
            }

            return GetPackagesFromPackageData(names, data);
        }

        internal async Task<IList<PipPackageView>> EnumerateAvailablePackages() {
            var cache = await GetLatestCache();
            if (cache == null) {
                return new PipPackageView[0];
            }

            var data = GetPackageDataFromCache(cache);
            if (data == null) {
                return new PipPackageView[0];
            }

            return GetPackagesFromPackageData(null, data);
        }

        private static string AsLogicalString(string str) {
            int value;
            if (int.TryParse(str, out value)) {
                return value.ToString("0000000000");
            }
            return str;
        }

        // TODO: Implement https://www.python.org/dev/peps/pep-0440/
        private static int CompareVersions(string x, string y) {
            return x.Split('.').Select(AsLogicalString).Zip(
                y.Split('.').Select(AsLogicalString),
                (b1, b2) => b1.CompareTo(b2)
            ).FirstOrDefault(c => c != 0);
        }

        /// <summary>
        /// Determines the version the package can be upgraded to.
        /// </summary>
        /// <param name="package">The package to check.</param>
        /// <returns>
        /// The newer version if one is available; otherwise, <c>null</c>.
        /// </returns>
        internal async Task<string> GetUpgradeVersionAsync(PipPackageView package) {
            var latestView = await GetLatestPackage(package.Name);
            if (latestView == null) {
                return null;
            }
            return CompareVersions(package.Version, latestView.Version) < 0 ? latestView.Version : null;
        }

        internal async Task<bool> IsPipInstalled() {
            using (var output = ProcessOutput.RunHiddenAndCapture(
                _factory.Configuration.InterpreterPath,
                "-c", "import pip"
            )) {
                try {
                    return (await output) == 0;
                } catch (OperationCanceledException) {
                    return false;
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

        public async Task InstallPip() {
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
                    if (!output.IsStarted) {
                        OnOperationFinished(Resources.InstallingPipFailed);
                        return;
                    }
                    bool success = true;
                    try {
                        var exitCode = await output;
                        if (exitCode != 0) {
                            success = false;
                            throw new PipException(Resources.InstallationFailed);
                        }
                    } finally {
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

            if (!string.IsNullOrEmpty(_index)) {
                args.Add("--index-url");
                args.Add(_index);
            }

            using (await WaitAndLockPip()) {
                OnOperationStarted(string.Format(Resources.InstallingPackageStarted, package));
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
                        OnOperationFinished(string.Format(Resources.InstallingPackageFailed, package));
                        return;
                    }
                    bool success = true;
                    try {
                        var exitCode = await output;
                        if (exitCode != 0) {
                            success = false;
                            throw new PipException(Resources.InstallationFailed);
                        }
                    } finally {
                        OnOperationFinished(string.Format(
                            success ? Resources.InstallingPackageSuccess : Resources.InstallingPackageFailed,
                            package
                        ));
                    }
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
                        OnOperationFinished(string.Format(Resources.UninstallingPackageFailed, package));
                        return;
                    }
                    bool success = true;
                    try {
                        var exitCode = await output;
                        if (exitCode != 0) {
                            // Double check whether the package has actually
                            // been uninstalled, to avoid reporting errors 
                            // where, for all practical purposes, there is no
                            // error.
                            if ((await EnumeratePackages(includeUpdateVersion: false)).Any(p => p.Name == package)) {
                                success = false;
                                throw new PipException(Resources.UninstallationFailed);
                            }
                        }
                    } finally {
                        OnOperationFinished(string.Format(
                            success ? Resources.UninstallingPackageSuccess : Resources.UninstallingPackageFailed,
                            package
                        ));
                    }
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

    internal sealed class PipPackageView : INotifyPropertyChanged {
        private readonly string _packageSpec;
        private readonly string _name;
        private readonly string _version;
        private readonly string _displayName;
        private readonly string _description;
private  string _upgradeVersion;

        internal PipPackageView(string name, string version, string description) {
            _name = name ?? "";
            _version = version ?? "";
            _description = description ?? "";
            _displayName = string.Format("{0} ({1})", _name, _version);
            _packageSpec = string.Format("{0}=={1}", _name, _version);
        }

        internal PipPackageView(string packageSpec) {
            var m = Regex.Match(packageSpec, @"(.+?) \((.+)\)");
            if (m.Success) {
                _displayName = packageSpec;
                _name = m.Groups[1].Value;
                _version = m.Groups[2].Value;
                _packageSpec = string.Format("{0}=={1}", _name, _version);
            } else if ((m = Regex.Match(packageSpec, @"(.+?)==(.+)")).Success) {
                _packageSpec = packageSpec;
                _name = m.Groups[1].Value;
                _version = m.Groups[2].Value;
                _displayName = string.Format("{0} ({1})", _name, _version);
            } else {
                _packageSpec = packageSpec;
                _name = packageSpec;
                _version = string.Empty;
                _displayName = packageSpec;
            }

        }

        public string PackageSpec {
            get { return _packageSpec; }
        }

        public string Name {
            get { return _name; }
        }

        public string Version {
            get { return _version; }
        }

        public string DisplayName {
            get { return _displayName; }
        }

        public string Description {
            get { return _description; }
        }

        public string UpgradeVersion {
            get { return _upgradeVersion; }
            set {
                if (_upgradeVersion != value) {
                    _upgradeVersion = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            var evt = PropertyChanged;
            if (evt != null) {
                evt(this, new PropertyChangedEventArgs(propertyName));
            }
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
