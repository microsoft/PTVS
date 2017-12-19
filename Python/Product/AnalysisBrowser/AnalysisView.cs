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
                    version = new Version(3, 6);
                } else {
                    version = new Version(2, 7);
                }
            }

            _factory = Interpreter.LegacyDB.PythonInterpreterFactoryWithDatabase.CreateFromDatabase(
                version,
                new[] { dbDir }.Concat(paths).ToArray()
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
                            PrettyPrint(writer, mod, "", "  ", true);
                        }
                    }
                }
            });
        }

        static void PrettyPrint(
            TextWriter writer,
            IAnalysisItemView item,
            string currentIndent,
            string indent,
            bool tree
        ) {
            var stack = new Stack<IAnalysisItemView>();
            var exportStack = new Stack<IAnalysisItemView>();
            var seen = new HashSet<IAnalysisItemView>();
            stack.Push(item);

            while (stack.Any()) {
                var i = stack.Pop();
                if (i == null) {
                    currentIndent = currentIndent.Remove(0, indent.Length);
                    exportStack.Pop();
                    continue;
                }

                IEnumerable<IAnalysisItemView> exportChildren;
                if (tree) {
                    i.ExportToTree(writer, currentIndent, indent, out exportChildren);
                } else {
                    i.ExportToDiffable(writer, currentIndent, indent, exportStack, out exportChildren);
                }
                if (exportChildren != null && seen.Add(i)) {
                    stack.Push(null);
                    foreach (var child in exportChildren.Reverse()) {
                        stack.Push(child);
                    }
                    exportStack.Push(i);
                    currentIndent += indent;
                }
            }
        }

        public Task ExportDiffable(string filename, string filter) {
            return Task.Factory.StartNew(() => {
                var regex = new Regex(filter);
                using (var writer = new StreamWriter(filename, false, Encoding.UTF8)) {
                    foreach (var mod in _modules) {
                        if (regex.IsMatch(mod.Name)) {
                            PrettyPrint(writer, mod, "", "  ", false);
                        }
                    }
                }
            });
        }
    }
}
