using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExternalProfilerDriverTest
{
    [TestClass]
    public class VTuneInvokerTest
    {
#if false
        [TestMethod]
        public void TestVTuneHotspotSpec()
        {
            VTuneCollectSpec spec = new VTuneCollectHotspotsSpec();
            Assert.AreEqual("hotspots", spec.AnalysisName);
        }
        
        [TestMethod]
        public void TestVTuneCallstackSpec()
        {
            VTuneReportSpec spec = new VTuneCollectCallstacksSpec();
            Assert.AreEqual("-report callstacks", spec.CLISpec);
        }

        [TestMethod]
        public void TestVTuneTimeSpec()
        {
            VTuneReportSpec spec = new VTuneCPUUtilizationSpec();
            Assert.AreEqual("-report time", spec.CLISpec);
        }
#endif

    }
}
