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

#if !DEV12_OR_LATER

using Microsoft.PythonTools.Django.TemplateParsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using TestUtilities;
using TestUtilities.Mocks;
using TestUtilities.Python;

namespace DjangoTests {
    [TestClass]
    public class SnapshotSpanSourceCodeReaderTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        public void SnapshotSpanSourceCodeReaderTest() {
            var text = "hello world\r\nHello again!";
            var buffer = new MockTextBuffer(text);
            var snapshot = new MockTextSnapshot(buffer, text);

            var reader = new SnapshotSpanSourceCodeReader(new SnapshotSpan(snapshot, new Span(0, text.Length)));

            Assert.AreEqual(reader.Snapshot, snapshot);
            Assert.AreEqual(reader.Position, 0);
            var line = reader.ReadLine();
            Assert.AreEqual(line, "hello world");
            line = reader.ReadLine();
            Assert.AreEqual(line, "Hello again!");

            reader = new SnapshotSpanSourceCodeReader(new SnapshotSpan(snapshot, new Span(0, text.Length)));
            int ch = reader.Peek();
            Assert.AreEqual(ch, (int)'h');

            char[] readBuf = new char[text.Length];
            reader.Read(readBuf, 0, readBuf.Length);

            Assert.AreEqual(new string(readBuf), text);

            reader = new SnapshotSpanSourceCodeReader(new SnapshotSpan(snapshot, new Span(0, text.Length)));
            Assert.AreEqual(reader.ReadToEnd(), text);

            reader.Reset();

            Assert.AreEqual(reader.ReadToEnd(), text);

            reader.Close();
            Assert.AreEqual(reader.Snapshot, null);
        }
    }
}

#endif