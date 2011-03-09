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
using System.IO;
using System.Reflection;
using System.Threading;
using Microsoft.PythonTools.Interpreter.Default;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis {
    class PyLibAnalyzer {
        private readonly string[] _dirs;
        private readonly string _indir;
        private readonly PythonLanguageVersion _version;

        private PyLibAnalyzer(List<string> dirs, string indir, PythonLanguageVersion version) {
            _dirs = dirs.ToArray();
            _indir = indir;
            _version = version;
        }

        public static int Main(string[] args) {
            List<string> dirs = new List<string>();
            PythonLanguageVersion version = PythonLanguageVersion.V27;
            string outdir = Environment.CurrentDirectory;
            string indir = Assembly.GetExecutingAssembly().Location;
            for (int i = 0; i < args.Length; i++) {
                if (args[i].StartsWith("/") || args[i].StartsWith("-")) {
                    switch (args[i].Substring(1).ToLower()) {
                        case "dir":
                            if (i == args.Length - 1) {
                                Help();
                                Console.WriteLine("Missing directory after {0}", args[i]);
                                return -1;
                            }
                            dirs.Add(args[i + 1]);
                            i++;
                            break;
                        case "version":
                            if (i == args.Length - 1) {
                                Help();
                                Console.WriteLine("Missing version after {0}", args[i]);
                                return -1;
                            }
                            if (!Enum.TryParse<PythonLanguageVersion>(args[i + 1], true, out version)) {
                                Help();
                                Console.WriteLine("Bad version specified: {0}", args[i + 1]);
                                return -1;
                            }
                            i++;
                            break;
                        case "outdir":
                            if (i == args.Length - 1) {
                                Help();
                                Console.WriteLine("Missing output dir after {0}", args[i]);
                                return -1;
                            }
                            outdir = args[i + 1];
                            i++;
                            break;
                        case "indir":
                            if (i == args.Length - 1) {
                                Help();
                                Console.WriteLine("Missing input dir after {0}", args[i]);
                                return -1;
                            }
                            indir = args[i + 1];
                            i++;
                            break;
                    }
                }
            }

            if (dirs.Count == 0) {
                Help();
                return -2;
            }

            Thread.CurrentThread.Priority = System.Threading.ThreadPriority.Lowest;
            try {
                new PyLibAnalyzer(dirs, indir, version).AnalyzeStdLib(outdir);
            } catch (Exception e) {
                Console.WriteLine("Error while saving analysis: {0}", e.ToString());
                return -3;
            }

            return 0;
        }

        private static void Help() {
            Console.WriteLine("Python Library Analyzer");
            Console.WriteLine();
            Console.WriteLine(" /dir     [directory]        - analyze provided directory (multiple directories can be provided)");
            Console.WriteLine(" /version [version]          - specify language version to be used ({0})", String.Join(", ", Enum.GetNames(typeof(PythonLanguageVersion))));
            Console.WriteLine(" /outdir  [output dir]       - specify output directory for analysis (default is current dir)");
            Console.WriteLine(" /indir   [input dir]        - specify input directory for baseline analysis");
        }

        private void AnalyzeStdLib(string outdir) {
            var allFiles = new List<List<string>>();
            foreach (var dir in _dirs) {
                CollectFiles(dir, allFiles);
            }

            foreach (var files in allFiles) {
                if (files.Count > 0) {
                    Console.WriteLine("Now analyzing: {0}", Path.GetDirectoryName(files[0]));
                    var projectState = new PythonAnalyzer(new CPythonInterpreter(new TypeDatabase(_indir, _version.Is3x())), _version);
                    var modules = new List<IPythonProjectEntry>();
                    for (int i = 0; i < files.Count; i++) {
                        string modName = PythonAnalyzer.PathToModuleName(files[i]);

                        modules.Add(projectState.AddModule(modName, files[i]));
                    }

                    var nodes = new List<PythonAst>();
                    for (int i = 0; i < modules.Count; i++) {
                        PythonAst ast = null;
                        try {
                            var sourceUnit = new StreamReader(files[i]);

                            ast = Parser.CreateParser(sourceUnit, Microsoft.PythonTools.Parsing.ErrorSink.Null, PythonLanguageVersion.V27).ParseFile();
                        } catch (Exception) {
                        }
                        nodes.Add(ast);
                    }

                    for (int i = 0; i < modules.Count; i++) {
                        var ast = nodes[i];

                        if (ast != null) {
                            modules[i].UpdateTree(ast, null);
                        }
                    }

                    for (int i = 0; i < modules.Count; i++) {
                        var ast = nodes[i];
                        if (ast != null) {
                            modules[i].Analyze(true);
                        }
                    }

                    if (modules.Count > 0) {
                        modules[0].AnalysisGroup.AnalyzeQueuedEntries();
                    }

                    new SaveAnalysis().Save(projectState, outdir);
                }
            }
        }

        private static void CollectFiles(string dir, List<List<string>> files) {
            List<string> libFiles = new List<string>();
            files.Add(libFiles);

            foreach (var subdir in Directory.GetDirectories(dir)) {
                if (Path.GetFileName(subdir) == "site-packages") {
                    // site packages can be big, analyze each package alone
                    foreach (var sitePackageDir in Directory.GetDirectories(subdir)) {
                        var list = new List<string>();
                        files.Add(list);
                        CollectFilesWorker(sitePackageDir, list);
                    }
                } else {
                    CollectFilesWorker(subdir, libFiles);
                }
            }

            foreach(var file in Directory.GetFiles(dir)) {
                if (file.EndsWith(".py", StringComparison.OrdinalIgnoreCase)) {
                    libFiles.Add(file);
                }
            }
        }

        private static void CollectFilesWorker(string dir, List<string> files) {
            foreach (string file in Directory.GetFiles(dir)) {
                if (file.EndsWith(".py", StringComparison.OrdinalIgnoreCase)) {
                    files.Add(file);
                }
            }

            foreach (string nestedDir in Directory.GetDirectories(dir)) {
                CollectFilesWorker(nestedDir, files);
            }
        }

    }
}
