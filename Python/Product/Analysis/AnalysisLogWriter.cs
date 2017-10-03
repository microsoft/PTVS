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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Analysis {
    class AnalysisLogWriter {
        private readonly DateTime _startTime;
        private readonly string _outputFile;
        private List<LogItem> _items;
        private readonly bool _csv, _console;
        private readonly int _cache;

        public struct LogItem {
            public TimeSpan Time;
            public string Event;
            public object[] Args;
        }

        public AnalysisLogWriter(string outputFile, bool csv, bool logToConsole, int cacheSize = 100) {
            _startTime = DateTime.UtcNow;
            _outputFile = outputFile;
            _csv = csv;
            _console = logToConsole;
            _cache = cacheSize;
            _items = new List<LogItem>(_cache);
            _items.Add(new LogItem { Event = "Start", Time = TimeSpan.Zero, Args = new object[] { _startTime } });
        }

        public bool CSV => _csv;
        public bool LogToConsole => _console;
        public string OutputFile => _outputFile;

        public void Log(string eventName, params object[] args) {
            if (!LogToConsole && string.IsNullOrEmpty(OutputFile)) {
                return;
            }

            var item = new LogItem {
                Time = DateTime.UtcNow.Subtract(_startTime),
                Event = eventName,
                Args = args
            };

            bool shouldFlush = false;
            for (; ; ) {
                var items = Volatile.Read(ref _items);
                lock (items) {
                    if (items.Count > 0 && items.Last().Event == null) {
                        // List was marked as complete, so we retry and should get a new one
                        continue;
                    }
                    items.Add(item);
                    shouldFlush = items.Count >= _cache;
                }
                break;
            }
            if (shouldFlush) {
                Flush();
            }
        }

        public void Flush(bool synchronous = false) {
            if (!LogToConsole && string.IsNullOrEmpty(OutputFile)) {
                return;
            }

            if (synchronous) {
                Dump();
            } else {
                Task.Run(() => Dump());
            }
        }

        private void Dump() {
            var newList = new List<LogItem>(_cache);
            var items = Interlocked.Exchange(ref _items, newList);
            lock (items) {
                // Mark this list as complete, so new events will not go there
                items.Add(new LogItem());
            }

            using (var output = OpenLogFile()) {
                foreach (var item in items) {
                    var s = SafeFormat(_csv ? "{0},{1},{2}" : "[{0}] {1}: {2}",
                        item.Time,
                        item.Event,
                        _csv ? string.Join(",", AsCsvStrings(item.Args)) : string.Join(", ", AsNormalStrings(item.Args))
                    ) + Environment.NewLine;
                    if (output != null) {
                        output.Write(s);
                        output.Flush();
                    }
                    if (_console) {
                        Console.Out.Write(s);
                        Console.Out.Flush();
                    }
                }
            }
        }

        private TextWriter OpenLogFile() {
            if (string.IsNullOrEmpty(_outputFile)) {
                return null;
            }

            while (true) {
                try {
                    return new StreamWriter(new FileStream(_outputFile, FileMode.Append, FileAccess.Write, FileShare.Read), new UTF8Encoding(false));
                } catch (IOException) {
                    Thread.Sleep(10);
                }
            }
        }

        private static IEnumerable<string> AsNormalStrings(IEnumerable<object> items) {
            foreach (var item in items) {
                if (item is DateTime dt) {
                    yield return dt.ToString("s");
                } else if (item == null) {
                    yield return "<null>";
                } else {
                    yield return item.ToString();
                }
            }
        }

        private static IEnumerable<string> AsCsvStrings(IEnumerable<object> items) {
            foreach (var item in AsNormalStrings(items)) {
                var str = item;
                if (str.Contains(',') || str.Contains('"')) {
                    str = "\"" + str.Replace("\"", "\"\"") + "\"";
                }
                yield return str;
            }
        }

        private static readonly char[] _badChars = Enumerable.Range(0, 32).Select(c => (char)c).ToArray();

        private static string SafeFormat(string format, params object[] args) {
            var s = string.Format(format, args);

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
    }
}
