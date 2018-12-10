// Python Tools for Visual Studio
// Copyright(c) 2018 Intel Corporation.  All rights reserved.
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
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Text;

using CsvHelper;

namespace Microsoft.PythonTools.Profiling.ExternalProfilerDriver {

    class PerformanceSample {
        public string Function { get; }
        public float CPUTime { get; }
        public string Module { get; }
        public string FunctionFull { get; }
        public string SourceFile { get; }
        public string StartAddress { get; }

        public PerformanceSample(string function, string cpuTime, string module, string functionFull, string sourceFile, string startAddress) {
            Function = function;
            try {
                CPUTime = Single.Parse(cpuTime);
            } catch (Exception) { // TODO -- should use a more specific exception
                CPUTime = 0;
            }
            Module = module;
            FunctionFull = functionFull;
            SourceFile = sourceFile;
            StartAddress = startAddress;
        }
    }

    class SampleWithTrace {
        private List<List<PerformanceSample>> _stacks = new List<List<PerformanceSample>>();
        public PerformanceSample TOSFrame { get; }
        public IEnumerable<IEnumerable<PerformanceSample>> Stacks {
            get {
                foreach (var s in _stacks) {
                    yield return s.AsEnumerable();
                }
            }
        }

        public SampleWithTrace(PerformanceSample sample) {
            TOSFrame = sample;
        }

        public void AddStack(List<PerformanceSample> stack) {
            _stacks.Add(stack);
        }
        public IEnumerable<PerformanceSample> AllSamples() {
            yield return TOSFrame; // assumes this is not null
            foreach (var stack in Stacks) {
                foreach (var frame in stack) {
                    yield return frame;
                }
            }
        }
    }

    static class VTuneStackParser {
        public static string RemovePrePosComma(string str) {
            if (str.Length > 0) {
                if (str[0] == '"') { str = str.Substring(1, str.Length - 1); }
            }
            if (str.Length > 0) {
                if (str[str.Length - 1] == '"') { str = str.Substring(0, str.Length - 1); }
            }
            return str;
        }

        public static IEnumerable<SampleWithTrace> ParseFromStream(this IEnumerable<string> seq) {
            Regex startsWithComma = new Regex("^(,+)(.*)$");

            SampleWithTrace current = null;
            List<PerformanceSample> currentStack = null;
            bool atEnd = false;

            foreach (var l in seq) {
                Match m = startsWithComma.Match(l);
                if (!m.Success) {
                    if (atEnd == true) {
                        if (currentStack != null) {
                            current.AddStack(currentStack);
                            currentStack = null;
                        }
                        yield return current;
                    }
                    atEnd = false;

                    // should assert record is comma-separated, seven-field, second one empty
                    var fields = l.Split(',');
                    try {
                                                                     /* Function, CPUTime, Module, FunctionFull, SourceFile, StartAddress */
                    current = new SampleWithTrace(new PerformanceSample(fields[0], fields[2], fields[3], fields[4], fields[5], fields[6]));
                    } catch (Exception ex) {
                        // Discard record
                        Console.WriteLine(string.Format(CultureInfo.CurrentCulture, Strings.ErrorMsgUnexpectedInputWhileParsing, ex.Message));
                    }
                } else {
                    // assert m.Groups.Count is 3
                    if (m.Groups[1].Length == 1) {
                        if (atEnd == true || currentStack == null) {
                            if (currentStack != null) {
                                current.AddStack(currentStack);
                            }
                            currentStack = new List<PerformanceSample>();
                        }
                        atEnd = false;
                        var fields = m.Groups[2].Value.Split(',');
                        try {
                                                                /* Function, CPUTime, Module, FunctionFull, SourceFile, StartAddress */
                            currentStack.Add(new PerformanceSample(fields[0], fields[1], fields[2], fields[3], fields[5], fields[5]));
                        } catch (Exception ex) {
                            // Discard record... happens on de-mangled C++ multi-templatized functions on Linux
                            Console.WriteLine(string.Format(CultureInfo.CurrentCulture, Strings.ErrorMsgUnexpectedInputWhileParsing, ex.Message));
                        }
                    } else {
                        // verify that the only other allowed value for Groups[1].Length is 6?
                        atEnd = true;
                    }
                }
            }
                        
            if (current != null) {
                if (currentStack != null) {
                    current.AddStack(currentStack);
                }
                yield return current;
            }          
        }

    }

    class VTuneStackParserForCPP {
        public static IEnumerable<SampleWithTrace> ParseFromFile(string filename) {
            if (!File.Exists(filename)) {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.ErrorMsgPathDoesNotExist, filename));
            }

            SampleWithTrace current = null;
            List<PerformanceSample> currentStack = new List<PerformanceSample>();

            uint index = 0;
            using (var reader = new StreamReader(filename)) {
                using (CsvReader csvReader = new CsvReader(reader)) {

                    while (csvReader.Read()) {
                        var row = csvReader.GetRecord<dynamic>();
                        var dict = row as IDictionary<string, object>;

                            if (dict["Function"] == null || dict["Function"].ToString() == string.Empty) {
                                if (dict["Function Stack"] == null || dict["Function Stack"].ToString() == string.Empty) {
                                    current.AddStack(currentStack);
                                    currentStack = new List<PerformanceSample>();
                                } else {
                                    PerformanceSample s = new PerformanceSample(dict["Function Stack"].ToString(), 
                                                                                dict["CPU Time"].ToString(),
                                                                                dict["Module"].ToString(),
                                                                                dict["Function (Full)"].ToString(),
                                                                                dict["Source File"].ToString(),
                                                                                dict["Start Address"].ToString());
                                    currentStack.Add(s);
                                }
                            } else {
                                if (current != null) {
                                    yield return current;
                                }
                                PerformanceSample s = new PerformanceSample(dict["Function"].ToString(), 
                                                                            dict["CPU Time"].ToString(),
                                                                            dict["Module"].ToString(),
                                                                            dict["Function (Full)"].ToString(),
                                                                            dict["Source File"].ToString(),
                                                                            dict["Start Address"].ToString()
                                );

                                current = new SampleWithTrace(s);
                            }
                            index += 1;
                    }
                }
            }
            if (current != null) {
                yield return current;
            }
        }
    }
}
