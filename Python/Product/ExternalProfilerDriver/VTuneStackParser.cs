using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.PythonTools.Profiling.ExternalProfilerDriver
{
    public class PerformanceSample
    {
        public string Function { get; }
        public float CPUTime { get; }
        public string Module { get; }
        public string FunctionFull { get; }
        public string SourceFile { get; }
        public string StartAddress { get; }

        public PerformanceSample(string function, string cpuTime, string module, string functionFull, string sourceFile, string startAddress)
        {
            Function = function;
            CPUTime = Single.Parse(cpuTime);
            Module = module;
            FunctionFull = functionFull;
            SourceFile = sourceFile;
            StartAddress = startAddress;
        }
    }

    public class SampleWithTrace
    {
        private List<List<PerformanceSample>> _stacks = new List<List<PerformanceSample>>();
        public PerformanceSample TOSFrame { get; }
        public IEnumerable<IEnumerable<PerformanceSample>> Stacks
        {
            get
            {
                foreach (var s in _stacks)
                {
                    yield return s.AsEnumerable();
                }
            }
        }

        public SampleWithTrace(PerformanceSample sample)
        {
            TOSFrame = sample;
        }

        public void AddStack(List<PerformanceSample> stack)
        {
            _stacks.Add(stack);
        }
        public IEnumerable<PerformanceSample> AllSamples()
        {
            yield return TOSFrame; // assumes this is not null
            foreach (var stack in Stacks)
            {
                foreach (var frame in stack)
                {
                    yield return frame;
                }
            }
        }
    }

    static class VTuneStackParser
    {
        public static IEnumerable<string> ReadFromFile(string filePath)
        {
            string line;
            using (var reader = File.OpenText(filePath))
            {
                while ((line = reader.ReadLine()) != null)
                {
                    yield return line;
                }
            }
        }

        public static string RemovePrePosComma(string str)
        {

            if (str.Length > 0)
            {
                if (str[0] == '"') { str = str.Substring(1, str.Length - 1); }
            }
            if (str.Length > 0)
            {
                if (str[str.Length - 1] == '"') { str = str.Substring(0, str.Length - 1); }
            }
            return str;
        }

        public static IEnumerable<SampleWithTrace> ParseFromStream(this IEnumerable<string> seq)
        {
            Regex startsWithComma = new Regex("^(,+)(.*)$");

            SampleWithTrace current = null;
            List<PerformanceSample> currentStack = null;
            bool atEnd = false;

            foreach (var l in seq)
            {
                Match m = startsWithComma.Match(l);
                if (!m.Success)
                {
                    if (atEnd == true)
                    {
                        if (currentStack != null)
                        {
                            current.AddStack(currentStack);
                            currentStack = null;
                        }
                        yield return current;
                    }
                    atEnd = false;

                    // should assert record is comma-separated, seven-field, second one empty
                    var fields = l.Split(',');
                    /* Function, CPUTime, Module, FunctionFull, SourceFile, StartAddress */
                    current = new SampleWithTrace(new PerformanceSample(fields[0], fields[2], fields[3], fields[4], fields[5], fields[6]));
                }
                else
                {
                    // assert m.Groups.Count is 3
                    if (m.Groups[1].Length == 1)
                    {
                        if (atEnd == true || currentStack == null)
                        {
                            if (currentStack != null)
                            {
                                current.AddStack(currentStack);
                            }
                            currentStack = new List<PerformanceSample>();
                        }
                        atEnd = false;
                        var fields = m.Groups[2].Value.Split(',');
                        /* Function, CPUTime, Module, FunctionFull, SourceFile, StartAddress */
                        currentStack.Add(new PerformanceSample(fields[0], fields[1], fields[2], fields[3], fields[5], fields[5]));
                    }
                    else
                    {
                        // verify that the only other allowed value for Groups[1].Length is 6?
                        atEnd = true;
                    }
                }
            }
                        
            if (current != null)
            {
                if (currentStack != null)
                {
                    current.AddStack(currentStack);
                }
                yield return current;
            }          
        }
    }
}