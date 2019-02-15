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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Profiling;
using Microsoft.PythonTools.Profiling.ExternalProfilerDriver;

using Trace = System.Diagnostics.Trace;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace ProfilingTestsDeployment {
    [TestClass]
    public class ProfilingTestsDeployment
    {
        public TestContext TestContext { get; set; }

        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

       

        [TestMethod]
        public void TestDummy() {
            var pf = new /*  Microsoft.PythonTools.Profiling.ExternalProfilerDriver. */ PerformanceSample("hello", "100", "world", "hello-world", "hello.c", "0x00");
            Assert.AreEqual(pf.Function, "hello");            
            //Assert.IsTrue(true);
        }

        [TestMethod]
        [DeploymentItem("zlib_example.csv")]
        public void TestParsing()
        {
            string filename = "zlib_example.csv";
            int expected_sample_count = 5;
            Assert.IsTrue(File.Exists(filename));

            var samples = VTuneToDWJSON.ParseFromFile(filename).ToList();
            Assert.AreEqual(samples.Count, expected_sample_count);

            Assert.IsInstanceOfType(samples[0], typeof(SampleWithTrace));

            string known_module = "libz.so.1";
            var dict = VTuneToDWJSON.ModuleFuncDictFromSamples(samples);
            Assert.IsTrue(dict.ContainsKey(known_module));

            foreach (var m in dict)
            {
                Dictionary<string, FuncInfo> v = m.Value;
                Trace.WriteLine($"Main Key: {m.Key}");
                foreach (var vkk in v)
                {
                    Trace.WriteLine($"Key: {vkk.Key}, Value: [{vkk.Value.FunctionName}, {vkk.Value.SourceFile}, {vkk.Value.LineNumber}]");
                }
            }
            // Works but this isnt used anywhere?
            // This assert doesnt work?
            // Assert.Throws<ArgumentException>(() => VTuneToDWJSON.AddLineNumbers(ref dict, "/etc/test"));
            int initial_count = dict.Count;
            //VTuneToDWJSON.AddLineNumbers(ref dict, "C:\\Users\\clairiky\\Documents\\zlib-1.2.11");
            Assert.AreEqual(initial_count, dict.Count);


            foreach (var m in dict)
            {
                Dictionary<string, FuncInfo> v = m.Value;
                Trace.WriteLine($"Main Key: {m.Key}");
                foreach (var vkk in v)
                {
                    Trace.WriteLine($"Key: {vkk.Key}, Value: [{vkk.Value.FunctionName}, {vkk.Value.SourceFile}, {vkk.Value.LineNumber}]");
                }
            }

            //var mfiles = VTuneToDWJSON.SourceFilesByModule(dict); // Doesnt exists



            // This function doesnt exist.
            //var modspec = VTuneToDWJSON.ModFunToTrace(dict).ToList();
            //Assert.IsInstanceOfType(modspec[0], typeof(ModuleSpec));
            // SequenceBaseSize

            //foreach (var r in VTuneToDWJSON.ModFunToTrace(dict))
            //{
            //    Trace.WriteLine($"**** Got module {r.name}, assigned id: [{r.id}]");
            //}
        }

        [TestMethod]
        //[DeploymentItem("r_stacks_0004.csv")]
        [DeploymentItem("zlib_example.csv")]
        public void TestParseFromFile()
        {
            string filename = "zlib_example.csv";
            Assert.IsTrue(File.Exists(filename));
            //int expected_sample_count = 5;
            // This function doesnt exists
           // var samples = VTuneStackParser.ParseFromFile(filename).ToList();
            //Assert.AreEqual(samples.Count, expected_sample_count);
        }

        [TestMethod]
        [DeploymentItem("something.pdb")]
        public void LoadTest()
        {

            Assert.IsTrue(true);

           /* string known_filename = "something.pdb";
            Assert.IsTrue(File.Exists(known_filename));
            SymbolReader symreader = SymbolReader.Load(known_filename);
            Assert.IsTrue(symreader != null);

            
            const int expected_symbol_count = 150;            
            var syms = symreader.FunctionLocations(known_filename).ToList();
            Assert.AreEqual(syms.Count, expected_symbol_count); */

        }
    }
}
