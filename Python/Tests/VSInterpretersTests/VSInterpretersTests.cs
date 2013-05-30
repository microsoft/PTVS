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
using System.ComponentModel.Composition.Hosting;
using System.Reflection;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InterpretersTests {
    [TestClass]
    public class InterpretersTests {
        [TestMethod]
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
                "Microsoft.PythonTools.Interpreters",
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
    }
}
