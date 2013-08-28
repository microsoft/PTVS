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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.PythonTools.Interpreter;
using IOPath = System.IO.Path;

namespace Microsoft.PythonTools.Analysis.Browser {
    class AnalysisView {
        public readonly static ICommand ExportTreeCommand = 
            new RoutedCommand("ExportTree", typeof(AnalysisView));
        public readonly static ICommand ExportDiffableCommand = 
            new RoutedCommand("ExportDiffable", typeof(AnalysisView));

        readonly IPythonInterpreterFactory _factory;
        readonly IPythonInterpreter _interpreter;
        readonly IModuleContext _context;
        readonly List<IAnalysisItemView> _modules;
        
        public AnalysisView(
            string dbDir,
            Version version = null,
            bool withContention = false,
            bool withRecursion = false
        ) {
            var paths = new List<string>();
            paths.Add(dbDir);
            while (!File.Exists(IOPath.Combine(paths[0], "__builtin__.idb")) &&
                !File.Exists(IOPath.Combine(paths[0], "builtins.idb"))) {
                var upOne = IOPath.GetDirectoryName(paths[0]);
                if (string.IsNullOrEmpty(upOne) || upOne == paths[0]) {
                    break;
                }
                paths.Insert(0, upOne);
            }

            if (withRecursion) {
                paths.AddRange(Directory.EnumerateDirectories(dbDir, "*", SearchOption.AllDirectories));
            }

            if (version == null) {
                if (File.Exists(IOPath.Combine(paths[0], "builtins.idb"))) {
                    version = new Version(3, 3);
                } else {
                    version = new Version(2, 7);
                }
            }

            _factory = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(
                version,
                dbDir,
                paths.ToArray()
            );
            Path = dbDir;
            _interpreter = _factory.CreateInterpreter();
            _context = _interpreter.CreateModuleContext();

            var modNames = _interpreter.GetModuleNames();
            IEnumerable<Tuple<string, string>> modItems;

            if (!withRecursion) {
                modItems = modNames
                    .Select(n => Tuple.Create(n, IOPath.Combine(dbDir, n + ".idb")))
                    .Where(t => File.Exists(t.Item2));
            } else {
                modItems = modNames
                    .Select(n => Tuple.Create(
                        n,
                        Directory.EnumerateFiles(dbDir, n + ".idb", SearchOption.AllDirectories).FirstOrDefault()
                    ))
                    .Where(t => File.Exists(t.Item2));
            }

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            if (withContention) {
                modItems = modItems
                    .AsParallel()
                    .WithExecutionMode(ParallelExecutionMode.ForceParallelism);
            }
            _modules = modItems
                .Select(t => new ModuleView(_interpreter, _context, t.Item1, t.Item2))
                .OrderBy(m => m.SortKey)
                .ThenBy(m => m.Name)
                .ToList<IAnalysisItemView>();
            stopwatch.Stop();

            _modules.Insert(0, new KnownTypesView(_interpreter, version));

            LoadMilliseconds = stopwatch.ElapsedMilliseconds;
            TopLevelModuleCount = _modules.Count - 1;
        }

        public IEnumerable<IAnalysisItemView> Modules {
            get { return _modules; }
        }

        public string Path { get; private set; }

        public long LoadMilliseconds { get; private set; }
        public int TopLevelModuleCount { get; private set; }

        public Task ExportTree(string filename, string filter) {
            return Task.Factory.StartNew(() => {
                var regex = new Regex(filter);
                using (var writer = new StreamWriter(filename, false, Encoding.UTF8)) {
                    foreach (var mod in _modules) {
                        if (regex.IsMatch(mod.Name)) {
                            PrettyPrint(writer, mod, "", "  ");
                        }
                    }
                }
            });
        }

        static void PrettyPrint(TextWriter writer, IAnalysisItemView item, string currentIndent, string indent) {
            var stack = new Stack<IAnalysisItemView>();
            var seen = new HashSet<IAnalysisItemView>();
            stack.Push(item);

            while (stack.Any()) {
                var i = stack.Pop();
                if (i == null) {
                    currentIndent = currentIndent.Remove(0, indent.Length);
                    continue;
                }
                
                IEnumerable<IAnalysisItemView> exportChildren;
                i.ExportToTree(writer, currentIndent, indent, out exportChildren);
                if (exportChildren != null && seen.Add(i)) {
                    stack.Push(null);
                    foreach (var child in exportChildren.Reverse()) {
                        stack.Push(child);
                    }
                    currentIndent += indent;
                }
            }
        }

        public Task ExportDiffable(string filename, string filter) {
            return Task.Factory.StartNew(() => {
            });
        }
    }
}
