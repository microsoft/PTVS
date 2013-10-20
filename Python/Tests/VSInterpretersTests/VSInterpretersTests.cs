/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Mocks;
using TestUtilities.Python;

namespace VSInterpretersTests {
    [TestClass]
    public class VSInterpretersTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy(includeTestData: false);
        }

        private static readonly List<string> _tempFiles = new List<string>();

        [ClassCleanup]
        public static void RemoveFiles() {
            foreach (var file in _tempFiles) {
                try {
                    File.Delete(file);
                } catch {
                }
            }
        }

        [TestMethod, Priority(0)]
        public void MinimumAssembliesLoaded() {
            var assembliesBefore = new HashSet<Assembly>(AppDomain.CurrentDomain.GetAssemblies());
            // This assembly is probably already loaded, but let's pretend that
            // we've loaded it again for this test.
            assembliesBefore.Remove(typeof(IInterpreterOptionsService).Assembly);

            var catalog = new AssemblyCatalog(typeof(IInterpreterOptionsService).Assembly);
            var container = new CompositionContainer(catalog);
            var service = container.GetExportedValue<IInterpreterOptionsService>();

            Assert.IsInstanceOfType(service, typeof(InterpreterOptionsService));

            // Ensure these assemblies were loaded.
            var expectedAssemblies = new HashSet<string> {
                "Microsoft.PythonTools.Analysis",
                "Microsoft.PythonTools.VSInterpreters",
                "Microsoft.PythonTools.IronPython.Interpreter"
            };

            // Ensure these assemblies were not loaded. In the out-of-VS
            // scenario, we cannot always resolve these and so will crash.
            // For tests, they are always available, and when installed they may
            // always be available in the GAC, but we want to ensure that they
            // are not loaded anyway.
            var notExpectedAssemblies = new HashSet<string> {
                "Microsoft.PythonTools",
                "Microsoft.VisualStudio.ReplWindow"
            };

            Console.WriteLine("Loaded assemblies:");
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                if (!assembliesBefore.Remove(assembly)) {
                    var name = assembly.GetName().Name;
                    Console.WriteLine("{0}: {1}", name, assembly.FullName);
                    expectedAssemblies.Remove(name);
                    Assert.IsFalse(notExpectedAssemblies.Remove(name), assembly.FullName + " should not have been loaded");
                }
            }

            Assert.AreEqual(0, expectedAssemblies.Count, "Was not loaded: " + string.Join(", ", expectedAssemblies));
        }

        private static string CompileString(string csharpCode, string outFile) {
            var provider = new Microsoft.CSharp.CSharpCodeProvider();
            var parameters = new System.CodeDom.Compiler.CompilerParameters {
                OutputAssembly = outFile,
                GenerateExecutable = false,
                GenerateInMemory = false
            };
            parameters.ReferencedAssemblies.Add(typeof(ExportAttribute).Assembly.Location);
            parameters.ReferencedAssemblies.Add(typeof(IPythonInterpreterFactoryProvider).Assembly.Location);
            var result = provider.CompileAssemblyFromSource(parameters, csharpCode);
            if (result.Errors.HasErrors) {
                foreach (var err in result.Errors) {
                    Console.WriteLine(err);
                }
            }

            if (!File.Exists(outFile)) {
                Assert.Fail("Failed to compile {0}", outFile);
            }
            _tempFiles.Add(outFile);
            return outFile;
        }

        private static string FactoryProviderSuccessPath {
            get {
                var path = Path.ChangeExtension(Path.GetTempFileName(), "dll");

                if (File.Exists(path)) {
                    return path;
                }

                return CompileString(@"
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.PythonTools.Interpreter;

namespace FactoryProviderSuccess {
    [Export(typeof(IPythonInterpreterFactoryProvider))]
    public class FactoryProviderSuccess : IPythonInterpreterFactoryProvider {
        public IEnumerable<IPythonInterpreterFactory> GetInterpreterFactories() { yield break; }
        public event EventHandler InterpreterFactoriesChanged { add { } remove { } }
    }
}", path);
            }
        }

        [TestMethod, Priority(0)]
        public void ProviderLoadLog_Success() {
            var sp = new MockServiceProvider();
            var log = new MockActivityLog();
            sp.Services[typeof(SVsActivityLog).Name] = log;
            var sm = new MockSettingsManager();
            sp.Services[typeof(SVsSettingsManager).Name] = sm;

            var path = FactoryProviderSuccessPath;

            sm.ReadOnlyStore.AddSetting(@"PythonTools\InterpreterFactories\Test", "CodeBase", path);

            var service = new InterpreterOptionsService(sp);

            foreach (var msg in log.AllItems) {
                Console.WriteLine(msg);
            }

            AssertUtil.AreEqual(
                new Regex(@"Information//Python Tools//Loading interpreter provider assembly.*//" + Regex.Escape(path)),
                log.AllItems.Single()
            );

            Assert.AreEqual(1, service.KnownProviders.Count());
        }

        [TestMethod, Priority(0)]
        public void ProviderLoadLog_FileNotFound() {
            var sp = new MockServiceProvider();
            var log = new MockActivityLog();
            sp.Services[typeof(SVsActivityLog).Name] = log;
            var sm = new MockSettingsManager();
            sp.Services[typeof(SVsSettingsManager).Name] = sm;

            var path = Path.ChangeExtension(Path.GetTempFileName(), "dll");
            File.Delete(path);
            Assert.IsFalse(File.Exists(path));

            sm.ReadOnlyStore.AddSetting(@"PythonTools\InterpreterFactories\Test", "CodeBase", path);

            var service = new InterpreterOptionsService(sp);

            foreach (var msg in log.AllItems) {
                Console.WriteLine(msg);
            }

            AssertUtil.AreEqual(
                new Regex(@"Error//Python Tools//Failed to load interpreter provider assembly.+System\.IO\.FileNotFoundException.+//" + Regex.Escape(path)),
                log.ErrorsAndWarnings.Single()
            );
        }

        private static string FactoryProviderCorruptPath {
            get {
                var path = Path.ChangeExtension(Path.GetTempFileName(), "dll");

                if (File.Exists(path)) {
                    return path;
                }

                using (var src = new FileStream(FactoryProviderSuccessPath, FileMode.Open, FileAccess.Read))
                using (var dest = new FileStream(path, FileMode.Create, FileAccess.Write)) {
                    var rnd = new Random();
                    var buffer = new byte[4096];
                    int read = src.Read(buffer, 0, buffer.Length);
                    while (read > 0) {
                        for (int count = 64; count > 0; --count) {
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

        [TestMethod, Priority(0)]
        public void ProviderLoadLog_CorruptImage() {
            var sp = new MockServiceProvider();
            var log = new MockActivityLog();
            sp.Services[typeof(SVsActivityLog).Name] = log;
            var sm = new MockSettingsManager();
            sp.Services[typeof(SVsSettingsManager).Name] = sm;

            var path = FactoryProviderCorruptPath;

            sm.ReadOnlyStore.AddSetting(@"PythonTools\InterpreterFactories\Test", "CodeBase", path);

            var service = new InterpreterOptionsService(sp);

            foreach (var msg in log.AllItems) {
                Console.WriteLine(msg);
            }

            AssertUtil.AreEqual(
                new Regex(@"Error//Python Tools//Failed to load interpreter provider assembly.+System\.BadImageFormatException.+//" + Regex.Escape(path)),
                log.ErrorsAndWarnings.Single()
            );
        }


        private static string FactoryProviderTypeLoadErrorPath {
            get {
                var path = Path.ChangeExtension(Path.GetTempFileName(), "dll");

                if (File.Exists(path)) {
                    return path;
                }

                return CompileString(@"
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.PythonTools.Interpreter;

namespace FactoryProviderTypeLoadException {
    [Export(typeof(IPythonInterpreterFactoryProvider))]
    public class FactoryProviderTypeLoadException : IPythonInterpreterFactoryProvider {
        static FactoryProviderTypeLoadException() {
            throw new Exception();
        }
        public IEnumerable<IPythonInterpreterFactory> GetInterpreterFactories() { yield break; }
        public event EventHandler InterpreterFactoriesChanged { add { } remove { } }
    }
}", path);
            }
        }

        [TestMethod, Priority(0)]
        public void ProviderLoadLog_TypeLoadException() {
            var sp = new MockServiceProvider();
            var log = new MockActivityLog();
            sp.Services[typeof(SVsActivityLog).Name] = log;
            var sm = new MockSettingsManager();
            sp.Services[typeof(SVsSettingsManager).Name] = sm;

            var path = FactoryProviderTypeLoadErrorPath;

            sm.ReadOnlyStore.AddSetting(@"PythonTools\InterpreterFactories\Test", "CodeBase", path);

            var service = new InterpreterOptionsService(sp);

            foreach (var msg in log.AllItems) {
                Console.WriteLine(msg);
            }

            AssertUtil.AreEqual(
                new Regex(@"Error//Python Tools//Failed to import factory providers.*System\.ComponentModel\.Composition\.CompositionException"),
                log.ErrorsAndWarnings.Single()
            );

            Assert.AreEqual(0, service.KnownProviders.Count());
        }

        [TestMethod, Priority(0)]
        public void ProviderLoadLog_SuccessAndFailure() {
            var sp = new MockServiceProvider();
            var log = new MockActivityLog();
            sp.Services[typeof(SVsActivityLog).Name] = log;
            var sm = new MockSettingsManager();
            sp.Services[typeof(SVsSettingsManager).Name] = sm;

            var path1 = FactoryProviderTypeLoadErrorPath;
            var path2 = FactoryProviderSuccessPath;

            sm.ReadOnlyStore.AddSetting(@"PythonTools\InterpreterFactories\Test1", "CodeBase", path1);
            sm.ReadOnlyStore.AddSetting(@"PythonTools\InterpreterFactories\Test2", "CodeBase", path2);

            var service = new InterpreterOptionsService(sp);

            foreach (var msg in log.AllItems) {
                Console.WriteLine(msg);
            }

            AssertUtil.AreEqual(
                new Regex(@"Error//Python Tools//Failed to import factory providers.*System\.ComponentModel\.Composition\.CompositionException"),
                log.ErrorsAndWarnings.Single()
            );

            Assert.AreEqual(1, service.KnownProviders.Count());
        }
    }
}
