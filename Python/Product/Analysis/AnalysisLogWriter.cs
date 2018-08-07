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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis.Infrastructure;

namespace Microsoft.PythonTools.Analysis {
    class AnalysisLogWriter : IDisposable {
        private readonly DateTime _startTime;
        private readonly string _outputFile;
        private List<LogItem> _items;
        private readonly bool _csv, _console;
        private readonly int _cache;
        private readonly SemaphoreSlim _dumping = new SemaphoreSlim(1);
        private bool _disposed;

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
            _items = new List<LogItem>(_cache + 2);
            _items.Add(new LogItem { Event = "Start", Time = TimeSpan.Zero, Args = new object[] { _startTime } });
        }

        protected void Dispose(bool disposing) {
            if (_disposed) {
                return;
            }

            _disposed = true;
            if (disposing) {
                _dumping.Dispose();
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~AnalysisLogWriter() {
            Dispose(false);
        }

        public bool CSV => _csv;
        public bool LogToConsole => _console;
        public string OutputFile => _outputFile;
        public TraceLevel MinimumLevel { get; set; } = TraceLevel.Warning;

        public void Rotate(int maxLines = 4096) {
            var enc = new UTF8Encoding(false);
            var lines = new string[maxLines];
            int i = 0;
            bool hitMaxLines = false;

            using (var log = PathUtils.OpenWithRetry(_outputFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) {
                if (log == null) {
                    return;
                }

                using (var reader = new StreamReader(log, enc, false, 4096, true)) {
                    string line;
                    while ((line = reader.ReadLine()) != null) {
                        lines[i++] = line;
                        while (i >= maxLines) {
                            hitMaxLines = true;
                            i -= maxLines;
                        }
                    }
                }

                if (!hitMaxLines) {
                    return;
                }

                log.Seek(0, SeekOrigin.Begin);
                log.SetLength(0);

                using (var writer = new StreamWriter(log, enc, 4096, true)) {
                    for (int j = (i + 1) % maxLines; j != i; j = (j + 1) % maxLines) {
                        if (lines[j] != null) {
                            writer.WriteLine(lines[j]);
                        }
                    }
                }
            }
        }

        public void Log(string eventName, params object[] args) {
            Log(TraceLevel.Off, eventName, args);
        }

        public void Log(TraceLevel level, string eventName, params object[] args) {
            if (level > MinimumLevel) {
                return;
            }

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

            if (_dumping.CurrentCount == 0) {
                if (synchronous) {
                    _dumping.Wait();
                    _dumping.Release();
                }
                return;
            }

            if (synchronous) {
                Dump();
            } else {
                Task.Run(() => Dump());
            }
        }

        private void Dump() {
            try {
                if (!_dumping.Wait(0)) {
                    return;
                }
            } catch (ObjectDisposedException) {
                return;
            }

            try {
                // Once triggered, allow any more immediate messages to be added.
                Thread.Sleep(50);

                var newList = new List<LogItem>(_cache + 2);
                var items = Interlocked.Exchange(ref _items, newList);
                lock (items) {
                    // Mark this list as complete, so new events will not go there
                    items.Add(new LogItem());
                }

                // Never just write a "Start" event
                if (items.Count == 2 && items[0].Event == "Start") {
                    lock (newList) {
                        newList.Insert(0, items[0]);
                    }
                    return;
                }

                using (var output = OpenLogFile()) {
                    foreach (var item in items) {
                        if (string.IsNullOrEmpty(item.Event)) {
                            continue;
                        }
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
            } catch (IOException) {
            } finally {
                try {
                    _dumping.Release();
                } catch (ObjectDisposedException) {
                }
            }
        }

        private TextWriter OpenLogFile() {
            if (string.IsNullOrEmpty(_outputFile)) {
                return null;
            }
            var enc = new UTF8Encoding(false);
            var stream = PathUtils.OpenWithRetry(_outputFile, FileMode.Append, FileAccess.Write, FileShare.Read);
            return stream == null ? null : new StreamWriter(stream, enc);
        }

        private static IEnumerable<string> AsNormalStrings(IEnumerable<object> items) {
            if (items == null) {
                yield break;
            }

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
            var s = format.FormatInvariant(args);

            // Last chance attempt at producing output that can be embedded in
            // test results file.
            if (s.IndexOfAny(_badChars) < 0) {
                return s;
            }

            for (char c = '\0'; c < ' '; ++c) {
                s = s.Replace(c.ToString(), "\\x{0:X2}".FormatInvariant((int)c));
            }
            return s;
        }
    }
}
