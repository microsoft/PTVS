using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

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
            var samples = ParseFromFile(filename);

            var modFunDictionary = samples.SelectMany(sm => sm.AllSamples())
                                          .Select(p => new { Module = p.Module, Function = p.Function })
                                          .GroupBy(t => t.Module)
                                          .Select(g => new { Module = g.Key, Functions = g.Select(gg => gg.Function).Distinct() })
                                    ;

            var ffs = modFunDictionary
             .Take(2)
             .Select(mf =>
             {
                 SequenceBaseSize seq = new SequenceBaseSize(0, 10);
                 return new ModuleSpec(mf.Module, mf.Functions.Zip(seq.Generate(), (f, bs) => new FunctionSpec(f, bs.Base, bs.Size)));
             });


            string json = JsonConvert.SerializeObject(ffs, Formatting.Indented);
            Console.WriteLine($"JSON serialization: << {json} >>");

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

    }
}
