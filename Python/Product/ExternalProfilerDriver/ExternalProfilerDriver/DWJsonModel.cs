using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ExternalProfilerDriver
{
    public class LongInt
    {
        public LongInt(long _h, long _l) { h = _h; l = _l; }
        public long h { get; set; }
        public long l { get; set; }
    }

    public class TimeSpec
    {
        public LongInt begin { get; set; }
        public LongInt duration { get; set; }
    }

    public class ProcessorSpec
    {
        public int logicalCount { get; set; }
        public long speedInMHz { get; set; }
        public int pointerSizeInBytes { get; set; }
        public LongInt highestUserAddress { get; set; }
    }

    public class ProcessSpec
    {
        public string name { get; set; }
        public int id { get; set; }
        public LongInt begin { get; set; }
        public LongInt end { get; set; }
        public bool isTarget { get; set; }
        public bool isUser { get; set; }
        public IList<int> moduleIDs { get; set; }
        public IList<ThreadSpec> threads { get; set; }
    }

    public class FrameInfo
    {
        public LongInt timestamp { get; set; }
        public IList<LongInt> frameIPs { get; set; }
    }

    public class FunctionSpec
    {
        public string name { get; set; }
        [JsonProperty("base")]
        public LongInt baseX { get; set; }
        public LongInt size { get; set; }

        public FunctionSpec(string _name, long _base, long _size)
        {
            name = _name;
            baseX = new LongInt(0, _base);
            size = new LongInt(0, _size);
        }
    }

    public class ModuleSpec
    {
        public string name { get; set; }
        public int id { get; set; }
        public LongInt begin { get; set; }
        public LongInt end { get; set; }
        [JsonProperty("base")]
        public LongInt baseX { get; set; }
        public LongInt size { get; set; }
        public IList<FunctionSpec> ranges { get; set; }
        public ModuleSpec(string _name, IEnumerable<FunctionSpec> _ranges)
        {
            name = _name;
            ranges = _ranges.ToList();
        }
    }

    public class ThreadSpec
    {
        public int id { get; set; }
        public LongInt begin { get; set; }
        public LongInt end { get; set; }
        public IList<FrameInfo> stacks { get; set; }
    }

    public class Trace
    {
        public TimeSpec totalTimeRange { get; set; }
        public string name { get; set; }
        public ProcessorSpec processor { get; set; }
        public IList<ProcessSpec> processes { get; set; }

        public string modules { get; set; }
    }

    public class CPUUtilTrace
    {
        public LongInt beginTime;
        public LongInt duration;
        public IList<ValueTrace> counters;
    }

    public class ValueTrace
    {
        public string id;
        public IList<CPUSample> p;
        public ValueTrace(IEnumerable<CPUSample> samples)
        {
            id = "DiagnosticsHub.Counters.Process.CPU";
            p = samples.ToList();
        }
    }

    public class CPUSample
    {
        public LongInt t;
        public float v;
        public CPUSample(LongInt _t, float _v)
        {
            t = _t;
            v = _v;
        }
    }
}
