using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestExternalProfilerDriver
{
    [TestClass]
    public class TestPerformanceSample
    {
        [TestMethod]
        public void TestPerformanceSampleCtor()
        {
            PerformanceSample p = new PerformanceSample("foo", "1.2", "module", "foo_complete", "??", "0x01");
            Assert.AreEqual("foo", p.Function);
        }
    }
}
