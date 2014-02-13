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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter.Default;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// Base class for interpreter factories that have an executable file
    /// following CPython command-line conventions, a standard library that is
    /// stored on disk as .py files, and using <see cref="PythonTypeDatabase"/>
    /// for the completion database.
    /// </summary>
    public class PythonInterpreterFactoryWithDatabase :
        IPythonInterpreterFactoryWithDatabase,
        IDisposable
    {
        private readonly string _description;
        private readonly Guid _id;
        private readonly InterpreterConfiguration _config;
        private PythonTypeDatabase _typeDb, _typeDbWithoutPackages;
        private bool _generating, _isValid, _isCheckingDatabase, _disposed;
        private string[] _missingModules;
        private string _isCurrentException;
        private readonly object _isCurrentLock = new object();
        private readonly Timer _refreshIsCurrentTrigger;
        private FileSystemWatcher _libWatcher;
        private readonly object _libWatcherLock = new object();
        private FileSystemWatcher _verWatcher;
        private readonly object _verWatcherLock = new object();

        public PythonInterpreterFactoryWithDatabase(
            Guid id,
            string description,
            InterpreterConfiguration config,
            bool watchLibraryForChanges
        ) {
            _description = description;
            _id = id;
            _config = config;

            if (watchLibraryForChanges && Directory.Exists(_config.LibraryPath)) {
                _refreshIsCurrentTrigger = new Timer(RefreshIsCurrentTimer_Elapsed);
                try {
                    _libWatcher = new FileSystemWatcher {
                        IncludeSubdirectories = true,
                        Path = _config.LibraryPath,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
                    };
                    _libWatcher.Created += OnChanged;
                    _libWatcher.Deleted += OnChanged;
                    _libWatcher.Changed += OnChanged;
                    _libWatcher.Renamed += OnRenamed;
                    _libWatcher.EnableRaisingEvents = true;
                } catch (ArgumentException ex) {
                    Debug.WriteLine("Error starting FileSystemWatcher:\r\n{0}", ex);
                }

                _isCheckingDatabase = true;
                _refreshIsCurrentTrigger.Change(1000, Timeout.Infinite);

                _verWatcher = CreateDatabaseVerWatcher();
            }

            // Assume the database is valid if the directory exists, then switch
            // to invalid after we've checked.
            _isValid = Directory.Exists(DatabasePath);
        }

        public InterpreterConfiguration Configuration {
            get {
                return _config;
            }
        }

        public virtual string Description {
            get { return _description; }
        }

        public Guid Id {
            get { return _id; }
        }

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
            if (!_generating && PythonTypeDatabase.IsDatabaseVersionCurrent(databasePath)) {
                var paths = new List<string>();
                paths.Add(databasePath);
                if (includeSitePackages) {
                    paths.AddRange(Directory.EnumerateDirectories(databasePath));
                }

                try {
                    return new PythonTypeDatabase(this, paths);
                } catch (IOException) {
                } catch (UnauthorizedAccessException) {
                }
            }

            return PythonTypeDatabase.CreateDefaultTypeDatabase(this);
        }

        private bool WatchingLibrary {
            get {
                if (_libWatcher != null) {
                    lock (_libWatcherLock) {
                        return _libWatcher != null && _libWatcher.EnableRaisingEvents;
                    }
                }
                return false;
            }
            set {
                if (_libWatcher != null) {
                    lock (_libWatcherLock) {
                        if (_libWatcher == null || _libWatcher.EnableRaisingEvents == value) {
                            return;
                        }
                        try {
                            _libWatcher.EnableRaisingEvents = value;
                        } catch (IOException) {
                            // May occur if the library has been deleted while the
                            // watcher was disabled.
                            _libWatcher.Dispose();
                            _libWatcher = null;
                        } catch (ObjectDisposedException) {
                            _libWatcher = null;
                        }
                    }
                }
            }
        }

        public virtual void GenerateDatabase(GenerateDatabaseOptions options, Action<int> onExit = null) {
            var req = new PythonTypeDatabaseCreationRequest {
                Factory = this,
                OutputPath = DatabasePath,
                SkipUnchanged = options.HasFlag(GenerateDatabaseOptions.SkipUnchanged)
            };

            GenerateDatabase(req, onExit);
        }

        protected virtual void GenerateDatabase(PythonTypeDatabaseCreationRequest request, Action<int> onExit = null) {
            WatchingLibrary = false;
            _generating = true;

            PythonTypeDatabase.GenerateAsync(request).ContinueWith(t => {
                int exitCode;
                try {
                    exitCode = t.Result;
                } catch (Exception ex) {
                    Debug.Fail(ex.ToString());
                    exitCode = PythonTypeDatabase.InvalidOperationExitCode;
                }

                if (exitCode != PythonTypeDatabase.AlreadyGeneratingExitCode) {
                    _generating = false;
                }
                if (onExit != null) {
                    onExit(exitCode);
                }
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

        public bool IsGenerating {
            get {
                return _generating;
            }
        }

        private void OnDatabaseVerChanged(object sender, FileSystemEventArgs e) {
            if ((!e.Name.Equals("database.ver", StringComparison.OrdinalIgnoreCase) &&
                !e.Name.Equals("database.pid", StringComparison.OrdinalIgnoreCase)) ||
                !CommonUtils.IsSubpathOf(DatabasePath, e.FullPath)) {
                return;
            }

            RefreshIsCurrent();

            if (IsCurrent) {
                NotifyNewDatabase();
            }
        }

        private void NotifyNewDatabase() {
            _generating = false;
            OnIsCurrentChanged();

            WatchingLibrary = true;

            // This also clears the previous database so that we load the new
            // one next time someone requests it.
            OnNewDatabaseAvailable();
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

        public virtual string DatabasePath {
            get {
                return Path.Combine(
                    PythonTypeDatabase.CompletionDatabasePath,
                    Id.ToString(),
                    Configuration.Version.ToString()
                );
            }
        }

        public string GetAnalysisLogContent(IFormatProvider culture) {
            var analysisLog = Path.Combine(DatabasePath, "AnalysisLog.txt");
            if (File.Exists(analysisLog)) {
                try {
                    return File.ReadAllText(analysisLog);
                } catch (Exception e) {
                    return string.Format(culture, "Error reading: {0}", e);
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
            lock (_isCurrentLock) {
                try {
                    _isCheckingDatabase = true;
                    OnIsCurrentChanged();

                    _generating = PythonTypeDatabase.IsDatabaseRegenerating(DatabasePath);
                    WatchingLibrary = !_generating;

                    if (_generating) {
                        // Skip the rest of the checks, because we know we're
                        // not current.
                    } else if (!PythonTypeDatabase.IsDatabaseVersionCurrent(DatabasePath)) {
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
                } catch (Exception ex) {
                    // Report the exception text as the reason.
                    _isCurrentException = ex.ToString();
                    _missingModules = null;
                } finally {
                    _isCheckingDatabase = false;
                }
            }

            OnIsCurrentChanged();
        }

        private static HashSet<string> GetExistingDatabase(string databasePath) {
            return new HashSet<string>(
                Directory.EnumerateFiles(databasePath, "*.idb", SearchOption.AllDirectories)
                    .Select(f => Path.GetFileNameWithoutExtension(f)),
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
            return ModulePath.GetModulesInLib(this)
                .Select(mp => mp.ModuleName)
                .Concat(RequiredBuiltinModules)
                .Where(name => !existingDatabase.Contains(name))
                .OrderBy(name => name, StringComparer.InvariantCultureIgnoreCase)
                .ToArray();
        }

        private void RefreshIsCurrentTimer_Elapsed(object state) {
            if (_disposed) {
                return;
            }

            if (Directory.Exists(Configuration.LibraryPath)) {
                RefreshIsCurrent();
            } else {
                if (_libWatcher != null) {
                    lock (_libWatcherLock) {
                        if (_libWatcher != null) {
                            _libWatcher.Dispose();
                            _libWatcher = null;
                        }
                    }
                }
                OnIsCurrentChanged();
            }
        }

        private void OnRenamed(object sender, RenamedEventArgs e) {
            _refreshIsCurrentTrigger.Change(1000, Timeout.Infinite);
        }

        private void OnChanged(object sender, FileSystemEventArgs e) {
            _refreshIsCurrentTrigger.Change(1000, Timeout.Infinite);
        }

        public event EventHandler IsCurrentChanged;

        public event EventHandler NewDatabaseAvailable;

        protected void OnIsCurrentChanged() {
            var evt = IsCurrentChanged;
            if (evt != null) {
                evt(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Clears any cached type databases and raises the
        /// <see cref="NewDatabaseAvailable"/> event.
        /// </summary>
        protected void OnNewDatabaseAvailable() {
            _typeDb = null;
            _typeDbWithoutPackages = null;

            var evt = NewDatabaseAvailable;
            if (evt != null) {
                evt(this, EventArgs.Empty);
            }
        }

        static string GetPackageName(string fullName) {
            int firstDot = fullName.IndexOf('.');
            return (firstDot > 0) ? fullName.Remove(firstDot) : fullName;
        }

        public virtual string GetFriendlyIsCurrentReason(IFormatProvider culture) {
            var missingModules = _missingModules;
            if (_isCurrentException != null) {
                return "An error occurred. Click Copy to get full details.";
            } else if (_generating) {
                return "Currently regenerating";
            } else if (_libWatcher == null) {
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
            var missingModules = _missingModules;
            var reason = "Database at " + DatabasePath;
            if (_isCurrentException != null) {
                return reason + " raised an exception while refreshing:" + Environment.NewLine + _isCurrentException;
            } else if (_generating) {
                return reason + " is regenerating";
            } else if (_libWatcher == null) {
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


        #region IDisposable Members

        protected virtual void Dispose(bool disposing) {
            if (!_disposed) {
                _disposed = true;
                if (_verWatcher != null) {
                    lock (_verWatcherLock) {
                        if (_verWatcher != null) {
                            _verWatcher.EnableRaisingEvents = false;
                            _verWatcher.Dispose();
                            _verWatcher = null;
                        }
                    }
                }

                if (_libWatcher != null) {
                    lock (_libWatcherLock) {
                        if (_libWatcher != null) {
                            _libWatcher.EnableRaisingEvents = false;
                            _libWatcher.Dispose();
                            _libWatcher = null;
                        }
                    }
                }
                if (_refreshIsCurrentTrigger != null) {
                    _refreshIsCurrentTrigger.Dispose();
                }
            }
        }

        public void Dispose() {
            Dispose(true);
        }

        #endregion

        #region Database.ver watcher

        private FileSystemWatcher CreateDatabaseVerWatcher() {
            lock (_verWatcherLock) {
                var dir = DatabasePath;
                if (Directory.Exists(dir)) {
                    try {
                        var watcher = new FileSystemWatcher {
                            IncludeSubdirectories = false,
                            Path = dir,
                            Filter = "database.*",
                            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
                        };
                        watcher.Deleted += OnDatabaseVerChanged;
                        watcher.Created += OnDatabaseVerChanged;
                        watcher.Changed += OnDatabaseVerChanged;
                        watcher.EnableRaisingEvents = true;
                        return watcher;
                    } catch (ArgumentException ex) {
                        Debug.WriteLine("Error starting database.ver FileSystemWatcher:\r\n{0}", ex);
                    }
                    return null;
                } else {
                    var dirName = Path.GetFileName(CommonUtils.TrimEndSeparator(dir));
                    while (CommonUtils.IsValidPath(dir) && !Directory.Exists(dir)) {
                        dir = Path.GetDirectoryName(dir);
                    }
                    if (Directory.Exists(dir)) {
                        var watcher = new FileSystemWatcher {
                            IncludeSubdirectories = true,
                            Path = dir,
                            Filter = dirName,
                            NotifyFilter = NotifyFilters.DirectoryName
                        };
                        watcher.Created += OnDatabaseFolderCreated;
                        watcher.EnableRaisingEvents = true;
                        return watcher;
                    } else {
                        return null;
                    }
                }
            }
        }

        private void OnDatabaseFolderCreated(object sender, FileSystemEventArgs e) {
            var watcher = CreateDatabaseVerWatcher();
            if (watcher != null) {
                lock (_verWatcherLock) {
                    _verWatcher.EnableRaisingEvents = false;
                    _verWatcher.Dispose();
                    _verWatcher = watcher;
                }
            }
        }

        #endregion
    }
}
