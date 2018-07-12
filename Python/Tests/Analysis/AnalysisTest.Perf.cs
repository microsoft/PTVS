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
using System.Text;
using System.Threading;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using TestUtilities;

namespace AnalysisTests {
    public class FileStreamReader : StreamReader {
        public readonly string Path;

        public FileStreamReader(string filename)
            : base(filename) {
            Path = filename;
        }
    }

    public partial class AnalysisTest : BaseAnalysisTest {
        [PerfMethod]
        public void TestLookupPerf_Namespaces() {
            var state = ProcessText(@"
import System
            ");
            var entry = state.Modules[state.DefaultModule].Analysis;

            Stopwatch sw = new Stopwatch();
            sw.Start();
            for (int i = 0; i < 1000; i++) {
                foreach (var varRef in entry.GetMembersByIndex("System", 1)) {
                    foreach (var innerRef in entry.GetMembersByIndex("System." + varRef.Name, 1)) {
                    }
                }
            }
            sw.Stop();
            Console.WriteLine("{0} ms", sw.ElapsedMilliseconds);
        }

        [PerfMethod]
        public void TestParsePerf_Decimal() {
            string merlin = Environment.GetEnvironmentVariable("DLR_ROOT") ?? @"C:\Product\0\dlr";
            var text = File.ReadAllText(Path.Combine(merlin + @"\External.LCA_RESTRICTED\Languages\IronPython\27\Lib\decimal.py"));

            var state = CreateAnalyzer();
            var entry = state.AddModule("decimal", text);
            Stopwatch sw = new Stopwatch();

            sw.Start();
            for (int i = 0; i < 5; i++) {
                state.UpdateModule(entry, text);
                state.WaitForAnalysis();
            }

            sw.Stop();
            Console.WriteLine("{0}", sw.ElapsedMilliseconds);
        }

        [PerfMethod]
        public void TestLookupPerf_Modules_Class() {
            var entry = ProcessText(@"
import System
            ");

            Stopwatch sw = new Stopwatch();
            sw.Start();
#if IPY
            foreach (var result in entry.ProjectState.GetModuleMembers(new[] { "System", "IO", "BinaryReader" })) {
            }
            foreach (var result in entry.ProjectState.GetModuleMembers(new[] { "System", "IO", "BinaryWriter" })) {
            }
            foreach (var result in entry.ProjectState.GetModuleMembers(new[] { "System", "IO", "BufferedStream" })) {
            }
            foreach (var result in entry.ProjectState.GetModuleMembers(new[] { "System", "IO", "Stream" })) {
            }
            foreach (var result in entry.ProjectState.GetModuleMembers(new[] { "System", "IO", "Directory" })) {
            }
            foreach (var result in entry.ProjectState.GetModuleMembers(new[] { "System", "IO", "File" })) {
            }
            foreach (var result in entry.ProjectState.GetModuleMembers(new[] { "System", "IO", "FileStream" })) {
            }
#endif
            sw.Stop();
            Console.WriteLine(sw.ElapsedMilliseconds);
        }

        [PerfMethod]
        public void TestLookupPerf_Namespaces2() {
            var state = ProcessText(@"
import System
            ");
            var entry = state.Modules[state.DefaultModule].Analysis;

            Stopwatch sw = new Stopwatch();
            sw.Start();
            foreach (var varRef in entry.GetMembersByIndex("System", 1)) {
            }
            foreach (var varRef in entry.GetMembersByIndex("System.Collections", 1)) {
            }
            foreach (var varRef in entry.GetMembersByIndex("System.Collections.Generic", 1)) {
            }
            foreach (var varRef in entry.GetMembersByIndex("System.CodeDom", 1)) {
            }
            foreach (var varRef in entry.GetMembersByIndex("System.Configuration", 1)) {
            }
            foreach (var varRef in entry.GetMembersByIndex("System.ComponentModel", 1)) {
            }
            foreach (var varRef in entry.GetMembersByIndex("System.Deployment", 1)) {
            }
            foreach (var varRef in entry.GetMembersByIndex("System.Diagnostics", 1)) {
            }
            foreach (var varRef in entry.GetMembersByIndex("System.Dynamic", 1)) {
            }
            foreach (var varRef in entry.GetMembersByIndex("System.Globalization", 1)) {
            }
            foreach (var varRef in entry.GetMembersByIndex("System.Linq", 1)) {
            }
            foreach (var varRef in entry.GetMembersByIndex("System.Management", 1)) {
            }
            foreach (var varRef in entry.GetMembersByIndex("System.Media", 1)) {
            }
            foreach (var varRef in entry.GetMembersByIndex("System.Net", 1)) {
            }
            foreach (var varRef in entry.GetMembersByIndex("System.Runtime", 1)) {
            }
            foreach (var varRef in entry.GetMembersByIndex("System.Security", 1)) {
            }
            foreach (var varRef in entry.GetMembersByIndex("System.Text", 1)) {
            }
            foreach (var varRef in entry.GetMembersByIndex("System.Threading", 1)) {
            }

            sw.Stop();
            Console.WriteLine("{0} ms", sw.ElapsedMilliseconds);
        }

        /// <summary>
        /// Gets all members from a large number of types
        /// </summary>
        [PerfMethod]
        public void TestLookupPerf_Types() {
            var state = ProcessText(@"
import System
            ");
            var entry = state.Modules[state.DefaultModule].Analysis;

            Stopwatch sw = new Stopwatch();
            sw.Start();
            foreach (var varRef in entry.GetMembersByIndex("System", 1)) {
                foreach (var innerRef in entry.GetMembersByIndex("System" + varRef.Name, 1)) {
                }
            }
            sw.Stop();
            Console.WriteLine("{0} ms", sw.ElapsedMilliseconds);
        }

        [PerfMethod]
        public void TestLookupPerf_BuiltinModules() {
            var builtin_module_names = new[] { "sys", "__builtin__", "exceptions", "clr", "future_builtins", "imp", "array", "binascii", "_sha512", "cmath", "_winreg", "_weakref", "_warnings", "_sre", "_random", "_functools", "xxsubtype", "time", "thread", "_struct", "_heapq", "_ctypes_test", "_ctypes", "socket", "_sha256", "_sha", "select", "re", "operator", "nt", "_md5", "_fileio", "math", "marshal", "_locale", "itertools", "gc", "errno", "datetime", "cStringIO", "cPickle", "copy_reg", "_collections", "_bytesio", "_codecs" };
            StringBuilder text = new StringBuilder();
            foreach (var name in builtin_module_names) {
                text.AppendLine("import " + name);
            }
            var state = ProcessText(text.ToString());
            var entry = state.Modules[state.DefaultModule].Analysis;

            Stopwatch sw = new Stopwatch();
            sw.Start();
            for (int i = 0; i < 50; i++) {
                foreach (var name in builtin_module_names) {
                    foreach (var varRef in entry.GetMembersByIndex(name, 1)) {
                        foreach (var innerRef in entry.GetMembersByIndex(name + "." + varRef.Name, 1)) {
                        }
                    }
                }
            }
            sw.Stop();
            Console.WriteLine("{0} ms", sw.ElapsedMilliseconds);
        }

        internal PythonAnalyzer AnalyzeDir(string dir, PythonLanguageVersion version = PythonLanguageVersion.V27, IEnumerable<string> excludeDirectories = null, CancellationToken? cancel = null) {
            List<string> files = new List<string>();
            try {
                ISet<string> excluded = null;
                if (excludeDirectories != null) {
                    excluded = new HashSet<string>(excludeDirectories, StringComparer.InvariantCultureIgnoreCase);
                }
                CollectFiles(dir, files, excluded);
            } catch (DirectoryNotFoundException) {
                return null;
            }

            List<FileStreamReader> sourceUnits = new List<FileStreamReader>();
            foreach (string file in files) {
                sourceUnits.Add(
                    new FileStreamReader(file)
                );
            }

            Stopwatch sw = new Stopwatch();

            sw.Start();
            long start0 = sw.ElapsedMilliseconds;

            var fact = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(version.ToVersion());
            var projectState = new PythonAnalyzer(fact, fact.CreateInterpreter());
            projectState.ReloadModulesAsync(cancel ?? CancellationToken.None).WaitAndUnwrapExceptions();

            projectState.Limits = AnalysisLimits.GetStandardLibraryLimits();

            var modules = new List<IPythonProjectEntry>();
            foreach (var sourceUnit in sourceUnits) {
                try {
                    modules.Add(projectState.AddModule(
                        ModulePath.FromFullPath(sourceUnit.Path).ModuleName,
                        sourceUnit.Path,
                        null
                    ));
                } catch (ArgumentException) {
                    // Invalid module name, so skip the module
                }
            }
            long start1 = sw.ElapsedMilliseconds;
            Trace.TraceInformation("AddSourceUnit: {0} ms", start1 - start0);

            var nodes = new List<Microsoft.PythonTools.Parsing.Ast.PythonAst>();
            for (int i = 0; i < modules.Count; i++) {
                PythonAst ast = null;
                try {
                    var sourceUnit = sourceUnits[i];

                    ast = Parser.CreateParser(sourceUnit, version).ParseFile();
                } catch (Exception) {
                }
                nodes.Add(ast);
            }
            long start2 = sw.ElapsedMilliseconds;
            Trace.TraceInformation("Parse: {0} ms", start2 - start1);

            for (int i = 0; i < modules.Count; i++) {
                var ast = nodes[i];

                if (ast != null) {
                    using (var p = modules[i].BeginParse()) {
                        p.Tree = ast;
                        p.Complete();
                    }
                }
            }

            long start3 = sw.ElapsedMilliseconds;
            for (int i = 0; i < modules.Count; i++) {
                Trace.TraceInformation("Analyzing {1}: {0} ms", sw.ElapsedMilliseconds - start3, sourceUnits[i].Path);
                var ast = nodes[i];
                if (ast != null) {
                    modules[i].Analyze(cancel ?? CancellationToken.None, true);
                }
            }
            if (modules.Count > 0) {
                Trace.TraceInformation("Analyzing queue");
                modules[0].AnalysisGroup.AnalyzeQueuedEntries(cancel ?? CancellationToken.None);
            }

            long start4 = sw.ElapsedMilliseconds;
            Trace.TraceInformation("Analyze: {0} ms", start4 - start3);
            return projectState;
        }

        private static void CollectFiles(string dir, List<string> files, ISet<string> excludeDirectories = null) {
            foreach (string file in Directory.GetFiles(dir)) {
                if (file.EndsWithOrdinal(".py", ignoreCase: true)) {
                    files.Add(file);
                }
            }
            foreach (string nestedDir in Directory.GetDirectories(dir)) {
                if (excludeDirectories != null) {
                    var dirName = Path.GetFileName(nestedDir);
                    if (excludeDirectories.Contains(dirName)) {
                        continue;
                    }
                }
                CollectFiles(nestedDir, files, excludeDirectories);
            }
        }
    }
}
