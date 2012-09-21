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
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;         // Ambiguous with EnvDTE.Thread.
using Microsoft.PythonTools.Django;
using Microsoft.PythonTools.Django.TemplateParsing;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using TestUtilities;
using TestUtilities.Mocks;

namespace DjangoTests {
    [TestClass]
    public class DjangoAttributeTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            TestData.Deploy();
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        public void ProvideDebugLanguageTests() {
            var attr = new ProvideDebugLanguageAttribute("Django Templates",
                DjangoPackage.DjangoTemplateLanguageId,
                "{" + DjangoPackage.DjangoExpressionEvaluatorGuid + "}",
                "{EC1375B7-E2CE-43E8-BF75-DC638DE1F1F9}");


            var mockCtx = new MockRegistrationContext();
            attr.Register(mockCtx);
            attr.Unregister(mockCtx);
            Assert.AreEqual(@"CreatedKey: Languages\Language Services\Django Templates\Debugger Languages\{918E5764-7026-4D57-918D-19D86AD73AC4}
SetValue: Languages\Language Services\Django Templates\Debugger Languages\{918E5764-7026-4D57-918D-19D86AD73AC4}, Django Templates
CreatedKey: AD7Metrics\ExpressionEvaluator\{918E5764-7026-4D57-918D-19D86AD73AC4}\{994B45C4-E6E9-11D2-903F-00C04FA302A1}
SetValue: AD7Metrics\ExpressionEvaluator\{918E5764-7026-4D57-918D-19D86AD73AC4}\{994B45C4-E6E9-11D2-903F-00C04FA302A1}, LanguageDjango Templates
SetValue: AD7Metrics\ExpressionEvaluator\{918E5764-7026-4D57-918D-19D86AD73AC4}\{994B45C4-E6E9-11D2-903F-00C04FA302A1}, NameDjango Templates
SetValue: AD7Metrics\ExpressionEvaluator\{918E5764-7026-4D57-918D-19D86AD73AC4}\{994B45C4-E6E9-11D2-903F-00C04FA302A1}, CLSID{64F20547-C246-487F-83A6-587BC54BAB2F}
Created SubKey: Engine
SetValue: Engine, 0{EC1375B7-E2CE-43E8-BF75-DC638DE1F1F9}
", mockCtx._result.ToString());
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        public void ProvideLanguageTemplatesAttributeTests() {
            var attr = new ProvideLanguageTemplatesAttribute(
                "{349C5851-65DF-11DA-9384-00065B846F21}", 
                "Python", 
                GuidList.guidDjangoPkgString, 
                "Web", 
                "Python Application Project Templates", 
                "{888888a0-9f3d-457c-b088-3a5042f75d52}", 
                ".py", 
                "Python", 
                "{9AF89C0F-85F6-4A20-9023-5D15D912F3B1}");

            var mockCtx = new MockRegistrationContext();
            attr.Register(mockCtx);
            attr.Unregister(mockCtx);
            Assert.AreEqual(@"CreatedKey: Projects\{349C5851-65DF-11DA-9384-00065B846F21}\LanguageTemplates
SetValue: Projects\{349C5851-65DF-11DA-9384-00065B846F21}\LanguageTemplates, {888888a0-9f3d-457c-b088-3a5042f75d52}{9AF89C0F-85F6-4A20-9023-5D15D912F3B1}
Closed Key: Projects\{349C5851-65DF-11DA-9384-00065B846F21}\LanguageTemplates
CreatedKey: Projects\{9AF89C0F-85F6-4A20-9023-5D15D912F3B1}
SetValue: Projects\{9AF89C0F-85F6-4A20-9023-5D15D912F3B1}, Python Application Project Templates
SetValue: Projects\{9AF89C0F-85F6-4A20-9023-5D15D912F3B1}, a8637c34-aa55-46e2-973c-9c3e09afc17b{888888a0-9f3d-457c-b088-3a5042f75d52}
SetValue: Projects\{9AF89C0F-85F6-4A20-9023-5D15D912F3B1}, Language(VsTemplate)Python
SetValue: Projects\{9AF89C0F-85F6-4A20-9023-5D15D912F3B1}, ShowOnlySpecifiedTemplates(VsTemplate)0
SetValue: Projects\{9AF89C0F-85F6-4A20-9023-5D15D912F3B1}, TemplateGroupIDs(VsTemplate)Web
Created SubKey: WebApplicationProperties
SetValue: WebApplicationProperties, CodeFileExtension.py
SetValue: WebApplicationProperties, TemplateFolderWeb
Closed Key: WebApplicationProperties
Closed Key: Projects\{9AF89C0F-85F6-4A20-9023-5D15D912F3B1}
", mockCtx._result.ToString());
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        public void ProvideEditorExtension2AttributeTests() {
            var attr = new ProvideEditorExtension2Attribute(
                    typeof(DjangoEditorFactory),
                    ".djt",
                    50,
                    "*:1");

            attr.ProjectGuid = "{A2FE74E1-B743-11d0-AE1A-00A0C90FFFC3}";
            attr.NameResourceID = 102;
            attr.DefaultName = "webpage";

            var mockCtx = new MockRegistrationContext();
            attr.Register(mockCtx);
            attr.Unregister(mockCtx);
            Assert.AreEqual(@"CreatedKey: Editors\{e1b7abde-cdde-4874-a8a6-5b5c7597a848}
SetValue: Editors\{e1b7abde-cdde-4874-a8a6-5b5c7597a848}, webpage
SetValue: Editors\{e1b7abde-cdde-4874-a8a6-5b5c7597a848}, DisplayName#102
SetValue: Editors\{e1b7abde-cdde-4874-a8a6-5b5c7597a848}, Package{a8637c34-aa55-46e2-973c-9c3e09afc17b}
Closed Key: Editors\{e1b7abde-cdde-4874-a8a6-5b5c7597a848}
CreatedKey: Editors\{e1b7abde-cdde-4874-a8a6-5b5c7597a848}\Extensions
SetValue: Editors\{e1b7abde-cdde-4874-a8a6-5b5c7597a848}\Extensions, djt50
SetValue: Editors\{e1b7abde-cdde-4874-a8a6-5b5c7597a848}\Extensions, *1
Closed Key: Editors\{e1b7abde-cdde-4874-a8a6-5b5c7597a848}\Extensions
CreatedKey: Projects\{a2fe74e1-b743-11d0-ae1a-00a0c90fffc3}\AddItemTemplates\TemplateDirs\{a8637c34-aa55-46e2-973c-9c3e09afc17b}\/1
SetValue: Projects\{a2fe74e1-b743-11d0-ae1a-00a0c90fffc3}\AddItemTemplates\TemplateDirs\{a8637c34-aa55-46e2-973c-9c3e09afc17b}\/1, #102
SetValue: Projects\{a2fe74e1-b743-11d0-ae1a-00a0c90fffc3}\AddItemTemplates\TemplateDirs\{a8637c34-aa55-46e2-973c-9c3e09afc17b}\/1, SortPriority50
Closed Key: Projects\{a2fe74e1-b743-11d0-ae1a-00a0c90fffc3}\AddItemTemplates\TemplateDirs\{a8637c34-aa55-46e2-973c-9c3e09afc17b}\/1
RemovedKey: Editors\{e1b7abde-cdde-4874-a8a6-5b5c7597a848}
RemovedKey: Projects\{a2fe74e1-b743-11d0-ae1a-00a0c90fffc3}\AddItemTemplates\TemplateDirs\{a8637c34-aa55-46e2-973c-9c3e09afc17b}
", mockCtx._result.ToString());
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        public void SnapshotSpanSourceCodeReaderTests() {
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

    class MockRegistrationContext : RegistrationAttribute.RegistrationContext {
        internal readonly StringBuilder _result = new StringBuilder();

        public override string CodeBase {
            get { return "CodeBase"; }
        }

        public override string ComponentPath {
            get { return "ComponentPath"; }
        }

        public override Type ComponentType {
            get { return typeof(DjangoPackage);  }
        }

        public override RegistrationAttribute.Key CreateKey(string name) {
            _result.Append("CreatedKey: ");
            _result.Append(name);
            _result.Append(Environment.NewLine);
            return new MockKey(name, _result);
        }

        public override string EscapePath(string str) {
            return str;
        }

        public override string InprocServerPath {
            get { return "InProcServerPath"; }
        }

        public override System.IO.TextWriter Log {
            get {
                return new StringWriter(_result);
            }
        }

        public override RegistrationMethod RegistrationMethod {
            get { return RegistrationMethod.Default; }
        }

        public override void RemoveKey(string name) {
            _result.Append("RemovedKey: ");
            _result.Append(name);
            _result.Append(Environment.NewLine);
        }

        public override void RemoveKeyIfEmpty(string name) {
            _result.Append("RemovedKeyIfEmpty: ");
            _result.Append(name);
            _result.Append(Environment.NewLine);
        }

        public override void RemoveValue(string keyname, string valuename) {
            _result.Append("RemovedValue: ");
            _result.Append(keyname);
            _result.Append(", ");
            _result.Append(valuename);
            _result.Append(Environment.NewLine);
        }

        public override string RootFolder {
            get { return "."; }
        }

        public class MockKey : RegistrationAttribute.Key {
            private readonly string _name;
            private readonly StringBuilder _result;

            public MockKey(string name, StringBuilder result) {
                _name = name;
                _result = result;
            }

            public override void Close() {
                _result.Append("Closed Key: ");
                _result.Append(_name);
                _result.Append(Environment.NewLine);
            }

            public override RegistrationAttribute.Key CreateSubkey(string name) {
                _result.Append("Created SubKey: ");
                _result.AppendLine(name);
                return new MockKey(name, _result);
            }

            public override void SetValue(string valueName, object value) {
                _result.Append("SetValue: ");
                _result.Append(_name);
                _result.Append(", ");
                _result.Append(valueName);
                _result.AppendLine(value.ToString());
            }
        }
    }
}
