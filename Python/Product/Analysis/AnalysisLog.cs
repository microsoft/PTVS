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
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Infrastructure;

namespace Microsoft.PythonTools.Analysis {
    internal static class AnalysisLog {
        static DateTime StartTime = DateTime.UtcNow;
        static TimeSpan? LastDisplayedTime = null;
        static List<List<LogItem>> LogItems;

        public static bool AsCSV { get; set; }
        public static TextWriter Output { get; set; }

        public struct LogItem {
            public TimeSpan Time;
            public string Event;
            public object[] Args;

            public override string ToString() {
                return string.Format("[{0}] {1}: {2}", Time, Event, string.Join(", ", Args));
            }
        }

        public static void Flush() {
            Dump();
        }

        private static readonly char[] _badChars = Enumerable.Range(0, 32).Select(c => (char)c).ToArray();

        private static string Sanitize(string s) {
            // Last chance attempt at producing output that can be embedded in
            // test results file.
            if (s.IndexOfAny(_badChars) < 0) {
                return s;
            }

            for (char c = '\0'; c < ' '; ++c) {
                s = s.Replace(c.ToString(), string.Format("\\x{0:X2}", (int)c));
            }
            return s;
        }

        private static void Dump() {
            var items = LogItems;
            LogItems = null;
            if (Output != null && items != null) {
                foreach (var item in items.SelectMany()) {
                    if (!LastDisplayedTime.HasValue || item.Time.Subtract(LastDisplayedTime.GetValueOrDefault()) > TimeSpan.FromMilliseconds(100)) {
                        LastDisplayedTime = item.Time;
                        Output.WriteLine(AsCSV ? "TS, {0}, {1}" : "[TS] {0}, {1}", item.Time.TotalMilliseconds, item.Time);
                    }

                    try {
                        if (AsCSV) {
                            Output.WriteLine("{0}, {1}", item.Event, Sanitize(string.Join(", ", AsCsvStrings(item.Args))));
                        } else {
                            Output.WriteLine("[{0}] {1}", item.Event, Sanitize(string.Join(", ", item.Args)));
                        }
                    } catch { }
                }
                Output.Flush();
            }
        }

        static IEnumerable<string> AsCsvStrings(IEnumerable<object> items) {
            foreach (var item in items) {
                var str = item.ToString();
                if (str.Contains(',') || str.Contains('"')) {
                    str = "\"" + str.Replace("\"", "\"\"") + "\"";
                }
                yield return str;
            }
        }

        public static void Add(string Event, params object[] Args) {
            if (Output == null) {
                return;
            }

            if (LogItems == null) {
                LogItems = new List<List<LogItem>>();
            }
            if (LogItems.Count >= 100) {
                Dump();
                if (LogItems == null) {
                    LogItems = new List<List<LogItem>>();
                }
            }
            if (LogItems.Count == 0) {
                LogItems.Add(new List<LogItem>(100));
            }
            var dest = LogItems[LogItems.Count - 1];
            if (dest.Count >= 100) {
                dest = new List<LogItem>();
                LogItems.Add(dest);
            }

            dest.Add(new LogItem { Time = Time, Event = Event, Args = Args });
        }

        public static void Reset() {
            LogItems = new List<List<LogItem>>();
            LogItems.Add(new List<LogItem>());
        }

        public static void ResetTime() {
            StartTime = DateTime.UtcNow;
            LastDisplayedTime = null;
        }

        static TimeSpan Time {
            get {
                return DateTime.UtcNow - StartTime;
            }
        }

        public static void Enqueue(Deque<AnalysisUnit> deque, AnalysisUnit unit) {
            if (Output != null) {
                Add("E", IdDispenser.GetId(unit), deque.Count);
            }
        }

        public static void Dequeue(Deque<AnalysisUnit> deque, AnalysisUnit unit) {
            if (Output != null) {
                Add("D", IdDispenser.GetId(unit), deque.Count);
            }
        }

        public static void NewUnit(AnalysisUnit unit) {
            if (Output != null) {
                Add("N", IdDispenser.GetId(unit), unit.FullName, unit.ToString());
            }
        }

        public static void UpdateUnit(AnalysisUnit unit) {
            if (Output != null) {
                Add("U", IdDispenser.GetId(unit), unit.FullName, unit.ToString());
            }
        }

        public static void EndOfQueue(int beforeLength, int afterLength) {
            Add("Q", beforeLength, afterLength, afterLength - beforeLength);
        }

        public static void ExceedsTypeLimit(string variableDefType, int total, string contents) {
            Add("X", variableDefType, total, contents);
        }

        public static void Cancelled(Deque<AnalysisUnit> queue) {
            Add("Cancel", queue.Count);
        }

        public static void ReduceCallDepth(FunctionInfo functionInfo, int callCount, int newLimit) {
            Add("R", functionInfo, callCount, newLimit);
        }

        public static void StartFileGroup(string library, int fileCount) {
            Add("FG", library, fileCount);
        }

        public static void EndFileGroup() {
            Add("EFG");
        }

        public static void Assert(bool condition, string message = null) {
            if (!condition) {
                try {
                    throw new InvalidOperationException(message);
                } catch (InvalidOperationException) {
                }
            }
        }
    }
}
