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
using System.Reflection;
using System.Threading;
using Microsoft.Win32;

namespace Microsoft.PythonTools.Interpreter.Default {
    class CPythonInterpreterFactory : IPythonInterpreterFactory, IInterpreterWithCompletionDatabase {
        private readonly string _description;
        private readonly Guid _id;
        private readonly InterpreterConfiguration _config;
        private readonly HashSet<WeakReference> _interpreters = new HashSet<WeakReference>();
        private TypeDatabase _typeDb;
        private bool _generating;

        public CPythonInterpreterFactory(Version version, Guid id, string description, string pythonPath, string pythonwPath, string pathEnvVar, ProcessorArchitecture arch) {
            if (version == default(Version)) {
                version = new Version(2, 7);
            }
            _description = description;
            _id = id;
            _config = new CPythonInterpreterConfiguration(pythonPath, pythonwPath, pathEnvVar, arch, version);
        }

        public InterpreterConfiguration Configuration {
            get {
                return _config;
            }
        }

        public string Description {
            get { return _description; }
        }

        public Guid Id {
            get { return _id; }
        }

        public IPythonInterpreter CreateInterpreter() {
            lock (_interpreters) {
                if (_typeDb == null) {
                    _typeDb = MakeTypeDatabase();
                } else if (_typeDb.DatabaseDirectory != GetConfiguredDatabasePath() && ConfigurableDatabaseExists()) {
                    // database has been generated for this interpreter, switch to the specific version.
                    _typeDb = new TypeDatabase(GetConfiguredDatabasePath(), Is3x);
                }

                var res = new CPythonInterpreter(_typeDb);

                if (!ConfigurableDatabaseExists()) {
                    _interpreters.Add(new WeakReference(res));
                }
                return res;
            }
        }

        internal TypeDatabase MakeTypeDatabase() {
            if (ConfigurableDatabaseExists()) {
                return new TypeDatabase(GetConfiguredDatabasePath(), Is3x);
            }
            return MakeDefaultTypeDatabase();
        }

        private bool ConfigurableDatabaseExists() {
            return File.Exists(Path.Combine(GetConfiguredDatabasePath(), Is3x ? "builtins.idb" : "__builtin__.idb"));
        }

        internal static TypeDatabase MakeDefaultTypeDatabase() {
            return new TypeDatabase(GetBaselineDatabasePath());
        }

        private string GetConfiguredDatabasePath() {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                String.Format("Python Tools\\CompletionDB\\{0}\\{1}", Id, Configuration.Version)
            );
        }
        
        internal static string GetBaselineDatabasePath() {
            return Path.Combine(GetPythonToolsInstallPath(), "CompletionDB");
        }

        bool IInterpreterWithCompletionDatabase.GenerateCompletionDatabase(GenerateDatabaseOptions options, Action databaseGenerationCompleted) {
            return GenerateCompletionDatabaseWorker(options, databaseGenerationCompleted);
        }

        private bool GenerateCompletionDatabaseWorker(GenerateDatabaseOptions options, Action databaseGenerationCompleted) {
            if (String.IsNullOrEmpty(Configuration.InterpreterPath)) {
                return false;
            }

            string outPath = GetConfiguredDatabasePath();

            if (!Directory.Exists(outPath)) {
                Directory.CreateDirectory(outPath);
            }

            var psi = new ProcessStartInfo();
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            psi.FileName = Configuration.InterpreterPath;
            psi.Arguments =
                "\"" + Path.Combine(GetPythonToolsInstallPath(), "PythonScraper.py") + "\"" +       // script to run
                " \"" + outPath + "\"" +                                                // output dir
                " \"" + GetBaselineDatabasePath() + "\"";           // baseline file

            var proc = new Process();
            proc.StartInfo = psi;
            proc.Start();
            proc.WaitForExit();

            if (proc.ExitCode == 0 && (options & GenerateDatabaseOptions.StdLibDatabase) != 0) {
                Thread t = new Thread(x => {
                    psi = new ProcessStartInfo();
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                    psi.FileName = Path.Combine(GetPythonToolsInstallPath(), "Microsoft.PythonTools.Analyzer.exe");
                    if (File.Exists(psi.FileName)) {
                        psi.Arguments = "/dir " + "\"" + Path.Combine(Path.GetDirectoryName(Configuration.InterpreterPath), "Lib") + "\"" +
                            " /version V" + this.Configuration.Version.ToString().Replace(".", "") +
                            " /outdir " + "\"" + outPath + "\"" +
                            " /indir " + "\"" + outPath + "\"";

                        proc = new Process();
                        proc.StartInfo = psi;

                        proc.Start();
                        proc.WaitForExit();

                        if (proc.ExitCode == 0) {
                            lock (_interpreters) {
                                _typeDb = new TypeDatabase(outPath, Is3x);
                                OnNewDatabaseAvailable();
                            }
                        }

                        databaseGenerationCompleted();
                    }
                });
                t.Start();
                return true;
            } else if (proc.ExitCode == 0) {
                databaseGenerationCompleted();
                lock (_interpreters) {
                    _typeDb = new TypeDatabase(outPath, Is3x);

                    OnNewDatabaseAvailable();
                }
            }
            return false;
        }

        private bool Is3x {
            get {
                return Configuration.Version.Major == 3;
            }
        }

        private void OnNewDatabaseAvailable() {
            foreach (var interpreter in _interpreters) {
                var curInterpreter = interpreter.Target as CPythonInterpreter;
                if (curInterpreter != null) {
                    curInterpreter.TypeDb = _typeDb;
                }
            }
            _interpreters.Clear();
        }

        void IInterpreterWithCompletionDatabase.AutoGenerateCompletionDatabase() {
            if (!ConfigurableDatabaseExists() && !_generating) {
                _generating = true;
                ThreadPool.QueueUserWorkItem(x => GenerateCompletionDatabaseWorker(GenerateDatabaseOptions.StdLibDatabase, () => { }));
            }
        }

        // This is duplicated throughout different assemblies in PythonTools, so search for it if you update it.
        private static string GetPythonToolsInstallPath() {
            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (File.Exists(Path.Combine(path, "Microsoft.PythonTools.dll"))) {
                return path;
            }

            // running from the GAC in remote attach scenario.  Look to the VS install dir.
            using (var configKey = OpenVisualStudioKey()) {
                var installDir = configKey.GetValue("InstallDir") as string;
                if (installDir != null) {
                    var toolsPath = Path.Combine(installDir, "Extensions\\Microsoft\\Python Tools for Visual Studio\\1.0");
                    if (File.Exists(Path.Combine(toolsPath, "Microsoft.PythonTools.dll"))) {
                        return toolsPath;
                    }
                }
            }

            return null;
        }

        private static Win32.RegistryKey OpenVisualStudioKey() {
            if (Environment.Is64BitOperatingSystem) {
                return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey("Software\\Microsoft\\VisualStudio\\10.0");
            } else {
                return Microsoft.Win32.Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\VisualStudio\\10.0");
            }
        }

    }
}
