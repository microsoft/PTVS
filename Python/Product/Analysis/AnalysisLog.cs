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
using Microsoft.PythonTools.Analysis.Values;

namespace Microsoft.PythonTools.Analysis {
    internal static class AnalysisLog {
        private static AnalysisLogWriter _log;
        private static bool _csv, _console;
        private static string _output;

        static DateTime? LastDisplayedTime = null;

        public static bool AsCSV {
            get => _csv;
            set {
                _csv = value;
                Reset();
            }
        }

        public static bool OutputToConsole {
            get => _console;
            set {
                _console = value;
                Reset();
            }
        }

        public static string Output {
            get => _output;
            set {
                _output = value;
                Reset();
            }
        }

        private static bool Active => _log != null;

        public static void Flush() {
            _log?.Flush();
        }

        public static void Add(string Event, params object[] Args) {
            if (_log == null) {
                return;
            }

            if (!LastDisplayedTime.HasValue || DateTime.UtcNow.Subtract(LastDisplayedTime.GetValueOrDefault()) > TimeSpan.FromMilliseconds(100)) {
                LastDisplayedTime = DateTime.UtcNow;
                _log.Log("TS", LastDisplayedTime.GetValueOrDefault());
            }

            _log.Log(Event, Args);
        }

        public static void Reset() {
            var oldLog = _log;
            _log = new AnalysisLogWriter(Output, AsCSV, OutputToConsole);
            oldLog?.Flush();
            oldLog?.Dispose();
            LastDisplayedTime = null;
        }

        public static void Enqueue(Deque<AnalysisUnit> deque, AnalysisUnit unit) {
            if (Active) {
                Add("E", IdDispenser.GetId(unit), deque.Count);
            }
        }

        public static void Dequeue(Deque<AnalysisUnit> deque, AnalysisUnit unit) {
            if (Active) {
                Add("D", IdDispenser.GetId(unit), deque.Count);
            }
        }

        public static void NewUnit(AnalysisUnit unit) {
            if (Active) {
                Add("N", IdDispenser.GetId(unit), unit.FullName, unit.ToString());
            }
        }

        public static void UpdateUnit(AnalysisUnit unit) {
            if (Active) {
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
    }
}
