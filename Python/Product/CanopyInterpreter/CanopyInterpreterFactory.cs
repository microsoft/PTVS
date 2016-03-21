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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text.RegularExpressions;
using Microsoft.PythonTools.Interpreter;

namespace CanopyInterpreter {
    /// <summary>
    /// Provides interpreter objects for a Canopy installation.
    /// 
    /// The factory is responsible for managing the cached analysis database,
    /// which for Canopy consists of two independent databases. When we create
    /// the User database, we include modules from the base (App) database as
    /// well.
    /// 
    /// Because we are using PythonTypeDatabase, we can use the default
    /// implementation of IPythonInterpreter.
    /// </summary>
    class CanopyInterpreterFactory : PythonInterpreterFactoryWithDatabase {
        private readonly PythonInterpreterFactoryWithDatabase _base;
        private PythonTypeDatabase _baseDb;
        private bool _baseHasRefreshed;

        /// <summary>
        /// Creates the factory for Canopy's base (App) interpreter. This
        /// factory is not displayed to the user, but is used to maintain the
        /// completion database.
        /// </summary>
        public static PythonInterpreterFactoryWithDatabase CreateBase(
            string basePath,
            string canopyVersion,
            Version languageVersion
        ) {
            var interpPath = FindFile(basePath, CanopyInterpreterFactoryConstants.ConsoleExecutable);
            var winInterpPath = FindFile(basePath, CanopyInterpreterFactoryConstants.WindowsExecutable);
            var libPath = Path.Combine(basePath, CanopyInterpreterFactoryConstants.LibrarySubPath);

            if (!File.Exists(interpPath)) {
                throw new FileNotFoundException(interpPath);
            }
            if (!File.Exists(winInterpPath)) {
                throw new FileNotFoundException(winInterpPath);
            }
            if (!Directory.Exists(libPath)) {
                throw new DirectoryNotFoundException(libPath);
            }

            // Detect the architecture and select the appropriate id
            var arch = NativeMethods.GetBinaryType(interpPath);
            var id = (arch == ProcessorArchitecture.Amd64) ?
                CanopyInterpreterFactoryConstants.BaseGuid64 :
                CanopyInterpreterFactoryConstants.BaseGuid32;

            // Make the description string look like "Base Canopy 1.1.0.46 (2.7 32-bit)"
            var description = "Base Canopy";
            if (!string.IsNullOrEmpty(canopyVersion)) {
                description += " " + canopyVersion;
            }
            description += string.Format(" ({0} ", languageVersion);
            if (arch == ProcessorArchitecture.Amd64) {
                description += " 64-bit)";
            } else {
                description += " 32-bit)";
            }

            return InterpreterFactoryCreator.CreateInterpreterFactory(
                new InterpreterConfiguration(
                    basePath,
                    description,
                    basePath,
                    interpPath,
                    winInterpPath,
                    libPath,
                    CanopyInterpreterFactoryConstants.PathEnvironmentVariableName,
                    arch,
                    languageVersion,
                    InterpreterUIMode.SupportsDatabase
                ),
                new InterpreterFactoryCreationOptions {
                    WatchLibraryForNewModules = true
                }
            );
        }

