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
using Microsoft.PythonTools.Interpreter;
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
                using (var writer = new StreamWriter(File.Open(Path.Combine(outdir, "AnalysisLog.txt"), FileMode.Create, FileAccess.ReadWrite, FileShare.Read))) {
                    try {
                        new PyLibAnalyzer(dirs, indir, version).AnalyzeStdLib(writer, outdir);
                    } catch (Exception e) {
                        Console.WriteLine("Error while saving analysis: {0}", e.ToString());
                        Log(writer, "ANALYSIS FAIL: \"" + e.ToString().Replace("\r\n", " -- ") + "\"");
                        return -3;
                    }
                }
            } catch (IOException) {
                // another process is already analyzing this project.  This happens when we have 2 VSs started at the same time w/o the
                // database being created yet.  Wait for that process to finish and then we can exit ourselves and both VS's will pick 
                // up the new analysis.

                while (true) {
                    try {
                        using (var writer = new StreamWriter(File.Open(Path.Combine(outdir, "AnalysisLog.txt"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))) {
                            break;
                        }
                    } catch (IOException) {
                        Thread.Sleep(20000);
                    }
                }
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

        private void AnalyzeStdLib(StreamWriter writer, string outdir) {
            
            var fileGroups = new List<List<string>>();
            HashSet<string> pthDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> allFileSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dir in _dirs) {
                CollectFiles(pthDirs, dir, fileGroups, allFileSet);
            }

            HashSet<string> allPthDirs = new HashSet<string>();
            while (pthDirs.Count > 0) {
                allPthDirs.UnionWith(pthDirs);

                pthDirs.Clear();
                foreach (var dir in pthDirs) {
                    CollectFiles(pthDirs, dir, fileGroups, allFileSet);
                }

                pthDirs.ExceptWith(allPthDirs);
            }

            foreach (var files in fileGroups) {
                if (files.Count > 0) {
                    Log(writer, "GROUP START \"" + Path.GetDirectoryName(files[0]) + "\"");
                    Console.WriteLine("Now analyzing: {0}", Path.GetDirectoryName(files[0]));
                    var projectState = new PythonAnalyzer(new CPythonInterpreter(new PythonTypeDatabase(_indir, _version.Is3x())), _version);
                    var modules = new List<IPythonProjectEntry>();
                    for (int i = 0; i < files.Count; i++) {
                        string modName = PythonAnalyzer.PathToModuleName(files[i]);

                        modules.Add(projectState.AddModule(modName, files[i]));
                    }

                    var nodes = new List<PythonAst>();
                    for (int i = 0; i < modules.Count; i++) {
                        PythonAst ast = null;
                        try {
                            var sourceUnit = new FileStream(files[i], FileMode.Open, FileAccess.Read, FileShare.Read);

                            Log(writer, "PARSE START: \"" + modules[i].FilePath + "\"");
                            ast = Parser.CreateParser(sourceUnit, PythonLanguageVersion.V27, new ParserOptions() { BindReferences = true }).ParseFile();
                            Log(writer, "PARSE END: \"" + modules[i].FilePath + "\"");
                        } catch (Exception ex) {
                            Log(writer, "PARSE ERROR: \"" + modules[i].FilePath + "\" \"" + ex.ToString().Replace("\r\n", " -- ") + "\"");
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
                            Log(writer, "ANALYSIS START: \"" + modules[i].FilePath + "\"");
                            modules[i].Analyze(true);
                            Log(writer, "ANALYSIS END: \"" + modules[i].FilePath + "\"");
                        }
                    }

                    if (modules.Count > 0) {
                        modules[0].AnalysisGroup.AnalyzeQueuedEntries();
                    }

                    Log(writer, "SAVING GROUP: \"" + Path.GetDirectoryName(files[0]) + "\"");
                    new SaveAnalysis().Save(projectState, outdir);
                    Log(writer, "GROUP END \"" + Path.GetDirectoryName(files[0]) + "\"");
                }
            }
        }

        private static void Log(StreamWriter writer, string contents) {
            writer.WriteLine(
                "\"{0}\" {1}",
                DateTime.Now.ToString("yyyy/MM/dd h:mm:ss.fff tt"),
                contents
            );
            writer.Flush();
        }


        private static void CollectFiles(HashSet<string> pthDirs, string dir, List<List<string>> files, HashSet<string> allFiles) {
            List<string> libFiles = new List<string>();
            files.Add(libFiles);

            foreach (var subdir in Directory.GetDirectories(dir)) {
                if (Path.GetFileName(subdir) == "site-packages") {
                    // site packages can be big, analyze each package alone
                    foreach (var sitePackageDir in Directory.GetDirectories(subdir)) {
                        var list = new List<string>();
                        files.Add(list);
                        CollectPackage(sitePackageDir, list, allFiles);
                    }
                } else {
                    CollectPackage(subdir, libFiles, allFiles);
                }
            }

            foreach(var file in Directory.GetFiles(dir)) {
                if (IsPythonFile(file)) {
                    libFiles.Add(file);
                } else if (IsPthFile(file)) {
                    string[] lines = File.ReadAllLines(file);
                    foreach (var line in lines) {
                        if (line.IndexOfAny(_invalidPathChars) == -1) {
                            string pthDir = line;
                            if (!Path.IsPathRooted(line)) {
                                pthDir = Path.Combine(dir, line);
                            }

                            if (Directory.Exists(pthDir)) {
                                pthDirs.Add(pthDir);
                            }
                        }
                    }
                }
            }
        }

        private static char[] _invalidPathChars = Path.GetInvalidPathChars();

        /// <summary>
        /// Collects a package and all sub-packages
        /// </summary>
        private static void CollectPackage(string dir, List<string> files, HashSet<string> allFiles) {
            foreach (string file in Directory.GetFiles(dir)) {
                if (IsPythonFile(file) && !allFiles.Contains(file)) { 
                    files.Add(file);
                }
            }

            foreach (string nestedDir in Directory.GetDirectories(dir)) {
                // only include packages and subpackages, don't look in random dirs (it's CollectFiles
                // responsibility to know dirs to look in that aren't packages)
                if (File.Exists(Path.Combine(nestedDir, "__init__.py"))) {
                    CollectPackage(nestedDir, files, allFiles);
                }
            }
        }
        
        private static bool IsPythonFile(string file) {
            string filename = Path.GetFileName(file);
            return filename.Length > 0 && (Char.IsLetter(filename[0]) || filename[0] == '_') && // distros include things like '1col.py' in tests which aren't valid module names, ignore those.
                file.EndsWith(".py", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".pyw", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPthFile(string file) {
            return String.Compare(Path.GetExtension(file), ".pth", StringComparison.OrdinalIgnoreCase) == 0;
        }
    }
}
