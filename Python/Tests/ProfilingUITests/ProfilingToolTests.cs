// Python Tools for Visual Studio
// Copyright(c) 2016 Intel Corporation
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.PythonTools.Profiling;

namespace ProfilingUITests {
    [TestClass]
    public class ProfilingToolTests {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void BuildCollectCommand()
        {
            VTuneCollectCommand c = new VTuneCollectCommand(VTuneCollectCommand.collectType.hotspots);
            Assert.AreEqual("C:\\Program Files (x86)\\IntelSWTools\\VTune Amplifier XE 2017\\bin32\\amplxe-cl.exe -collect hotspots", c.get());
            c.setDuration(5);
            c.setUserDataDir("C:\\temp\\out");
            Assert.AreEqual("C:\\Program Files (x86)\\IntelSWTools\\VTune Amplifier XE 2017\\bin32\\amplxe-cl.exe -user-data-dir=C:\\temp\\out -d 5 -collect hotspots", c.get());
        }

        [TestMethod]
        public void BuildReportCommand()
        {
            VTuneReportCommand c = new VTuneReportCommand(VTuneReportCommand.collectType.hotspots);
            Assert.AreEqual("C:\\Program Files (x86)\\IntelSWTools\\VTune Amplifier XE 2017\\bin32\\amplxe-cl.exe -report hotspots -report-output=report.csv -format=csv -csv-delimiter=,", c.get());
            c.setResultDir("r000hs");
            Assert.AreEqual("C:\\Program Files (x86)\\IntelSWTools\\VTune Amplifier XE 2017\\bin32\\amplxe-cl.exe -r r000hs -report hotspots -report-output=report.csv -format=csv -csv-delimiter=,", c.get());

        }
    }
}