        /// <summary>
        /// Creates the Canopy User interpreter. This handles layering of the
        /// User database on top of the App database, and ensures that refreshes
        /// work correctly.
        /// 
        /// Because it is exposed as its own factory type, it can also be used
        /// as a base interpreter for DerivedInterpreterFactory (virtual
        /// environments).
        /// </summary>
        public static CanopyInterpreterFactory Create(
            PythonInterpreterFactoryWithDatabase baseFactory,
            string userPath,
            string canopyVersion
        ) {
            var interpPath = FindFile(userPath, CanopyInterpreterFactoryConstants.ConsoleExecutable);
            var winInterpPath = FindFile(userPath, CanopyInterpreterFactoryConstants.WindowsExecutable);
            var libPath = Path.Combine(userPath, CanopyInterpreterFactoryConstants.LibrarySubPath);

            if (!File.Exists(interpPath)) {
                throw new FileNotFoundException(interpPath);
            }
            if (!File.Exists(winInterpPath)) {
                throw new FileNotFoundException(winInterpPath);
            }
            if (!Directory.Exists(libPath)) {
                throw new DirectoryNotFoundException(libPath);
            }

            // Make the description string look like "Canopy 1.1.0.46 (2.7 32-bit)"
            var description = "Canopy ";
            if (!string.IsNullOrEmpty(canopyVersion)) {
                description += " " + canopyVersion;
            }
            description += string.Format(" ({0} ", baseFactory.Configuration.Version);
            if (baseFactory.Configuration.Architecture == ProcessorArchitecture.Amd64) {
                description += " 64-bit)";
            } else {
                description += " 32-bit)";
            }

            var config = new InterpreterConfiguration(
                userPath,
                description,
                userPath,
                interpPath,
                winInterpPath,
                libPath,
                CanopyInterpreterFactoryConstants.PathEnvironmentVariableName,
                baseFactory.Configuration.Architecture,
                baseFactory.Configuration.Version,
                InterpreterUIMode.SupportsDatabase
            );

            return new CanopyInterpreterFactory(description, baseFactory, config);
        }

        private CanopyInterpreterFactory(
            string description,
            PythonInterpreterFactoryWithDatabase baseFactory,
            InterpreterConfiguration config
        )
            : base(description, config, true) {
            if (baseFactory == null) {
                throw new ArgumentNullException("baseFactory");
            }

            _base = baseFactory;
            _base.IsCurrentChanged += OnIsCurrentChanged;
            _base.NewDatabaseAvailable += OnNewDatabaseAvailable;
        }

        protected override void Dispose(bool disposing) {
            _baseDb = null;
            _base.IsCurrentChanged -= OnIsCurrentChanged;
            _base.NewDatabaseAvailable -= OnNewDatabaseAvailable;

            base.Dispose(disposing);
        }

        private void OnIsCurrentChanged(object sender, EventArgs e) {
            // Raised if our base database's IsCurrent changes, which means that
            // ours may have as well.
            base.OnIsCurrentChanged();
        }

        private void OnNewDatabaseAvailable(object sender, EventArgs e) {
            // Raised if our base database is updated, which means we need to
            // refresh our database too.
            if (_baseDb != null) {
                _baseDb = null;
                _baseHasRefreshed = true;
            }
            OnNewDatabaseAvailable();
            OnIsCurrentChanged();
        }

        // We could override MakeInterpreter() to return a different
        // implementation of IPythonInterpreter, but we don't need to here, as
        // the default implementation works well with PythonTypeDatabase.

        //public override IPythonInterpreter MakeInterpreter(PythonInterpreterFactoryWithDatabase factory) {
        //    return base.MakeInterpreter(factory);
        //}

        /// <summary>
        /// Returns a new database that contains the database from our base
        /// interpreter.
        /// </summary>
        public override PythonTypeDatabase MakeTypeDatabase(string databasePath, bool includeSitePackages = true) {
            if (_baseDb == null && _base.IsCurrent) {
                _baseDb = _base.GetCurrentDatabase(ShouldIncludeGlobalSitePackages);
            }

            var paths = new List<string> { databasePath };
            if (includeSitePackages) {
                try {
                    paths.AddRange(Directory.EnumerateDirectories(databasePath));
                } catch (ArgumentException) {
                } catch (IOException) {
                } catch (SecurityException) {
                } catch (UnauthorizedAccessException) {
                }
            }
            return new PythonTypeDatabase(this, paths, _baseDb);
        }

