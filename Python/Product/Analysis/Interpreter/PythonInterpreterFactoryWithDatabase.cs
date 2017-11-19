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
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter.Default;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// Base class for interpreter factories that have an executable file
    /// following CPython command-line conventions, a standard library that is
    /// stored on disk as .py files, and using <see cref="PythonTypeDatabase"/>
    /// for the completion database.
    /// </summary>
    public class PythonInterpreterFactoryWithDatabase :
        IPythonInterpreterFactoryWithDatabase,
        IPythonInterpreterFactoryWithLog,
        IDisposable
    {
        private PythonTypeDatabase _typeDb, _typeDbWithoutPackages;
        private IDisposable _generating;
        private bool _isValid, _isCheckingDatabase, _disposed;
        private readonly string _databasePath;
#if DEBUG
        private bool _hasEverCheckedDatabase;
#endif
        private string[] _missingModules;
        private string _isCurrentException;
        private FileSystemWatcher _verWatcher;
        private FileSystemWatcher _verDirWatcher;
        private readonly object _verWatcherLock = new object();

        // Only one thread can be updating our current state
        private readonly SemaphoreSlim _isCurrentSemaphore = new SemaphoreSlim(1);

        // Only four threads can be updating any state. This is to prevent I/O
        // saturation when multiple threads refresh simultaneously.
        private static readonly SemaphoreSlim _sharedIsCurrentSemaphore = new SemaphoreSlim(4);

        /// <summary>
        /// Creates a new interpreter factory backed by a type database.
        /// </summary>
        /// <remarks>
        /// <see cref="RefreshIsCurrent"/> must be called after creation to
        /// ensure the database state is correctly reflected.
        /// </remarks>
        [SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public PythonInterpreterFactoryWithDatabase(
            InterpreterConfiguration config,
            InterpreterFactoryCreationOptions options
        ) {
            Configuration = config ?? throw new ArgumentNullException(nameof(config));
            CreationOptions = options ?? new InterpreterFactoryCreationOptions();

            _databasePath = CreationOptions.DatabasePath;

            // Avoid creating a interpreter with an unsupported version.
            // https://github.com/Microsoft/PTVS/issues/706
            try {
                var langVer = config.Version.ToLanguageVersion();
            } catch (InvalidOperationException ex) {
                throw new ArgumentException(ex.Message, ex);
            }

            if (!GlobalInterpreterOptions.SuppressFileSystemWatchers && CreationOptions.WatchFileSystem && !string.IsNullOrEmpty(DatabasePath)) {
                // Assume the database is valid if the version is up to date, then
                // switch to invalid after we've checked.
                _isValid = PythonTypeDatabase.IsDatabaseVersionCurrent(DatabasePath);
                _isCheckingDatabase = true;

                _verWatcher = CreateDatabaseVerWatcher();
                _verDirWatcher = CreateDatabaseDirectoryWatcher();
#if DEBUG
                var creationStack = new StackTrace(true).ToString();
                Task.Delay(1000).ContinueWith(t => {
                    Debug.Assert(
                        _hasEverCheckedDatabase,
                        "Database check was not triggered for {0}".FormatUI(Configuration.Id),
                        creationStack
                    );
                });
#endif
            } else {
                // Assume the database is valid
                _isValid = true;
            }

            if (!GlobalInterpreterOptions.SuppressPackageManagers) {
                try {
                    var pm = CreationOptions.PackageManager;
                    if (pm != null) {
                        pm.SetInterpreterFactory(this);
                        pm.InstalledFilesChanged += PackageManager_InstalledFilesChanged;
                        PackageManager = pm;
                    }
                } catch (NotSupportedException) {
                }
            }
        }

        public virtual void BeginRefreshIsCurrent() {
#if DEBUG
            _hasEverCheckedDatabase = true;
#endif
            Task.Run(() => RefreshIsCurrent()).DoNotWait();
        }

        private void PackageManager_InstalledFilesChanged(object sender, EventArgs e) {
            BeginRefreshIsCurrent();
        }

        public InterpreterConfiguration Configuration { get; }

        public InterpreterFactoryCreationOptions CreationOptions { get; }

        public IPackageManager PackageManager { get; }

        /// <summary>
        /// Returns a new interpreter created with the specified factory.
        /// </summary>
        /// <remarks>
        /// This is intended for use by derived classes only. To get an
        /// interpreter instance, use <see cref="CreateInterpreter"/>.
        /// </remarks>
        public virtual IPythonInterpreter MakeInterpreter(PythonInterpreterFactoryWithDatabase factory) {
            return new CPythonInterpreter(factory);
        }

        public IPythonInterpreter CreateInterpreter() {
            return MakeInterpreter(this);
        }

        /// <summary>
        /// Returns the database for this factory. This database may be shared
        /// between callers and should be cloned before making modifications.
        /// 
        /// This function never returns null.
        /// </summary>
        public PythonTypeDatabase GetCurrentDatabase() {
            if (_typeDb == null || _typeDb.DatabaseDirectory != DatabasePath) {
                _typeDb = MakeTypeDatabase(DatabasePath) ??
                    PythonTypeDatabase.CreateDefaultTypeDatabase(this);
            }

            return _typeDb;
        }

        /// <summary>
        /// Returns the database for this factory, optionally excluding package
        /// analysis. This database may be shared between callers and should be
        /// cloned before making modifications.
        /// 
        /// This function never returns null.
        /// </summary>
        public PythonTypeDatabase GetCurrentDatabase(bool includeSitePackages) {
            if (includeSitePackages) {
                return GetCurrentDatabase();
            }

            if (_typeDbWithoutPackages == null || _typeDbWithoutPackages.DatabaseDirectory != DatabasePath) {
                _typeDbWithoutPackages = MakeTypeDatabase(DatabasePath, false) ??
                    PythonTypeDatabase.CreateDefaultTypeDatabase(this);
            }

            return _typeDbWithoutPackages;
        }

        /// <summary>
        /// Returns a new database loaded from the specified path. If null is
        /// returned, <see cref="GetCurrentDatabase"/> will assume the default
        /// completion DB is intended.
        /// </summary>
        /// <remarks>
        /// This is intended for overriding in derived classes. To get a
        /// queryable database instance, use <see cref="GetCurrentDatabase"/> or
        /// <see cref="CreateInterpreter"/>.
        /// </remarks>
        public virtual PythonTypeDatabase MakeTypeDatabase(string databasePath, bool includeSitePackages = true) {
            if (!string.IsNullOrEmpty(databasePath) && !IsGenerating && PythonTypeDatabase.IsDatabaseVersionCurrent(databasePath)) {
                var paths = new List<string>();
                paths.Add(databasePath);
                if (includeSitePackages) {
                    paths.AddRange(PathUtils.EnumerateDirectories(databasePath, recurse: false));
                }

                try {
                    return new PythonTypeDatabase(this, paths);
                } catch (IOException) {
                } catch (UnauthorizedAccessException) {
                }
            }

            return PythonTypeDatabase.CreateDefaultTypeDatabase(this);
        }

        public virtual void GenerateDatabase(GenerateDatabaseOptions options, Action<int> onExit = null) {
            if (string.IsNullOrEmpty(DatabasePath)) {
                onExit?.Invoke(PythonTypeDatabase.NotSupportedExitCode);
                return;
            }
            if (IsGenerating) {
                onExit?.Invoke(PythonTypeDatabase.AlreadyGeneratingExitCode);
                return;
            }

            var req = new PythonTypeDatabaseCreationRequest {
                Factory = this,
                OutputPath = DatabasePath,
                SkipUnchanged = options.HasFlag(GenerateDatabaseOptions.SkipUnchanged)
            };

            GenerateDatabase(req, onExit);
        }

        protected virtual void GenerateDatabase(PythonTypeDatabaseCreationRequest request, Action<int> onExit = null) {
            // Use the NoPackageManager instance if we don't have a package
            // manager, so that we still get a valid disposable object while we
            // are generating.
            var generating = _generating = (request.Factory.PackageManager ?? NoPackageManager.Instance).SuppressNotifications();

            PythonTypeDatabase.GenerateAsync(request).ContinueWith(t => {
                int exitCode;
                try {
                    exitCode = t.Result;
                } catch (Exception ex) {
                    Debug.Fail(ex.ToString());
                    exitCode = PythonTypeDatabase.InvalidOperationExitCode;
                }

                if (exitCode != PythonTypeDatabase.AlreadyGeneratingExitCode) {
                    generating.Dispose();
                    Interlocked.CompareExchange(ref _generating, null, generating);
                }
                onExit?.Invoke(exitCode);
            });
        }

        /// <summary>
        /// Called to inform the interpreter that its database cannot be loaded
        /// and may need to be regenerated.
        /// </summary>
        public void NotifyCorruptDatabase() {
            _isValid = false;

            OnIsCurrentChanged();
            OnNewDatabaseAvailable();
        }

        public bool IsGenerating => _generating != null;

        private void OnDatabaseVerChanged(object sender, FileSystemEventArgs e) {
            Debug.Assert(!string.IsNullOrEmpty(DatabasePath));

            if ((!e.Name.Equals("database.ver", StringComparison.OrdinalIgnoreCase) &&
                !e.Name.Equals("database.pid", StringComparison.OrdinalIgnoreCase)) ||
                !PathUtils.IsSubpathOf(DatabasePath, e.FullPath)) {
                return;
            }

            RefreshIsCurrent();

            if (IsCurrent) {
                var generating = Interlocked.Exchange(ref _generating, null);
                generating?.Dispose();
                OnIsCurrentChanged();

                // This also clears the previous database so that we load the new
                // one next time someone requests it.
                OnNewDatabaseAvailable();
            }
        }

        private bool IsValid {
            get {
                return _isValid && _missingModules == null && _isCurrentException == null;
            }
        }

        public virtual bool IsCurrent {
            get {
                return !IsGenerating && IsValid;
            }
        }

        public virtual bool IsCheckingDatabase {
            get {
                return _isCheckingDatabase;
            }
        }

        public virtual string DatabasePath { get { return _databasePath; } }

        public string GetAnalysisLogContent(IFormatProvider culture) {
            if (string.IsNullOrEmpty(DatabasePath)) {
                return "No log for this interpreter";
            }

            var analysisLog = Path.Combine(DatabasePath, "AnalysisLog.txt");
            if (File.Exists(analysisLog)) {
                try {
                    return File.ReadAllText(analysisLog);
                } catch (Exception ex) {
                    return string.Format(
                        culture,
                        "Error reading {0}. Please let analysis complete and try again.\r\n{1}",
                        analysisLog,
                        ex
                    );
                }
            }
            return null;
        }

        /// <summary>
        /// Called to manually trigger a refresh of <see cref="IsCurrent"/>.
        /// After completion, <see cref="IsCurrentChanged"/> will always be
        /// raised, regardless of whether the values were changed.
        /// </summary>
        public virtual void RefreshIsCurrent() {
#if DEBUG
            // Indicate that we've arrived here at least once. Otherwise we
            // will assert.
            _hasEverCheckedDatabase = true;
#endif

            if (GlobalInterpreterOptions.SuppressFileSystemWatchers || string.IsNullOrEmpty(DatabasePath)) {
                _isCheckingDatabase = false;
                _isValid = true;
                _missingModules = null;
                _isCurrentException = null;
                OnIsCurrentChanged();
                return;
            }
            if (IsGenerating) {
                if (PythonTypeDatabase.IsDatabaseRegenerating(DatabasePath)) {
                    _isValid = false;
                    _missingModules = null;
                    _isCurrentException = null;
                    return;
                }
                var generating = Interlocked.Exchange(ref _generating, null);
                generating?.Dispose();
            }

            try {
                if (!_isCurrentSemaphore.Wait(0)) {
                    // Another thread is working on our state, so we will wait for
                    // them to finish and return, since the value is up to date.
                    _isCurrentSemaphore.Wait();
                    _isCurrentSemaphore.Release();
                    return;
                }
            } catch (ObjectDisposedException) {
                // We've been disposed and the call has come in from
                // externally, probably a timer.
                return;
            }
            
            try {
                _isCheckingDatabase = true;
                OnIsCurrentChanged();

                // Wait for a slot that will allow us to scan the disk. This
                // prevents too many threads from updating at once and causing
                // I/O saturation.
                _sharedIsCurrentSemaphore.Wait();

                try {
                    if (PythonTypeDatabase.IsDatabaseRegenerating(DatabasePath) ||
                        !PythonTypeDatabase.IsDatabaseVersionCurrent(DatabasePath)) {
                        // Skip the rest of the checks, because we know we're
                        // not current.
                        _isValid = false;
                        _missingModules = null;
                        _isCurrentException = null;
                    } else {
                        _isValid = true;
                        HashSet<string> existingDatabase = null;
                        string[] missingModules = null;

                        for (int retries = 3; retries > 0; --retries) {
                            try {
                                existingDatabase = GetExistingDatabase(DatabasePath);
                                break;
                            } catch (UnauthorizedAccessException) {
                            } catch (IOException) {
                            }
                            Thread.Sleep(100);
                        }

                        if (existingDatabase == null) {
                            // This will either succeed or throw again. If it throws
                            // then the error is reported to the user.
                            existingDatabase = GetExistingDatabase(DatabasePath);
                        }

                        for (int retries = 3; retries > 0; --retries) {
                            try {
                                missingModules = GetMissingModules(existingDatabase);
                                break;
                            } catch (UnauthorizedAccessException) {
                            } catch (IOException) {
                            }
                            Thread.Sleep(100);
                        }

                        if (missingModules == null) {
                            // This will either succeed or throw again. If it throws
                            // then the error is reported to the user.
                            missingModules = GetMissingModules(existingDatabase);
                        }

                        if (missingModules.Length > 0) {
                            var oldModules = _missingModules;
                            if (oldModules == null ||
                                oldModules.Length != missingModules.Length ||
                                !oldModules.SequenceEqual(missingModules)) {
                            }
                            _missingModules = missingModules;
                        } else {
                            _missingModules = null;
                        }
                    }
                    _isCurrentException = null;
                } finally {
                    _sharedIsCurrentSemaphore.Release();
                }
            } catch (Exception ex) {
                // Report the exception text as the reason.
                _isCurrentException = ex.ToString();
                _missingModules = null;
            } finally {
                _isCheckingDatabase = false;
                try {
                    _isCurrentSemaphore.Release();
                } catch (ObjectDisposedException) {
                    // The semaphore is not locked for disposal as it is only
                    // used to prevent reentrance into this function. As a
                    // result, it may have been disposed while we were in here.
                }
            }

            OnIsCurrentChanged();
        }

        private static HashSet<string> GetExistingDatabase(string databasePath) {
            Debug.Assert(!string.IsNullOrEmpty(databasePath));

            return new HashSet<string>(
                PathUtils.EnumerateFiles(databasePath, "*.idb").Select(f => Path.GetFileNameWithoutExtension(f)),
                StringComparer.InvariantCultureIgnoreCase
            );
        }

        /// <summary>
        /// Returns a sequence of module names that are required for a database.
        /// If any of these are missing, the database will be marked as invalid.
        /// </summary>
        private IEnumerable<string> RequiredBuiltinModules {
            get {
                if (Configuration.Version.Major == 2) {
                    yield return "__builtin__";
                } else if (Configuration.Version.Major == 3) {
                    yield return "builtins";
                }
            }
        }

        private string[] GetMissingModules(HashSet<string> existingDatabase) {
            Debug.Assert(!string.IsNullOrEmpty(DatabasePath));

            var searchPaths = PythonTypeDatabase.GetCachedDatabaseSearchPaths(DatabasePath);

            if (searchPaths == null) {
                // No cached search paths means our database is out of date.
                return existingDatabase
                    .Except(RequiredBuiltinModules)
                    .OrderBy(name => name, StringComparer.InvariantCultureIgnoreCase)
                    .ToArray();
            }
            
            return PythonTypeDatabase.GetDatabaseExpectedModules(Configuration.Version, searchPaths)
                .SelectMany()
                .Select(mp => mp.ModuleName)
                .Concat(RequiredBuiltinModules)
                .Where(m => !existingDatabase.Contains(m))
                .OrderBy(name => name, StringComparer.InvariantCultureIgnoreCase)
                .ToArray();
        }

        public event EventHandler IsCurrentChanged;

        public event EventHandler NewDatabaseAvailable;

        protected void OnIsCurrentChanged() {
            IsCurrentChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Clears any cached type databases and raises the
        /// <see cref="NewDatabaseAvailable"/> event.
        /// </summary>
        protected void OnNewDatabaseAvailable() {
            _typeDb = null;
            _typeDbWithoutPackages = null;

            NewDatabaseAvailable?.Invoke(this, EventArgs.Empty);
        }

        static string GetPackageName(string fullName) {
            int firstDot = fullName.IndexOf('.');
            return (firstDot > 0) ? fullName.Remove(firstDot) : fullName;
        }

        public virtual string GetFriendlyIsCurrentReason(IFormatProvider culture) {
            if (string.IsNullOrEmpty(DatabasePath)) {
                return "Interpreter has no database";
            }

            var missingModules = _missingModules;
            if (_isCurrentException != null) {
                return "An error occurred. Click Copy to get full details.";
            } else if (_generating != null) {
                return "Currently regenerating";
            } else if (PackageManager == null) {
                return "Interpreter has no library";
            } else if (!Directory.Exists(DatabasePath)) {
                return "Database has never been generated";
            } else if (!_isValid) {
                return "Database is corrupt or an old version";
            } else if (missingModules != null) {
                if (missingModules.Length < 100) {
                    return string.Format(culture,
                        "The following modules have not been analyzed:{0}    {1}",
                        Environment.NewLine,
                        string.Join(Environment.NewLine + "    ", missingModules)
                    );
                } else {
                    var packages = new List<string>(
                        from m in missingModules
                        group m by GetPackageName(m) into groupedByPackage
                        where groupedByPackage.Count() > 1
                        orderby groupedByPackage.Key
                        select groupedByPackage.Key
                    );

                    if (packages.Count > 0 && packages.Count < 100) {
                        return string.Format(culture,
                            "{0} modules have not been analyzed.{2}Packages include:{2}    {1}",
                            missingModules.Length,
                            string.Join(Environment.NewLine + "    ", packages),
                            Environment.NewLine
                        );
                    } else {
                        return string.Format(culture,
                            "{0} modules have not been analyzed.",
                            missingModules.Length
                        );
                    }
                }
            }

            return "Up to date";
        }

        public virtual string GetIsCurrentReason(IFormatProvider culture) {
            if (string.IsNullOrEmpty(DatabasePath)) {
                return "Interpreter has no database";
            }

            var missingModules = _missingModules;
            var reason = "Database at " + DatabasePath;
            if (_isCurrentException != null) {
                return reason + " raised an exception while refreshing:" + Environment.NewLine + _isCurrentException;
            } else if (_generating != null) {
                return reason + " is regenerating";
            } else if (PackageManager == null) {
                return "Interpreter has no library";
            } else if (!Directory.Exists(DatabasePath)) {
                return reason + " does not exist";
            } else if (!_isValid) {
                return reason + " is corrupt or an old version";
            } else if (missingModules != null) {
                return reason + " does not contain the following modules:" + Environment.NewLine +
                    string.Join(Environment.NewLine, missingModules);
            }

            return reason + " is up to date";
        }

        public IEnumerable<string> GetUpToDateModules() {
            if (!Directory.Exists(DatabasePath)) {
                return Enumerable.Empty<string>();
            }

            // Currently we assume that if the file exists, it's up to date.
            // PyLibAnalyzer will perform timestamp checks if the user manually
            // refreshes.
            return PathUtils.EnumerateFiles(DatabasePath, "*.idb")
                .Select(f => Path.GetFileNameWithoutExtension(f));
        }

        #region IDisposable Members

        protected virtual void Dispose(bool disposing) {
            if (!_disposed) {
                _disposed = true;
                if (_verWatcher != null || _verDirWatcher != null) {
                    lock (_verWatcherLock) {
                        if (_verWatcher != null) {
                            _verWatcher.EnableRaisingEvents = false;
                            _verWatcher.Dispose();
                            _verWatcher = null;
                        }
                        if (_verDirWatcher != null) {
                            _verDirWatcher.EnableRaisingEvents = false;
                            _verDirWatcher.Dispose();
                            _verDirWatcher = null;
                        }
                    }
                }

                if (PackageManager != null) {
                    PackageManager.InstalledFilesChanged -= PackageManager_InstalledFilesChanged;
                }
                (PackageManager as IDisposable)?.Dispose();

                _isCurrentSemaphore.Dispose();
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Directory watchers

        private FileSystemWatcher CreateDatabaseDirectoryWatcher() {
            Debug.Assert(!string.IsNullOrEmpty(DatabasePath));
            FileSystemWatcher watcher = null;

            lock (_verWatcherLock) {
                var dirName = PathUtils.GetFileOrDirectoryName(DatabasePath);
                var dir = Path.GetDirectoryName(DatabasePath);

                while (PathUtils.IsValidPath(dir) && !Directory.Exists(dir)) {
                    dirName = PathUtils.GetFileOrDirectoryName(dir);
                    dir = Path.GetDirectoryName(dir);
                }

                if (Directory.Exists(dir)) {
                    try {
                        watcher = new FileSystemWatcher {
                            IncludeSubdirectories = false,
                            Path = dir,
                            Filter = dirName,
                            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
                        };
                    } catch (ArgumentException ex) {
                        Debug.WriteLine("Error starting database directory FileSystemWatcher:\r\n{0}", ex);
                        return null;
                    }

                    watcher.Created += OnDatabaseFolderChanged;
                    watcher.Renamed += OnDatabaseFolderChanged;
                    watcher.Deleted += OnDatabaseFolderChanged;

                    try {
                        watcher.EnableRaisingEvents = true;
                        return watcher;
                    } catch (IOException) {
                        // Raced with directory deletion
                        watcher.Dispose();
                        watcher = null;
                    }
                }

                return null;
            }
        }

        private FileSystemWatcher CreateDatabaseVerWatcher() {
            Debug.Assert(!string.IsNullOrEmpty(DatabasePath));

            FileSystemWatcher watcher = null;

            lock (_verWatcherLock) {
                var dir = DatabasePath;
                if (Directory.Exists(dir)) {
                    try {
                        watcher = new FileSystemWatcher {
                            IncludeSubdirectories = false,
                            Path = dir,
                            Filter = "database.*",
                            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
                        };
                    } catch (ArgumentException ex) {
                        Debug.WriteLine("Error starting database.ver FileSystemWatcher:\r\n{0}", ex);
                        return null;
                    }

                    watcher.Deleted += OnDatabaseVerChanged;
                    watcher.Created += OnDatabaseVerChanged;
                    watcher.Changed += OnDatabaseVerChanged;

                    try {
                        watcher.EnableRaisingEvents = true;
                        return watcher;
                    } catch (IOException) {
                        // Raced with directory deletion. Fall through and find
                        // a parent directory that exists.
                        watcher.Dispose();
                        watcher = null;
                    }
                }

                return null;
            }
        }

        private void OnDatabaseFolderChanged(object sender, FileSystemEventArgs e) {
            lock (_verWatcherLock) {
                if (_verWatcher != null) {
                    _verWatcher.EnableRaisingEvents = false;
                    _verWatcher.Dispose();
                }
                if (_verDirWatcher != null) {
                    _verDirWatcher.EnableRaisingEvents = false;
                    _verDirWatcher.Dispose();
                }
                _verDirWatcher = CreateDatabaseDirectoryWatcher();
                _verWatcher = CreateDatabaseVerWatcher();
                RefreshIsCurrent();
            }
        }

        #endregion
    }
}
