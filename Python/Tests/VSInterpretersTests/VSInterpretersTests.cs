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

namespace VSInterpretersTests
{
    [TestClass]
    public class VSInterpretersTests
    {
        [ClassInitialize]
        public static void DoDeployment(TestContext context)
        {
            AssertListener.Initialize();
        }

        private static readonly List<string> _tempFiles = new List<string>();

        [ClassCleanup]
        public static void RemoveFiles()
        {
            foreach (var file in _tempFiles)
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                }
            }
        }

        private static string CompileString(string csharpCode, string outFile)
        {
            var provider = new Microsoft.CSharp.CSharpCodeProvider();
            var parameters = new System.CodeDom.Compiler.CompilerParameters
            {
                OutputAssembly = outFile,
                GenerateExecutable = false,
                GenerateInMemory = false
            };
            parameters.ReferencedAssemblies.Add(typeof(ExportAttribute).Assembly.Location);
            parameters.ReferencedAssemblies.Add(typeof(IPythonInterpreterFactoryProvider).Assembly.Location);
            parameters.ReferencedAssemblies.Add(typeof(InterpreterConfiguration).Assembly.Location);
            var result = provider.CompileAssemblyFromSource(parameters, csharpCode);
            if (result.Errors.HasErrors)
            {
                foreach (var err in result.Errors)
                {
                    Console.WriteLine(err);
                }
            }

            if (!File.Exists(outFile))
            {
                Assert.Fail("Failed to compile {0}", outFile);
            }
            _tempFiles.Add(outFile);
            return outFile;
        }

        private static string FactoryProviderSuccessPath
        {
            get
            {
                var path = Path.ChangeExtension(Path.GetTempFileName(), "dll");

                if (File.Exists(path))
                {
                    return path;
                }

                return CompileString(@"
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.PythonTools.Interpreter;

namespace FactoryProviderSuccess {
    [Export(typeof(IPythonInterpreterFactoryProvider))]
    [InterpreterFactoryId(""Success"")]
    public class FactoryProviderSuccess : IPythonInterpreterFactoryProvider {
        public IPythonInterpreterFactory GetInterpreterFactory(string id) {
            return null;
        }
        public IEnumerable<InterpreterConfiguration> GetInterpreterConfigurations() { yield break; }
        public event EventHandler InterpreterFactoriesChanged { add { } remove { } }
        public object GetProperty(string id, string propName) { return null; }
    }
}", path);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void ProviderLoadLog_Success()
        {
            var path = FactoryProviderTypeLoadErrorPath;

            var catalogLog = new MockLogger();
            var container = InterpreterCatalog.CreateContainer(
                catalogLog,
                typeof(IInterpreterOptionsService).Assembly.Location,
                typeof(IInterpreterRegistryService).Assembly.Location,
                GetType().Assembly.Location
            );

            var log = container.GetExport<MockLogger>().Value;
            var service = container.GetExportedValue<IInterpreterOptionsService>();
            var registry = container.GetExportedValue<IInterpreterRegistryService>();

            foreach (var interpreter in registry.Configurations)
            {
                Console.WriteLine(interpreter);
            }

            foreach (var item in log.AllItems)
            {
                Console.WriteLine(item);
            }

            Assert.AreEqual(0, log.AllItems.Count);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void ProviderLoadLog_FileNotFound()
        {
            var catalogLog = new MockLogger();

            var path = Path.ChangeExtension(Path.GetTempFileName(), "dll");
            File.Delete(path);
            Assert.IsFalse(File.Exists(path));

            var container = InterpreterCatalog.CreateContainer(
                catalogLog,
                typeof(IInterpreterOptionsService).Assembly.Location,
                typeof(IInterpreterRegistryService).Assembly.Location,
                GetType().Assembly.Location,
                path
            );

            var log = container.GetExport<MockLogger>().Value;
            var service = container.GetExportedValue<IInterpreterOptionsService>();
            var registry = container.GetExportedValue<IInterpreterRegistryService>();

            foreach (var interpreter in registry.Configurations)
            {
                Console.WriteLine(interpreter);
            }

            var error = catalogLog.AllItems.Single();
            Assert.IsTrue(error.StartsWith("Failed to load interpreter provider assembly"));
            Assert.AreNotEqual(-1, error.IndexOf("System.IO.FileNotFoundException: "));
        }

        private static string FactoryProviderCorruptPath
        {
            get
            {
                var path = Path.ChangeExtension(Path.GetTempFileName(), "dll");

                if (File.Exists(path))
                {
                    return path;
                }

                using (var src = new FileStream(FactoryProviderSuccessPath, FileMode.Open, FileAccess.Read))
                using (var dest = new FileStream(path, FileMode.Create, FileAccess.Write))
                {
                    var rnd = new Random();
                    var buffer = new byte[4096];
                    int read = src.Read(buffer, 0, buffer.Length);
                    while (read > 0)
                    {
                        for (int count = 64; count > 0; --count)
                        {
                            var i = rnd.Next(buffer.Length);
                            buffer[i] = (byte)(buffer[i] + rnd.Next(byte.MaxValue));
                        }

                        dest.Write(buffer, 0, read);
                        read = src.Read(buffer, 0, buffer.Length);
                    }
                }

                return path;
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void ProviderLoadLog_CorruptImage()
        {
            var catalogLog = new MockLogger();

            var path = Path.ChangeExtension(Path.GetTempFileName(), "dll");
            File.Delete(path);
            Assert.IsFalse(File.Exists(path));

            var container = InterpreterCatalog.CreateContainer(
                catalogLog,
                typeof(IInterpreterOptionsService).Assembly.Location,
                typeof(IInterpreterRegistryService).Assembly.Location,
                GetType().Assembly.Location,
                FactoryProviderCorruptPath
            );

            var log = container.GetExport<MockLogger>().Value;
            var service = container.GetExportedValue<IInterpreterOptionsService>();
            var registry = container.GetExportedValue<IInterpreterRegistryService>();

            foreach (var interpreter in registry.Configurations)
            {
                Console.WriteLine(interpreter);
            }

            var error = catalogLog.AllItems.Single();
            Assert.IsTrue(error.StartsWith("Failed to load interpreter provider assembly"));
            Assert.AreNotEqual(-1, error.IndexOf("System.BadImageFormatException: "));
        }


        private static string FactoryProviderTypeLoadErrorPath
        {
            get
            {
                var path = Path.ChangeExtension(Path.GetTempFileName(), "dll");

                if (File.Exists(path))
                {
                    return path;
                }

                return CompileString(@"
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.PythonTools.Interpreter;

namespace FactoryProviderTypeLoadException {
    [Export(typeof(IPythonInterpreterFactoryProvider))]
    [InterpreterFactoryId(""TypeError"")]
    public class FactoryProviderTypeLoadException : IPythonInterpreterFactoryProvider {
        static FactoryProviderTypeLoadException() {
            throw new Exception();
        }
        public IPythonInterpreterFactory GetInterpreterFactory(string id) {
            return null;
        }
        public IEnumerable<InterpreterConfiguration> GetInterpreterConfigurations() { yield break; }
        public event EventHandler InterpreterFactoriesChanged { add { } remove { } }
        public object GetProperty(string id, string propName) { return null; }
    }
}", path);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void ProviderLoadLog_TypeLoadException()
        {
            var path = FactoryProviderTypeLoadErrorPath;

            var catalogLog = new MockLogger();
            var container = InterpreterCatalog.CreateContainer(
                catalogLog,
                FactoryProviderTypeLoadErrorPath,
                typeof(IInterpreterOptionsService).Assembly.Location,
                typeof(IInterpreterRegistryService).Assembly.Location,
                GetType().Assembly.Location
            );

            var log = container.GetExport<MockLogger>().Value;
            var service = container.GetExportedValue<IInterpreterOptionsService>();
            var registry = container.GetExportedValue<IInterpreterRegistryService>();

            foreach (var interpreter in registry.Configurations)
            {
                Console.WriteLine(interpreter);
            }

            bool isMatch = false;
            foreach (var msg in log.AllItems)
            {
                Console.WriteLine(msg);
                isMatch |= new Regex(@"Failed to get interpreter factory value:.*System\.ComponentModel\.Composition\.CompositionException").IsMatch(msg);
            }

            Assert.IsTrue(isMatch);

            Assert.IsNotNull(registry.Configurations.FirstOrDefault());
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void ProviderLoadLog_SuccessAndFailure()
        {
            var path = FactoryProviderTypeLoadErrorPath;

            var catalogLog = new MockLogger();
            var container = InterpreterCatalog.CreateContainer(
                catalogLog,
                FactoryProviderTypeLoadErrorPath,
                FactoryProviderSuccessPath,
                typeof(IInterpreterOptionsService).Assembly.Location,
                typeof(IInterpreterRegistryService).Assembly.Location,
                GetType().Assembly.Location
            );

            var log = container.GetExport<MockLogger>().Value;
            var service = container.GetExportedValue<IInterpreterOptionsService>();
            var registry = container.GetExportedValue<IInterpreterRegistryService>();

            foreach (var interpreter in registry.Configurations)
            {
                Console.WriteLine(interpreter);
            }

            foreach (var item in log.AllItems)
            {
                Console.WriteLine(item);
            }

            Assert.AreEqual(1, log.AllItems.Count);
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void InvalidInterpreterVersion()
        {
            try
            {
                var lv = new Version(1, 0).ToLanguageVersion();
                Assert.Fail("Expected InvalidOperationException");
            }
            catch (InvalidOperationException)
            {
            }

            try
            {
                InterpreterFactoryCreator.CreateInterpreterFactory(new VisualStudioInterpreterConfiguration(
                    Guid.NewGuid().ToString(),
                    "Test Interpreter",
                    version: new Version(1, 0)
                ));
                Assert.Fail("Expected ArgumentException");
            }
            catch (ArgumentException ex)
            {
                // Expect version number in message
                AssertUtil.Contains(ex.Message, "1.0");
            }
        }
    }

    [Export(typeof(IInterpreterLog))]
    [Export(typeof(MockLogger))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    class MockLogger : ICatalogLog, IInterpreterLog
    {
        public readonly List<string> AllItems = new List<string>();

        public void Log(string msg)
        {
            AllItems.Add(msg);
        }
    }

}
