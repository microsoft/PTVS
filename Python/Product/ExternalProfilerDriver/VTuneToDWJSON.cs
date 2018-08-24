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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Net;
using System.Globalization;

using Newtonsoft.Json;

namespace Microsoft.PythonTools.Profiling.ExternalProfilerDriver {
    
    class BaseSizeTuple {
        public long Base { get; set; }
        public long Size { get; set; }
        public BaseSizeTuple(long _base, long _size) {
            Base = _base;
            Size = _size;
        }

        override public string ToString()
        {
            return $"({this.Base},{this.Size})";
        }
    }

    class SequenceBaseSize {
        private long _current;
        private long _size; // should this be constant?
        public SequenceBaseSize(long start = 0, long size = 10) { _current = start; _size = size; }
        public IEnumerable<BaseSizeTuple> Generate() {
            while (true) {
                yield return new BaseSizeTuple(_current, _size);
                _current += _size + 1;
            }
        }
    }

    class FuncInfo
    {
        public string FunctionName { get; set; }
        public string SourceFile { get; set; }
        public long? LineNumber { get; set; }
        public long? Base { get; set; }
        public long? Size { get; set; }
        
        public FuncInfo(string _functionname, string _sourceFile = "", long? _lineNumber = null, long? _base = null, long? _size = null)
        {
            FunctionName = _functionname;
            SourceFile = _sourceFile;
            LineNumber = _lineNumber;
            Base = _base;
            Size = _size;
        }

        override public string ToString()
        {
            return $"Function {this.FunctionName}, @ {this.SourceFile}:{this.LineNumber} and assigned Base/Size {this.Base}/{this.Size}";
        }
    }
    
    class FuncInfoComparer : IEqualityComparer<FuncInfo>
    {
        public bool Equals(FuncInfo x, FuncInfo y)
        {
            return x.FunctionName == y.FunctionName && x.SourceFile == y.SourceFile;
        }
 
        public int GetHashCode(FuncInfo obj)
        {
            return (obj.FunctionName + obj.SourceFile).GetHashCode();
        }
    }

    class VTuneToDWJSON {
        /// <summary>
        /// <paramref name="filename"/>
        /// </summary>
        /// <param name="filename">The filename with the callstack report</param>
        public static double CSReportToDWJson(string filename, string outfname) {
            if (!File.Exists(filename)) {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.ErrorMsgPathDoesNotExist, filename));
            }
            var samples = ParseFromFile(filename);

            var times = samples.Select(x => x.TOSFrame.CPUTime);
            var total = times.Sum();
            var fractional = times.Select(x => (x / total) * 100).ToList();

            LongInt duration = TraceUtils.ToNanoseconds(total);

            var modFunDictionary = samples.SelectMany(sm => sm.AllSamples())
                                          .Select(p => new { Module = p.Module, Function = p.Function })
                                          .GroupBy(t => t.Module)
                                          .Select(g => new { Module = g.Key, Functions = g.Select(gg => gg.Function).Distinct() });

            // Create a two-level dictionary module -> (function -> (base, size))
            var mfdd = modFunDictionary.Select(x => new {
                Module = x.Module,
                Functions = x.Functions.Zip((new SequenceBaseSize()).Generate(), (f, b) => new { Function = f, BaseSize = b })
                                                                                 .ToDictionary(t => t.Function, t => t.BaseSize)
            })
                                       .ToDictionary(od => od.Module, od => od.Functions);

            if (mfdd.Count <= 0) {
                throw new Exception(Strings.ErrorMsgCannotBuildModuleFunctionDict);
            }

            var mods = mfdd.Zip(Enumerable.Range(1, int.MaxValue), (x, y) => new ModuleSpec() {
                name = x.Key,
                id = y,
                begin = new LongInt(0, 0), // should build these according to mfdd (i.e., argument x)
                end = new LongInt(0, 10000), // not sure why 2500 is the smallest number than seems to work
                @base = new LongInt(0, (y - 1) * 1000),
                size = new LongInt(0, 300),
                ranges = x.Value.Select(xx => new FunctionSpec(xx.Key, xx.Value.Base, xx.Value.Size)).ToList()
            });
            var modBase = mods.ToDictionary(x => x.name, x => x.@base);

            AddressTranslator tr = new AddressTranslator(modBase, mfdd);

            int startime = 2500; // the base is important, as it's coordinated with the modules `end`
            int stepsize = 1;

            List<FrameInfo> chains = new List<FrameInfo>();
            int idx = 0;
            foreach (var s in samples) {
                foreach (var y in s.Stacks) {
                    var fi = new FrameInfo {
                        timestamp = new LongInt(0, startime + stepsize * idx),
                        frameIPs = y.Select(z => tr.Translate(z.Module, z.Function)).ToList()
                    };
                    fi.frameIPs.Insert(0, tr.Translate(s.TOSFrame.Module, s.TOSFrame.Function));
                    chains.Add(fi);

                    idx++;
                }
            }

