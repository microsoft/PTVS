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
using System.Linq;
using Microsoft.PythonTools.Analysis.Interpreter;

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

        public static void Dump() {
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
                            Output.WriteLine("{0}, {1}", item.Event, string.Join(", ", AsCsvStrings(item.Args)));
                        } else {
                            Output.WriteLine("[{0}] {1}", item.Event, string.Join(", ", item.Args));
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
            Add("E", IdDispenser.GetId(unit), deque.Count);
        }

        public static void Dequeue(Deque<AnalysisUnit> deque, AnalysisUnit unit) {
            Add("D", IdDispenser.GetId(unit), deque.Count);
        }

        public static void NewUnit(AnalysisUnit unit) {
            Add("N", IdDispenser.GetId(unit), unit.FullName, unit.ToString());
        }

        public static void UpdateUnit(AnalysisUnit unit) {
            Add("U", IdDispenser.GetId(unit), unit.FullName, unit.ToString());
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
    }
}