        /// <summary>
        /// Regenerates the database for this environment. If the base
        /// interpreter needs regenerating, it will also be regenerated.
        /// </summary>
        public override void GenerateDatabase(GenerateDatabaseOptions options, Action<int> onExit = null) {
            if (!Directory.Exists(Configuration.LibraryPath)) {
                return;
            }

            var req = new PythonTypeDatabaseCreationRequest {
                Factory = this,
                OutputPath = DatabasePath,
                SkipUnchanged = options.HasFlag(GenerateDatabaseOptions.SkipUnchanged)
            };

            req.ExtraInputDatabases.Add(_base.DatabasePath);

            _baseHasRefreshed = false;

            if (_base.IsCurrent) {
                // The App database is already up to date, so start analyzing
                // the User database immediately.
                base.GenerateDatabase(req, onExit);
            } else {
                // The App database needs to be updated, so start both and wait
                // for the base to finish before analyzing User.

                // Specifying our base interpreter as 'WaitFor' allows the UI to
                // forward progress and status messages to the user, even though
                // the factory is not visible.
                req.WaitFor = _base;

                // Because the underlying analysis of the standard library has
                // changed, we must reanalyze the entire database.
                req.SkipUnchanged = false;

                // Clear out the existing base database, since we're going to
                // need to reload it again. This also means that when
                // NewDatabaseAvailable is raised, we are expecting it and won't
                // incorrectly set _baseHasRefreshed to true again.
                _baseDb = null;

                // Start our analyzer first, since we will wait up to a minute
                // for the base analyzer to start (which may cause a one minute
                // delay if it completes before we start, but that is unlikely).
                base.GenerateDatabase(req, onExit);
                _base.GenerateDatabase(GenerateDatabaseOptions.SkipUnchanged);
            }
        }

        public override bool IsCurrent {
            get {
                // The User database is only current if the App database is
                // current as well.
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
                    "Base interpreter is out of date:{0}{0}    {1}",
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
                    "{0} is out of date:{1}{2}",
                    _base.Configuration.Description,
                    Environment.NewLine,
                    _base.GetIsCurrentReason(culture));
            }
            return base.GetIsCurrentReason(culture);
        }


        private bool ShouldIncludeGlobalSitePackages {
            get {
                // Canopy by default includes global site-packages, but we
                // should check in case a user modifies this manually.
                var cfgFile = Path.Combine(Configuration.PrefixPath, "pyvenv.cfg");
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
                    } catch (SecurityException) {
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Recursively searches for a file using breadth-first-search. This
        /// ensures that the result closest to <paramref name="root"/> is
        /// returned first.
        /// </summary>
        /// <param name="root">
        /// Directory to start searching.
        /// </param>
        /// <param name="file">
        /// Filename to find. Wildcards are not supported.
        /// </param>
        /// <param name="depthLimit">
        /// The number of subdirectories to search in.
        /// </param>
        /// <returns>
        /// The full path to the file if found; otherwise, null.
        /// </returns>
        private static string FindFile(string root, string file, int depthLimit = 2) {
            var candidate = Path.Combine(root, file);
            if (File.Exists(candidate)) {
                return candidate;
            }
            // In our context, the file we a searching for is often in a
            // Scripts subdirectory, so prioritize that directory.
            candidate = Path.Combine(root, "Scripts", file);
            if (File.Exists(candidate)) {
                return candidate;
            }

            // Do a BFS of the filesystem to ensure we find the match closest to
            // the root directory.
            var dirQueue = new Queue<string>();
            dirQueue.Enqueue(root);
            dirQueue.Enqueue("<EOD>");
            while (dirQueue.Any()) {
                var dir = dirQueue.Dequeue();
                if (dir == "<EOD>") {
                    depthLimit -= 1;
                    if (depthLimit <= 0) {
                        return null;
                    }
                    continue;
                }
                var result = Directory.EnumerateFiles(dir, file, SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (result != null) {
                    return result;
                }
                foreach (var subDir in Directory.EnumerateDirectories(dir)) {
                    dirQueue.Enqueue(subDir);
                }
                dirQueue.Enqueue("<EOD>");
            }
            return null;
        }
    }
}
