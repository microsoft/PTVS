using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using ExternalProfilerDriver;

namespace ExternalProfilerDriverTest
{
    [TestClass]
    public class PerformanceSampleTest
    {
        [TestMethod]
        public void PerformanceSampleCtorTest()
        {
            PerformanceSample p = new PerformanceSample("foo", "1.2", "module", "foo_complete", "??", "0x01");
            Assert.AreEqual("foo", p.Function);
        }
    }
}
