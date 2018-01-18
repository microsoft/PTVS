using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

using Newtonsoft.Json;

namespace ExternalProfilerDriver
{
    public class BaseSizeTuple
    {
        public long Base { get; set; }
        public long Size { get; set; }
        public BaseSizeTuple(long _base, long _size)
        {
            Base = _base;
            Size = _size;
        }
    }

    public class SequenceBaseSize
    {
        private long _current;
        private long _size; // should this be constant?
        public SequenceBaseSize(long start = 0, long size = 10) { _current = start; _size = size; }
        public IEnumerable<BaseSizeTuple> Generate()
        {
            while (true)
            {
                yield return new BaseSizeTuple(_current, _size);
                _current += _size + 1;
            }
        }
    }

    class VTuneToDWJSON
    {
        /// <summary>
        /// <paramref name="filename"/>
        /// </summary>
        /// <param name="filename">The filename with the callstack report</param>
        public static void CSReportToDWJson(string filename)
        {
            if (!File.Exists(filename))
            {
                Console.WriteLine("Cannot find file to process!");
                return;
            }
            var samples = ParseFromFile(filename);

            var times = samples.Select(x => x.TOSFrame.CPUTime);
            var total = times.Sum();
            var fractional = times.Select(x => (x / total) * 100).ToList();

            var modFunDictionary = samples.SelectMany(sm => sm.AllSamples())
                                          .Select(p => new { Module = p.Module, Function = p.Function })
                                          .GroupBy(t => t.Module)
                                          .Select(g => new { Module = g.Key, Functions = g.Select(gg => gg.Function).Distinct() })
                                    ;


            // Create a two-level dictionary module -> (function -> (base, size))
            var mfdd = modFunDictionary.Select(x => new {
                Module = x.Module,
                Functions = x.Functions.Zip((new SequenceBaseSize()).Generate(), (f, b) => new { Function = f, BaseSize = b })
                                                                                 .ToDictionary(t => t.Function, t => t.BaseSize)
            })
                                       .ToDictionary(od => od.Module, od => od.Functions);

            if (mfdd.Count <= 0)
            {
                Console.WriteLine("Couldn't build the module/function dictionary, can't figure out why");
                return;
            }

            // should zip on a sequence generator
            var mods = mfdd.Zip(SequenceGenerator(1), (x, y) => new ModuleSpec()
            {
                name = x.Key,
                id = y,
                begin = new LongInt(0, 0), // should build these according to mfdd (i.e., argument x)
                end = new LongInt(0, 10000), // not sure why 2500 is the smallest number than seems to work
                baseX = new LongInt(0, (y - 1) * 1000),
                size = new LongInt(0, 300),
                ranges = x.Value.Select(xx => new FunctionSpec(xx.Key, xx.Value.Base, xx.Value.Size)).ToList()
            });
            var modBase = mods.ToDictionary(x => x.name, x => x.baseX);

            AddressTranslator tr = new AddressTranslator(modBase, mfdd);

            int startime = 2500; // the base is important, as it's coordinated with the modules `end`
            int stepsize = 1;
            var chains = SequenceGenerator(0).Zip(samples.First().Stacks, (x, y) => new FrameInfo
            {
                timestamp = new LongInt(0, startime + stepsize * x),
                frameIPs = y.Select(z => tr.Translate(z.Module, z.Function)).ToList()
            });

            ProcessSpec proc = new ProcessSpec
            {
                name = "python36.dll",
                id = 1234,
                begin = new LongInt(0, 1000),
                end = new LongInt(0, 8000),
                isTarget = true,
                isUser = true,
                moduleIDs = SequenceGenerator(1).Take(mods.Count()).ToList()
            };
            List<ProcessSpec> processes = new List<ProcessSpec>(); // TODO -- is there a literal for this?
            processes.Add(proc);

            ThreadSpec thread = new ThreadSpec()
            {
                id = 1,
                begin = new LongInt(0, 2000),
                end = new LongInt(0, 3000),
                stacks = chains.ToList()
            };
            List<ThreadSpec> threads = new List<ThreadSpec>();
            threads.Add(thread);
            proc.threads = threads;

            var trace = new Trace
            {
                totalTimeRange = new TimeSpec
                {
                    begin = new LongInt(0, 0),
                    //duration = new LongInt(0, (int)(total * 1000))
                    duration = new LongInt(0, 10000)
                },
                name = "machine-name",
                processor = new ProcessorSpec
                {
                    logicalCount = 4,
                    speedInMHz = 2670,
                    pointerSizeInBytes = 4,
                    highestUserAddress = new LongInt(0, 2147418111)
                },
                processes = processes,
                modules = mods.ToList()
            };

            string json = JsonConvert.SerializeObject(trace, Formatting.Indented);

            StreamWriter writer = File.CreateText(outfname);
            writer.WriteLine(json);
            writer.Close();
        }

        public static IEnumerable<SampleWithTrace> ParseFromFile(string filename)
        {
            var samples = VTuneStackParser.ReadFromFile(filename)
                                          .Skip(1)
                                          .Select(s => String.Join(",", s.Split(',')
                                          .Select(VTuneStackParser.RemovePrePosComma)))
                                          .ParseFromStream();
            return samples;
        }

        public static IEnumerable<int> SequenceGenerator(int start = 0)
        {
            int i = start;
            while (true)
            {
                yield return i;
                i++;
            }
        }

        public static void CPUReportToDWJson(string filename, string outfname)
        {
            if (!File.Exists(filename))
            {
                return;
            }

            var cpuRecords = VTuneStackParser.ReadFromFile(filename)
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
            long totalTime = 10000;
            long stepSize = totalTime / steps;

            List<ValueTrace> vts = new List<ValueTrace>();
            vts.Add(new ValueTrace(SequenceGenerator().Take(steps).Zip(cpuRecords, (x, y) => new CPUSample(new LongInt(0, x * stepSize), (float)(y.CPUUtil)))));

            CPUUtilTrace trace = new CPUUtilTrace
            {
                beginTime = new LongInt(0, 0),
                duration = new LongInt(0, totalTime),
                counters = vts
            };

            string json = JsonConvert.SerializeObject(trace, Formatting.Indented);

            // var fs = new FileStream(@"C:\users\perf\Sample2.counters", FileMode.Create);
            var fs = new FileStream(outfname, FileMode.Create);
            using (StreamWriter writer = new StreamWriter(fs, Encoding.Unicode)) // encoding in Unicode here is key
            {
                writer.WriteLine(json);
            }
        }

    }

    public class AddressTranslator
    {
        private Dictionary<string, Dictionary<string, BaseSizeTuple>> modfundict;
        private Dictionary<string, LongInt> modindex;

        public AddressTranslator(Dictionary<string, LongInt> _modindex, Dictionary<string, Dictionary<string, BaseSizeTuple>> _modfundict)
        {
            modfundict = _modfundict;
            modindex = _modindex;
        }

        public LongInt Translate(string module, string function)
        {
            //Console.WriteLine($"- Being asked to translate {module}/{function}");
            return new LongInt(0, modindex[module].l + modfundict[module][function].Base + 1);
        }

    }
}
