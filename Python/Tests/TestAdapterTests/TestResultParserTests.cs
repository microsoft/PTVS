using System;
using System.Text;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.PythonTools.TestAdapter.Pytest;

namespace TestAdapterTests {
    /// <summary>
    /// Summary description for TestResultParserTests
    /// </summary>
    [TestClass]
    public class TestResultParserTests {
        public TestResultParserTests() {
            //
            // TODO: Add constructor logic here
            //
        }

        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext {
            get {
                return testContextInstance;
            }
            set {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

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
