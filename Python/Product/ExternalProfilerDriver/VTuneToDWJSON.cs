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

using Newtonsoft.Json;

namespace Microsoft.PythonTools.Profiling.ExternalProfilerDriver {
    public class BaseSizeTuple {
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

    public class SequenceBaseSize {
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

    public class FuncInfo
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
    
    public class FuncInfoComparer : IEqualityComparer<FuncInfo>
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
                throw new ArgumentException($"Specified file {filename} does not exist!");
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
                throw new Exception("Couldn't build the module/function dictionary, can't figure out why");
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

            string json = JsonConvert.SerializeObject(trace, Formatting.Indented);
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
                throw new ArgumentException($"Cannot find specified CPU utilization report {filename}");
            }

            if (timeTotal <= 0) {
                throw new Exception("Invalid runtime specification in CPU utilization report");
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

            string json = JsonConvert.SerializeObject(trace, Formatting.Indented);

            // var fs = new FileStream(@"C:\users\perf\Sample2.counters", FileMode.Create);
            var fs = new FileStream(outfname, FileMode.Create);
            using (StreamWriter writer = new StreamWriter(fs, Encoding.Unicode)) { // encoding in Unicode here is key
                writer.WriteLine(json);
            }
        }

        /// <summary>
        /// Creates a two-level dictionary from a stream of <cref>SampleWithTrace</cref>.
        /// The "primary" (top-level) key is the module name, and the lower-level key
        /// is the function name (function -> (sourcefile, base, size))
        /// </summary>
        public static Dictionary< string, Dictionary< string, FuncInfo > > ModuleFuncDictFromSamples(IEnumerable<SampleWithTrace> samples)
        {
            var modFunDictionary = samples.SelectMany(sm => sm.AllSamples())
                                              .Select(p => new { Module = p.Module, Function = p.Function, SourceFile = p.SourceFile })
                                              .GroupBy(t => t.Module)
                                              .Select(g => new { Module = g.Key, 
                                                                 Functions = g.Select(gg => new FuncInfo(gg.Function, gg.SourceFile)).Distinct(new FuncInfoComparer()),
                                                               });

            var mfdd = modFunDictionary.ToDictionary(r => r.Module, 
                                                     r => r.Functions.ToDictionary(
                                                         rr => rr.FunctionName,
                                                         rr => rr
                                                     ));

            return mfdd;
        }

        public static Dictionary<string, List<SourceFileId>> SourceFilesByModule(Dictionary< string, Dictionary< string, FuncInfo > > modfun)
        {

            var modFile = modfun.Select( mfs => new { Module = mfs.Key, 
                                                      FilesId = mfs.Value.Values.Select(fi => fi.SourceFile).Distinct().Where(f => File.Exists(f))
                                                                         .Zip(Enumerable.Range(1, int.MaxValue), (x, y) => new SourceFileId(x, y) )
                                                                         .ToList()
                                                    })
                                .Where(mfi => mfi.FilesId.Count > 0)
                                .ToDictionary(mfi => mfi.Module, mfi => mfi.FilesId)
                                ;

            return modFile;
        }

        /// <summary>
        /// Translates a two-level module -> func -> FuncInfo dictionary to the dwjson data model
        /// </summary>
        public static IEnumerable<ModuleSpec> ModFunToTrace(Dictionary< string, Dictionary< string, FuncInfo > > modfun)
        {
            var mfiles = SourceFilesByModule(modfun);
            var mfilesDWJSON = mfiles.Select(kv => new { Module = kv.Key, 
                                                         Files = kv.Value.Select(fi => new FileIDMapSpec {id = fi.Id, file = fi.SourceFile}).ToList()
                                                       })
                                     .ToDictionary(k => k.Module, v => v.Files);
                                     
            /*
            This means that, to find the id that has been assigned to a filename in this structure we need something like:
            var idx = mfilesDWJSON[<module>].FindIndex(fi => fi.file == <filename>);
            var y = mfilesDWJSON[<module>][x];
            */
            
            // Generate LineSpecs for each function in the module
            List<ModuleSpec> modulesInTrace = new List<ModuleSpec>();
            foreach (var mf in modfun.Select(mfd => new { Module = mfd.Key, Fnames = mfd.Value.Select(ffi => ffi.Value).ToList()})) {

                ModuleSpec currentModule = new ModuleSpec {
                    name = mf.Module
                };
                List<FunctionSpec> funcsInCurrentModule = new List<FunctionSpec>();
                foreach (var ff in mf.Fnames) {
                    //FunctionSpec currrent = new FunctionSpec (ff.FunctionName, ff.Base, ff.Size);
                    FunctionSpec current = new FunctionSpec (ff.FunctionName, 0,0);

                    // look up information on function
                    if (mfilesDWJSON.ContainsKey(mf.Module)) {
                        int idx = mfilesDWJSON[mf.Module].FindIndex(fi => fi.file == ff.SourceFile);
                        if (idx >= 0) {
                            var idForFun = mfilesDWJSON[mf.Module][idx].id;
                            LineSpec found = new LineSpec {
                                fileId = idForFun,
                                offset = 10,
                                lineBegin = (int)(ff.LineNumber ?? 0),
                                lineEnd = (int)(ff.LineNumber ?? 1),
                                columnBegin = 0,
                                columnEnd = 1
                            };
                            current.lines = Utils.Emit<LineSpec>(found).ToList();
                        }
                    }
                    funcsInCurrentModule.Add(current);
                }
                currentModule.ranges = funcsInCurrentModule;
                if (mfilesDWJSON.ContainsKey(currentModule.name)) { // maybe should use the description used thus far
                    currentModule.fileIdMapping = mfilesDWJSON[currentModule.name];
                }
                modulesInTrace.Add(currentModule);
            }

#if false
                string json = JsonConvert.SerializeObject(modulesInTrace, Formatting.Indented, new JsonSerializerSettings {
                    NullValueHandling = NullValueHandling.Ignore});
#endif

            return modulesInTrace;
        }
    }

    public class AddressTranslator {
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

    public class SourceFileId
    {
            public string SourceFile { get; set; }
            public int Id { get; set; }
            public SourceFileId(string _sourceFile, int _id) {
                SourceFile = _sourceFile;
                Id = _id;
            }
    }
   
}
