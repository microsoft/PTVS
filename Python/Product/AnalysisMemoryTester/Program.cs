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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Analysis.MemoryTester {
    class Program {
        static void PrintUsage() {
            Console.WriteLine("{0} scriptfile", Path.GetFileName(typeof(Program).Assembly.Location));
            Console.WriteLine();
            Console.WriteLine("Each line of the script file contains one of the following commands:");
            Console.WriteLine();
            Console.WriteLine("== Configuration Commands ==");
            Console.WriteLine(" python <version in x.y format> <interpreter path>");
            Console.WriteLine();
            Console.WriteLine("== Analysis Sequence Commands ==");
            Console.WriteLine(" module <module name> <relative path to source file>");
            Console.WriteLine(" enqueue <module name or *>");
            Console.WriteLine(" analyze");
            Console.WriteLine(" repeat <count> / end");
            Console.WriteLine();
            Console.WriteLine("== Memory Commands ==");
            Console.WriteLine(" dump <relative path to dump file to create>");
            Console.WriteLine(" gc");
            Console.WriteLine();
            Console.WriteLine("== Debugging Commands ==");
            Console.WriteLine(" debugbreak always|ifattached");
            Console.WriteLine(" print <message>");
            Console.WriteLine(" pause");
        }

        static T GetFirstCommand<T>(IEnumerable<string> commands, string pattern, Func<Match, T> parse, Func<T, bool> check = null) {
            var iter = commands
                .Select(cmd => Regex.Match(cmd, pattern))
                .Where(m => m.Success)
                .Select(parse);
            return check != null ? iter.FirstOrDefault(check) : iter.FirstOrDefault();
        }

        static IEnumerable<T> GetCommands<T>(IEnumerable<string> commands, string pattern, Func<Match, T> parse, Func<T, bool> check = null) {
            var iter = commands
                .Select(cmd => Regex.Match(cmd, pattern))
                .Where(m => m.Success)
                .Select(parse);
            return check != null ? iter.Where(check) : iter;
        }

        static void Main(string[] args) {
            var responseFile = Path.GetFullPath(args.FirstOrDefault() ?? "AnalysisMemoryTester.rsp");

            if (!File.Exists(responseFile)) {
                if (!string.IsNullOrEmpty(responseFile)) {
                    Console.WriteLine("Could not open {0}", responseFile);
                } else {
                    Console.WriteLine("No response file specified");
                }
                PrintUsage();
                return;
            }

            var commands = File.ReadAllLines(responseFile)
                .Select(line => line.Trim())
                .ToList();

            Environment.CurrentDirectory = Path.GetDirectoryName(responseFile);

            var interpreter = GetFirstCommand(commands, "python\\s+(\\d\\.\\d)\\s+(.+)", m => m.Value, v => v.Length > 4 && File.Exists(v.Substring(4).Trim()));
            var version = Version.Parse(interpreter.Substring(0, 3));
            interpreter = interpreter.Substring(4).Trim();
            Console.WriteLine($"Using Python from {interpreter}");

            var config = new InterpreterConfiguration(
                "Python|" + interpreter,
                interpreter,
                Path.GetDirectoryName(interpreter),
                interpreter,
                interpreter,
                "PYTHONPATH",
                NativeMethods.GetBinaryType(interpreter) == System.Reflection.ProcessorArchitecture.Amd64 ? InterpreterArchitecture.x64 : InterpreterArchitecture.x86,
                version
            );

            using (var factory = new Interpreter.Ast.AstPythonInterpreterFactory(config, new InterpreterFactoryCreationOptions()))
            using (var analyzer = PythonAnalyzer.CreateAsync(factory).WaitAndUnwrapExceptions()) {
                var modules = new Dictionary<string, IPythonProjectEntry>();
                var state = new State();

                foreach (var tuple in SplitCommands(commands)) {
                    RunCommand(
                        tuple.Item1,
                        tuple.Item2,
                        analyzer,
                        modules,
                        state
                    );
                }
            }
        }

        private static HashSet<string> IgnoredCommands = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase) {
            "python",
            "",
            null
        };

        private static IEnumerable<Tuple<string, string>> SplitCommands(IEnumerable<string> originalCommands) {
            using (var e = originalCommands.GetEnumerator()) {
                foreach (var cmd in SplitCommandsWorker(e)) {
                    yield return cmd;
                }
            }
        }

        private static IEnumerable<Tuple<string, string>> SplitCommandsWorker(IEnumerator<string> originalCommands) {
            while (originalCommands.MoveNext()) {
                var cmdLine = originalCommands.Current;
                int commentStart = cmdLine.IndexOf('#');
                if (commentStart >= 0) {
                    cmdLine = cmdLine.Substring(0, commentStart).Trim();
                }

                if (string.IsNullOrEmpty(cmdLine)) {
                    continue;
                }

                var m = Regex.Match(cmdLine, @"^\s*(?<cmd>\w+)\s*(?<args>.*)");
                if (!m.Success) {
                    Console.WriteLine("Invalid command: {0}", cmdLine);
                    continue;
                }

                var cmd = m.Groups["cmd"].Value;
                var args = m.Groups["args"].Value;

                if (IgnoredCommands.Contains(cmd)) {
                    continue;
                }

                if ("end".Equals(cmd, StringComparison.CurrentCultureIgnoreCase)) {
                    yield break;
                } else if ("repeat".Equals(cmd, StringComparison.CurrentCultureIgnoreCase)) {
                    int count;
                    if (!int.TryParse(args, out count)) {
                        Console.WriteLine("Failed to parse repeat count '{0}'", args);
                        Environment.Exit(1);
                    }
                    var cmds = SplitCommandsWorker(originalCommands).ToList();
                    while (--count >= 0) {
                        foreach (var t in cmds) {
                            yield return t;
                        }
                    }
                } else {
                    yield return Tuple.Create(cmd, args);
                }
            }
        }

        private class State {
            public long LastDumpSize;
        }

        private static void RunCommand(
            string cmd,
            string args,
            PythonAnalyzer analyzer,
            Dictionary<string, IPythonProjectEntry> modules,
            State state
        ) {
            switch (cmd.ToLower()) {
                case "print":
                    Console.WriteLine(args);
                    break;

                case "module":
                    foreach (var mod in GetModules(args)) {
                        IPythonProjectEntry entry;
                        if (!modules.TryGetValue(mod.ModuleName, out entry)) {
                            Console.WriteLine("Creating module {0}", mod.ModuleName);
                            modules[mod.ModuleName] = entry = analyzer.AddModule(mod.ModuleName, mod.SourceFile);
                        } else {
                            Console.WriteLine("Reusing module {0}", mod.ModuleName);
                        }

                        using (var file = File.OpenText(mod.SourceFile)) {
                            var parser = Parser.CreateParser(
                                file,
                                analyzer.LanguageVersion,
                                new ParserOptions() { BindReferences = true }
                            );
                            entry.UpdateTree(parser.ParseFile(), null);
                        }
                    }
                    break;
                case "enqueue":
                    if (args == "*") {
                        foreach (var entry in modules.Values) {
                            entry.Analyze(CancellationToken.None, true);
                        }
                    } else {
                        IPythonProjectEntry entry;
                        foreach (var modName in args.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s))) {
                            if (!modules.TryGetValue(modName, out entry)) {
                                Console.WriteLine("Could not enqueue {0}", modName);
                            } else {
                                entry.Analyze(CancellationToken.None, true);
                            }
                        }
                    }
                    break;
                case "analyze":
                    Console.Write("Waiting for complete analysis... ");
                    analyzer.AnalyzeQueuedEntries(CancellationToken.None);
                    Console.WriteLine("done!");
                    break;

                case "pause":
                    Console.Write("Press enter to continue...");
                    Console.ReadKey(true);
                    Console.WriteLine();
                    break;
                case "debugbreak":
                    if (!args.Equals("ifattached", StringComparison.CurrentCultureIgnoreCase) || Debugger.IsAttached) {
                        Console.WriteLine("Breaking into the debugger");
                        Debugger.Break();
                    }
                    break;
                case "gc":
                    Console.WriteLine("Collecting garbage");
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                    GC.WaitForFullGCComplete();
                    GC.WaitForPendingFinalizers();
                    break;
                case "dump": {
                        var fullPath = Path.GetFullPath(args);
                        var length = WriteDump(Process.GetCurrentProcess(), fullPath, MiniDumpType.FullDump);
                        Console.WriteLine(
                            "Dump written to {0} at {1:F1}MB ({2} bytes)",
                            fullPath,
                            length / (1024.0 * 1024.0),
                            length
                        );
                        if (state.LastDumpSize > 0 && state.LastDumpSize != length) {
                            var delta = Math.Abs(length - state.LastDumpSize);
                            var direction = (length > state.LastDumpSize) ? "larger" : "smaller";
                            Console.WriteLine(
                                "Dump is {0:F1}MB ({1} bytes) {2} than previous",
                                delta / (1024.0 * 1024.0),
                                delta,
                                direction
                            );
                        }
                        state.LastDumpSize = length;
                        break;
                    }
                default:
                    Console.WriteLine("Command not available: {0}", cmd);
                    break;
            }
        }

        private static IEnumerable<ModulePath> GetModules(string args) {
            var m = Regex.Match(args, "(\\*|[\\w\\.]+)\\s+(.+)");
            var modName = m.Groups[1].Value;
            var fileName = m.Groups[2].Value;
            if (modName != "*") {
                fileName = Path.GetFullPath(fileName);
                yield return new ModulePath(modName, fileName, null);
                yield break;
            }

            if (!fileName.Contains("*")) {
                yield return ModulePath.FromFullPath(Path.GetFullPath(fileName));
                yield break;
            }

            var opt = SearchOption.TopDirectoryOnly;

            if (fileName.StartsWith("**\\")) {
                opt = SearchOption.AllDirectories;
                fileName = fileName.Substring(3);
            }

            if (!Path.IsPathRooted(fileName)) {
                fileName = Path.Combine(Environment.CurrentDirectory, fileName);
            }

            Console.WriteLine("Adding modules from {0}", fileName);
            foreach (var file in Directory.EnumerateFiles(Path.GetDirectoryName(fileName), Path.GetFileName(fileName), opt)) {
                yield return ModulePath.FromFullPath(file);
            }
        }

        #region MiniDump Support

        public enum MiniDumpType {
            Normal = 0x0,
            WithDataSegs = 1,
            WithFullMemory = 2,
            WithHandleData = 4,
            FilterMemory = 8,
            ScanMemory = 0x10,
            WithUnloadedModules = 0x20,
            WithIndirectlyReferencedMemory = 0x40,
            FilterModulePaths = 0x80,
            WithProcessThreadData = 0x100,
            WithPrivateReadWriteMemory = 0x200,
            WithoutOptionalData = 0x400,
            WithFullMemoryInfo = 0x800,
            WithThreadInfo = 0x1000,
            WithCodeSegs = 0x2000,
            WithoutAuxiliaryState = 0x4000,
            WithFullAuxiliaryState = 0x8000,
            FullDump = WithDataSegs | WithFullMemory | WithHandleData
        }

        [DllImport("dbghelp.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool MiniDumpWriteDump(IntPtr hProcess, uint ProcessId, IntPtr hFile, MiniDumpType DumpType, IntPtr ExceptionParam, IntPtr UserStreamParam, IntPtr CallbackParam);

        public static long WriteDump(Process proc, string dump, MiniDumpType type) {
            Directory.CreateDirectory(Path.GetDirectoryName(dump));

            FileStream stream = null;
            for (int retries = 10; retries > 0 && stream == null; --retries) {
                try {
                    stream = new FileStream(dump, FileMode.Create, FileAccess.Write, FileShare.None);
                } catch (IOException ex) {
                    Console.WriteLine("Dump failed: {0}", ex.Message);
                    Console.WriteLine("Retrying in 10 seconds ({0} retries remaining before choosing another name)", retries);
                    // Ensure this is Ctrl+C abortable
                    var evt = new AutoResetEvent(false);
                    ConsoleCancelEventHandler handler = (o, e) => { evt.Set(); };
                    Console.CancelKeyPress += handler;
                    try {
                        if (evt.WaitOne(10000)) {
                            Environment.Exit(1);
                        }
                    } finally {
                        Console.CancelKeyPress -= handler;
                        evt.Dispose();
                    }
                }
            }

            if (stream == null) {
                var baseName = Path.GetFileNameWithoutExtension(dump);
                var ext = Path.GetExtension(dump);
                dump = string.Format("{0}{1}{2}", baseName, 1, ext);
                for (int i = 0; File.Exists(dump); ++i) {
                    dump = string.Format("{0}{1}{2}", baseName, 1, ext);
                }
                stream = new FileStream(dump, FileMode.Create, FileAccess.Write, FileShare.None);
            }

            using (stream) {
                if (!MiniDumpWriteDump(proc.Handle, (uint)proc.Id, stream.SafeFileHandle.DangerousGetHandle(), type, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero)) {
                    throw new Win32Exception();
                }
                return stream.Length;
            }
        }

        #endregion
    }
}
