using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.PythonTools.Profiling;

namespace ProfilingUITests {
    [TestClass]
    public class ProfilingCommandTests {
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
            Assert.AreEqual("C:\\Program Files (x86)\\IntelSWTools\\VTune Amplifier XE 2017\\bin32\\amplxe-cl.exe -report hotspots", c.get());
            c.setResultDir("r000hs");
            Assert.AreEqual("C:\\Program Files (x86)\\IntelSWTools\\VTune Amplifier XE 2017\\bin32\\amplxe-cl.exe -report hotspots -r r000hs", c.get());

        }
    }
}