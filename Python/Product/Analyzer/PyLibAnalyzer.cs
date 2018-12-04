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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Intellisense;

namespace Microsoft.PythonTools.Analysis {
    internal class PyLibAnalyzer : IDisposable {
        private static void Help() {
            Console.WriteLine("Python Library Analyzer {0} ({1})",
                AssemblyVersionInfo.StableVersion,
                AssemblyVersionInfo.Version);
            Console.WriteLine("Python analysis server.");
            Console.WriteLine();
#if DEBUG
            Console.WriteLine(" /unittest - run from tests, Debug.Listeners will be replaced");
#endif
        }

        private static IEnumerable<KeyValuePair<string, string>> ParseArguments(IEnumerable<string> args) {
            string currentKey = null;

            using (var e = args.GetEnumerator()) {
                while (e.MoveNext()) {
                    if (e.Current.StartsWithOrdinal("/")) {
                        if (currentKey != null) {
                            yield return new KeyValuePair<string, string>(currentKey, null);
                        }
                        currentKey = e.Current.Substring(1).Trim();
                    } else {
                        yield return new KeyValuePair<string, string>(currentKey, e.Current);
                        currentKey = null;
                    }
                }

                if (currentKey != null) {
                    yield return new KeyValuePair<string, string>(currentKey, null);
                }
            }
        }

        /// <summary>
        /// The exit code returned when database generation fails due to an
        /// invalid argument.
        /// </summary>
        public const int InvalidArgumentExitCode = -1;

        /// <summary>
        /// The exit code returned when database generation fails due to a
        /// non-specific error.
        /// </summary>
        public const int InvalidOperationExitCode = -2;

        public static int Main(string[] args) {
            PyLibAnalyzer inst;
            try {
                inst = MakeFromArguments(args);
            } catch (ArgumentNullException ex) {
                Console.Error.WriteLine("{0} is a required argument", ex.Message);
                Help();
                return InvalidArgumentExitCode;
            } catch (ArgumentException ex) {
                Console.Error.WriteLine("'{0}' is not valid for {1}", ex.Message, ex.ParamName);
                Help();
                return InvalidArgumentExitCode;
            } catch (InvalidOperationException ex) {
                Console.Error.WriteLine(ex.Message);
                Help();
                return InvalidOperationExitCode;
            }

            using (inst) {
                return inst.Run().GetAwaiter().GetResult();
            }
        }

        private async Task<int> Run() {
#if DEBUG
            // Running with the debugger attached will skip the
            // unhandled exception handling to allow easier debugging.
            if (Debugger.IsAttached) {
                await RunWorker();
            } else {
#endif
                try {
                    await RunWorker();
                } catch (Exception e) {
                    Console.Error.WriteLine("Error during analysis: {0}{1}", Environment.NewLine, e.ToString());
                    return -10;
                }
#if DEBUG
            }
#endif

            return 0;
        }

        private async Task RunWorker() {
            var analyzer = new OutOfProcProjectAnalyzer(
                Console.OpenStandardOutput(),
                Console.OpenStandardInput(),
                Console.Error.WriteLine
            );

            await analyzer.ProcessMessages();
        }

        public PyLibAnalyzer() {
        }

        public void Dispose() {
        }

        private static PyLibAnalyzer MakeFromArguments(IEnumerable<string> args) {
            var options = ParseArguments(args)
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);

#if DEBUG
            if (options.ContainsKey("unittest")) {
                AssertListener.Initialize();
            }
#endif

            return new PyLibAnalyzer();
        }

#if DEBUG
        class AssertListener : TraceListener {
            private AssertListener() {
            }

            public override string Name {
                get { return "Microsoft.PythonTools.AssertListener"; }
                set { }
            }

            public static bool LogObjectDisposedExceptions { get; set; } = true;

            public static void Initialize() {
                var listener = new AssertListener();
                if (null == Debug.Listeners[listener.Name]) {
                    Debug.Listeners.Add(listener);
                    Debug.Listeners.Remove("Default");

                    AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;
                }
            }

            static void CurrentDomain_FirstChanceException(object sender, FirstChanceExceptionEventArgs e) {
                if (e.Exception is NullReferenceException || (e.Exception is ObjectDisposedException && LogObjectDisposedExceptions)) {
                    // Exclude safe handle messages because they are noisy
                    if (!e.Exception.Message.Contains("Safe handle has been closed")) {
                        var log = new EventLog("Application");
                        log.Source = "Application Error";
                        log.WriteEntry(
                            "First-chance exception: " + e.Exception.ToString(),
                            EventLogEntryType.Warning
                        );
                    }
                }
            }

            public override void Fail(string message) {
                Fail(message, null);
            }

            public override void Fail(string message, string detailMessage) {
                Trace.WriteLine("Debug.Assert failed in analyzer");
                if (!string.IsNullOrEmpty(message)) {
                    Trace.WriteLine(message);
                } else {
                    Trace.WriteLine("(No message provided)");
                }
                if (!string.IsNullOrEmpty(detailMessage)) {
                    Trace.WriteLine(detailMessage);
                }
                var trace = new StackTrace(true);
                bool seenDebugAssert = false;
                foreach (var frame in trace.GetFrames()) {
                    var mi = frame.GetMethod();
                    if (!seenDebugAssert) {
                        seenDebugAssert = (mi.DeclaringType == typeof(Debug) &&
                            (mi.Name == "Assert" || mi.Name == "Fail"));
                    } else if (mi.DeclaringType == typeof(System.RuntimeMethodHandle)) {
                        break;
                    } else {
                        var filename = frame.GetFileName();
                        Console.WriteLine(string.Format(
                            " at {0}.{1}({2}) in {3}:line {4}",
                            mi.DeclaringType.FullName,
                            mi.Name,
                            string.Join(", ", mi.GetParameters().Select(p => p.ToString())),
                            filename ?? "<unknown>",
                            frame.GetFileLineNumber()
                        ));
                        if (File.Exists(filename)) {
                            try {
                                Console.WriteLine(
                                    "    " +
                                    File.ReadLines(filename).ElementAt(frame.GetFileLineNumber() - 1).Trim()
                                );
                            } catch {
                            }
                        }
                    }
                }

                message = string.IsNullOrEmpty(message) ? "Debug.Assert failed" : message;
                if (Debugger.IsAttached) {
                    Debugger.Break();
                }

                Console.WriteLine(message);
            }

            public override void WriteLine(string message) {
            }

            public override void Write(string message) {
            }
        }
#endif
    }
}