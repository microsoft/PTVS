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
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Interpreters {
    class DerivedInterpreterFactory : PythonInterpreterFactoryWithDatabase {
        readonly PythonInterpreterFactoryWithDatabase _base;

        string _description;

        PythonTypeDatabase _baseDb;
        bool _baseHasRefreshed;

        public DerivedInterpreterFactory(PythonInterpreterFactoryWithDatabase baseFactory,
                                         InterpreterFactoryCreationOptions options)
            : base(options.Id,
                   options.Description,
                   new InterpreterConfiguration(options.PrefixPath,
                                                options.InterpreterPath,
                                                options.WindowInterpreterPath,
                                                options.LibraryPath,
                                                options.PathEnvironmentVariableName,
                                                options.Architecture,
                                                options.LanguageVersion),
                   options.WatchLibraryForNewModules) {

            if (baseFactory.Configuration.Version != options.LanguageVersion) {
                throw new ArgumentException("Language versions do not match", "options");
            }

            _base = baseFactory;
            _base.IsCurrentChanged += OnIsCurrentChanged;
            _base.IsCurrentReasonChanged += OnIsCurrentReasonChanged;

            _description = options.Description;
        }

        protected override void Dispose(bool disposing) {
            if (_baseDb != null) {
                _baseDb.DatabaseCorrupt -= Base_DatabaseCorrupt;
                _baseDb.DatabaseReplaced -= Base_DatabaseReplaced;
            }
            _base.IsCurrentChanged -= OnIsCurrentChanged;
            _base.IsCurrentReasonChanged -= OnIsCurrentReasonChanged;

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

        private void OnIsCurrentChanged(object sender, EventArgs e) {
            base.OnIsCurrentChanged();
        }

        private void OnIsCurrentReasonChanged(object sender, EventArgs e) {
            base.OnIsCurrentReasonChanged();
        }

        private void Base_DatabaseReplaced(object sender, DatabaseReplacedEventArgs e) {
            if (_baseDb == e.NewDatabase) {
                return;
            }

            if (_baseDb != null) {
                _baseDb.DatabaseCorrupt -= Base_DatabaseCorrupt;
                _baseDb.DatabaseReplaced -= Base_DatabaseReplaced;
                _baseHasRefreshed = true;
            }
            _baseDb = e.NewDatabase;
            if (_baseDb != null) {
                _baseDb.DatabaseCorrupt += Base_DatabaseCorrupt;
                _baseDb.DatabaseReplaced += Base_DatabaseReplaced;

                if (_baseHasRefreshed) {
                    if (IsCurrentDatabaseInUse) {
                        // We have interpreter instances using our database, so
                        // refresh it immediately.
                        GenerateCompletionDatabase(GenerateDatabaseOptions.None, exitCode => {
                            // When we finish, refresh our status.
                            _baseHasRefreshed = (exitCode != 0);
                            OnIsCurrentChanged(this, EventArgs.Empty);
                            OnIsCurrentReasonChanged(this, EventArgs.Empty);
                        });
                    } else {
                        // Otherwise, update our IsCurrent status.
                        OnIsCurrentChanged(this, EventArgs.Empty);
                        OnIsCurrentReasonChanged(this, EventArgs.Empty);
                    }
                }
            }
        }

        private void Base_DatabaseCorrupt(object sender, EventArgs e) {
            NotifyInvalidDatabase();
        }

        public override IPythonInterpreter MakeInterpreter(PythonTypeDatabase typeDb) {
            return _base.MakeInterpreter(typeDb);
        }

        private static bool ShouldIncludeGlobalSitePackages(string prefixPath, string libPath) {
            var cfgFile = Path.Combine(prefixPath, "pyvenv.cfg");
            if (File.Exists(cfgFile)) {
                try {
                    var lines = File.ReadAllLines(cfgFile);
                    return lines
                        .Select(line => Regex.Match(
                            line,
                            "^include-system-site-packages\\s*=\\s*true",
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
            if (_baseDb == null) {
                var includeBaseSitePackages = ShouldIncludeGlobalSitePackages(
                    Configuration.PrefixPath,
                    Configuration.LibraryPath
                );

                _baseDb = _base.MakeTypeDatabase(_base.DatabasePath, includeSitePackages: includeBaseSitePackages);
                _baseDb.DatabaseCorrupt += Base_DatabaseCorrupt;
                _baseDb.DatabaseReplaced += Base_DatabaseReplaced;
            }

            var db = _baseDb.Clone(this);
            try {
                db.LoadDatabase(databasePath);
                if (includeSitePackages) {
                    foreach (var dir in Directory.EnumerateDirectories(databasePath)) {
                        db.LoadDatabase(dir);
                    }
                }
            } catch (IOException) {
            } catch (UnauthorizedAccessException) {
            }
            return db;
        }

        public override void GenerateCompletionDatabase(GenerateDatabaseOptions options, Action<int> onExit = null) {
            if (IsGenerating) {
                return;
            }

            if (!Directory.Exists(Configuration.LibraryPath)) {
                return;
            }

            if (!_base.IsCurrent) {
                _base.GenerateCompletionDatabase(GenerateDatabaseOptions.SkipUnchanged, onExit);
                return;
            }

            // Create and mark the DB directory as hidden
            try {
                var dbDir = Directory.CreateDirectory(BaseDatabasePath);
                dbDir.Attributes |= FileAttributes.Hidden;
            } catch (IOException) {
            } catch (UnauthorizedAccessException) {
            }

            NotifyGeneratingDatabase(true);
            var req = new PythonTypeDatabaseCreationRequest {
                Factory = this,
                OutputPath = DatabasePath,
                SkipUnchanged = options.HasFlag(GenerateDatabaseOptions.SkipUnchanged)
            };

            req.ExtraInputDatabases.Add(_base.DatabasePath);
            req.OnExit = onExit;

            PythonTypeDatabase.Generate(req);
        }

        private string BaseDatabasePath {
            get {
                return Path.Combine(
                    Configuration.PrefixPath,
                    ".ptvs"
                );
            }
        }

        public override string DatabasePath {
            get {
                return Path.Combine(
                    BaseDatabasePath,
                    Id.ToString(),
                    Configuration.Version.ToString()
                );
            }
        }

        public override void AutoGenerateCompletionDatabase() {
            _base.AutoGenerateCompletionDatabase();
            if (_base.IsCurrent) {
                base.AutoGenerateCompletionDatabase();
            }
        }

        public override bool IsCurrent {
            get {
                return !_baseHasRefreshed && _base.IsCurrent && base.IsCurrent;
            }
        }

        public override void RefreshIsCurrent() {
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
