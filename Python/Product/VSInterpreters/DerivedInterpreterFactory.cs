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
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace Microsoft.PythonTools.Interpreter {
    class DerivedInterpreterFactory : PythonInterpreterFactoryWithDatabase {
        readonly PythonInterpreterFactoryWithDatabase _base;
        bool _deferRefreshIsCurrent;

        string _description;

        PythonTypeDatabase _baseDb;
        bool _baseHasRefreshed;

        [SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors",
            Justification = "call to RefreshIsCurrent is required for back compat")]
        public DerivedInterpreterFactory(
            PythonInterpreterFactoryWithDatabase baseFactory,
            InterpreterFactoryCreationOptions options
        ) : base(
                options.Id,
                options.Description,
                new InterpreterConfiguration(
                    options.PrefixPath,
                    options.InterpreterPath,
                    options.WindowInterpreterPath,
                    options.LibraryPath,
                    options.PathEnvironmentVariableName,
                    options.Architecture,
                    options.LanguageVersion,
                    InterpreterUIMode.CannotBeDefault | InterpreterUIMode.CannotBeConfigured
                ),
                options.WatchLibraryForNewModules
        ) {
            if (baseFactory.Configuration.Version != options.LanguageVersion) {
                throw new ArgumentException("Language versions do not match", "options");
            }

            _base = baseFactory;
            _base.IsCurrentChanged += Base_IsCurrentChanged;
            _base.NewDatabaseAvailable += Base_NewDatabaseAvailable;

            _description = options.Description;

            if (Volatile.Read(ref _deferRefreshIsCurrent)) {
                // This rare race condition is due to a design flaw that is in
                // shipped public API and cannot be fixed without breaking
                // compatibility with 3rd parties.
                RefreshIsCurrent();
            }
        }

        private void Base_NewDatabaseAvailable(object sender, EventArgs e) {
            if (_baseDb != null) {
                _baseDb = null;
                _baseHasRefreshed = true;
            }
            OnNewDatabaseAvailable();
            OnIsCurrentChanged();
        }

        protected override void Dispose(bool disposing) {
            _base.IsCurrentChanged -= Base_IsCurrentChanged;
            _base.NewDatabaseAvailable -= Base_NewDatabaseAvailable;

            base.Dispose(disposing);
        }

        public IPythonInterpreterFactory BaseInterpreter {
            get {
                return _base;
            }
        }

        public override string Description {
            get {
                return _description;
            }
        }

        public void SetDescription(string value) {
            _description = value;
        }

        public override IPythonInterpreter MakeInterpreter(PythonInterpreterFactoryWithDatabase factory) {
            return _base.MakeInterpreter(factory);
        }

        private static bool ShouldIncludeGlobalSitePackages(string prefixPath, string libPath) {
            var cfgFile = Path.Combine(prefixPath, "pyvenv.cfg");
            if (File.Exists(cfgFile)) {
                try {
                    var lines = File.ReadAllLines(cfgFile);
                    return !lines
                        .Select(line => Regex.Match(
                            line,
                            "^include-system-site-packages\\s*=\\s*false",
                            RegexOptions.IgnoreCase
                        ))
                        .Any(m => m != null && m.Success);
                } catch (IOException) {
                } catch (UnauthorizedAccessException) {
                } catch (System.Security.SecurityException) {
                }
                return false;
            }

            var prefixFile = Path.Combine(libPath, "orig-prefix.txt");
            var markerFile = Path.Combine(libPath, "no-global-site-packages.txt");
            if (File.Exists(prefixFile)) {
                return !File.Exists(markerFile);
            }

            return false;
        }


        public override PythonTypeDatabase MakeTypeDatabase(string databasePath, bool includeSitePackages = true) {
            if (_baseDb == null && _base.IsCurrent) {
                var includeBaseSitePackages = ShouldIncludeGlobalSitePackages(
                    Configuration.PrefixPath,
                    Configuration.LibraryPath
                );

                _baseDb = _base.GetCurrentDatabase(includeBaseSitePackages);
            }

            if (!IsCurrent || !Directory.Exists(databasePath)) {
                GenerateDatabase(GenerateDatabaseOptions.SkipUnchanged);
                return _baseDb;
            }

            var paths = new List<string> { databasePath };
            if (includeSitePackages) {
                try {
                    paths.AddRange(Directory.EnumerateDirectories(databasePath));
                } catch (IOException) {
                } catch (UnauthorizedAccessException) {
                }
            }
            return new PythonTypeDatabase(this, paths, _baseDb);
        }

        public override void GenerateDatabase(GenerateDatabaseOptions options, Action<int> onExit = null) {
            if (!Directory.Exists(Configuration.LibraryPath)) {
                return;
            }

            // Create and mark the DB directory as hidden
            try {
                var dbDir = Directory.CreateDirectory(DatabasePath);
                dbDir.Attributes |= FileAttributes.Hidden;
            } catch (ArgumentException) {
            } catch (IOException) {
            } catch (UnauthorizedAccessException) {
            }

            var req = new PythonTypeDatabaseCreationRequest {
                Factory = this,
                OutputPath = DatabasePath,
                SkipUnchanged = options.HasFlag(GenerateDatabaseOptions.SkipUnchanged),
                DetectLibraryPath = !AssumeSimpleLibraryLayout
            };

            req.ExtraInputDatabases.Add(_base.DatabasePath);

            _baseHasRefreshed = false;

            if (_base.IsCurrent) {
                base.GenerateDatabase(req, onExit);
            } else {
                req.WaitFor = _base;
                req.SkipUnchanged = false;

                // Clear out the existing base database, since we're going to
                // need to reload it again. This also means that when
                // NewDatabaseAvailable is raised, we are expecting it and won't
                // incorrectly set _baseHasRefreshed to true again.
                _baseDb = null;

                // Start our analyzer first, since we will wait up to a minute
                // for our base analyzer to start (which may cause a one minute
                // delay if it completes before we start, but that is unlikely).
                base.GenerateDatabase(req, onExit);
                _base.GenerateDatabase(GenerateDatabaseOptions.SkipUnchanged);
            }
        }

        public override string DatabasePath {
            get {
                return Path.Combine(
                    Configuration.PrefixPath,
                    ".ptvs"
                );
            }
        }

        public override bool IsCurrent {
            get {
                return !_baseHasRefreshed && _base.IsCurrent && base.IsCurrent;
            }
        }

        public override bool IsCheckingDatabase {
            get {
                return base.IsCheckingDatabase || _base.IsCheckingDatabase;
            }
        }

        private void Base_IsCurrentChanged(object sender, EventArgs e) {
            base.OnIsCurrentChanged();
        }

        public override void RefreshIsCurrent() {
            if (_base == null) {
                // This rare race condition is due to a design flaw that is in
                // shipped public API and cannot be fixed without breaking
                // compatibility with 3rd parties.
                Volatile.Write(ref _deferRefreshIsCurrent, true);
                return;
            }
            _base.RefreshIsCurrent();
            base.RefreshIsCurrent();
        }

        public override string GetFriendlyIsCurrentReason(IFormatProvider culture) {
            if (_baseHasRefreshed) {
                return "Base interpreter has been refreshed";
            } else if (!_base.IsCurrent) {
                return string.Format(culture,
                    "Base interpreter {0} is out of date{1}{1}{2}",
                    _base.Description,
                    Environment.NewLine,
                    _base.GetFriendlyIsCurrentReason(culture));
            }
            return base.GetFriendlyIsCurrentReason(culture);
        }

        public override string GetIsCurrentReason(IFormatProvider culture) {
            if (_baseHasRefreshed) {
                return "Base interpreter has been refreshed";
            } else if (!_base.IsCurrent) {
                return string.Format(culture,
                    "Base interpreter {0} is out of date{1}{1}{2}",
                    _base.Description,
                    Environment.NewLine,
                    _base.GetIsCurrentReason(culture));
            }
            return base.GetIsCurrentReason(culture);
        }
    }
}