            ThreadSpec thread = new ThreadSpec() {
                id = 1,
                begin = new LongInt(0, 2000),
                end = new LongInt(0, 3000),
                stacks = chains
            };

            ProcessSpec proc = new ProcessSpec {
                name = "python36.dll",
                id = 1,
                begin = new LongInt(0, 1000),
                //end = new LongInt(0, 8000),
                end = duration,
                isTarget = true,
                isUser = true,
                moduleIDs = Enumerable.Range(1, int.MaxValue).Take(mods.Count()).ToList(),
                threads = new List<ThreadSpec> { thread }
            };
            var processes = new List<ProcessSpec>() { proc };

            var trace = new Trace {
                totalTimeRange = new TimeSpec {
                    begin = new LongInt(0, 0),
                    //duration = new LongInt(0, (int)(total * 1000))
                    //duration = new LongInt(0, 10000)
                    duration = duration
                },
                name = Dns.GetHostName() ?? "machine-name",
                processor = new ProcessorSpec {
                    logicalCount = 4,
                    speedInMHz = 2670,
                    pointerSizeInBytes = 4,
                    highestUserAddress = new LongInt(0, 2147418111)
                },
                processes = processes,
                modules = mods.ToList()
            };

            string json = JsonConvert.SerializeObject(trace, Formatting.Indented, new JsonSerializerSettings {
                NullValueHandling = NullValueHandling.Ignore
            });
            File.WriteAllText(outfname, json);

            return total;
        }

        public static IEnumerable<SampleWithTrace> ParseFromFile(string filename) {
            var samples = Utils.ReadFromFile(filename)
                               .Skip(1)
                               .Select(s => String.Join(",", s.Split(',')
                               .Select(VTuneStackParser.RemovePrePosComma)))
                               .ParseFromStream();
            return samples;
        }

        public static void CPUReportToDWJson(string filename, string outfname, double timeTotal = 0.0) {
            if (!File.Exists(filename)) {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.ErrorMsgCannotFindCPUUtilizationReport, filename));
            }

            if (timeTotal <= 0) {
                throw new Exception(Strings.ErrorMsgWrongTimeSpecified);
            }

            LongInt durationli = TraceUtils.ToNanoseconds(timeTotal);

            var cpuRecords = Utils.ReadFromFile(filename)
                                  .Skip(2)
                                  .ParseCPURecords();
            /*
            CPUUtilRecord first = cpuRecords.First();
            CPUUtilRecord last = cpuRecords.Last();

            CPUUtilTrace trace = new CPUUtilTrace();
            trace.beginTime = new LongInt(0, (long)(first.Start));
            trace.duration = new LongInt(0, (long)(last.End - first.Start));
            trace.counters = new List<ValueTrace> { new ValueTrace(cpuRecords.Select(r => new CPUSample(new LongInt(0, (long)r.Start), (float)r.CPUUtil)).ToList()) };
            */

            int steps = cpuRecords.Count() - 1;
            double totalTime = timeTotal;
            double stepSize = totalTime / steps;

            List<ValueTrace> vts = new List<ValueTrace>();
            vts.Add(new ValueTrace(Enumerable.Range(0, int.MaxValue).Take(steps).Zip(cpuRecords, (x, y) => new CPUSample(TraceUtils.ToNanoseconds(x * stepSize), (float)(y.CPUUtil)))));

            CPUUtilTrace trace = new CPUUtilTrace {
                beginTime = new LongInt(0, 0),
                //duration = new LongInt(0, totalTime),
                duration = durationli,
                counters = vts
            };

            string json = JsonConvert.SerializeObject(trace, Formatting.Indented, new JsonSerializerSettings {
                NullValueHandling = NullValueHandling.Ignore
            });

            // var fs = new FileStream(@"C:\users\perf\Sample2.counters", FileMode.Create);
            var fs = new FileStream(outfname, FileMode.Create);
            using (StreamWriter writer = new StreamWriter(fs, Encoding.Unicode)) { // encoding in Unicode here is key
                writer.WriteLine(json);
            }
        }
    }

    class AddressTranslator {
        private Dictionary<string, Dictionary<string, BaseSizeTuple>> modfundict;
        private Dictionary<string, LongInt> modindex;

        public AddressTranslator(Dictionary<string, LongInt> _modindex, Dictionary<string, Dictionary<string, BaseSizeTuple>> _modfundict) {
            modfundict = _modfundict;
            modindex = _modindex;
        }

        public LongInt Translate(string module, string function) {
            return new LongInt(0, modindex[module].l + modfundict[module][function].Base + 1);
        }
    }
}
