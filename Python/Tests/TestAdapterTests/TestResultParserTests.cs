using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.PythonTools.TestAdapter.Pytest;

namespace TestAdapterTests {

    [TestClass]
    public class TestResultParserTests {

        [TestMethod]
        public void CreatePytestId_FuncInsideClass() {
            Assert.AreEqual(".\\test2.py::Test_test2::test_A", TestResultParser.CreatePytestId("test2.py", "test2.Test_test2", "test_A"));
        }

        [TestMethod]
        public void CreatePytestId_GlobalFunc() {
            Assert.AreEqual(".\\test_sample.py::test_answer", TestResultParser.CreatePytestId("test_sample.py", "test_sample", "test_answer"));
        }

        [TestMethod]
        public void CreatePytestId_ClassFuncWithRelativeFilename() {
            Assert.AreEqual(".\\package1\\packageA\\test1.py::Test_test1::test_A", TestResultParser.CreatePytestId("package1\\packageA\\test1.py", "package1.packageA.test1.Test_test1", "test_A"));
        }

    }
}
