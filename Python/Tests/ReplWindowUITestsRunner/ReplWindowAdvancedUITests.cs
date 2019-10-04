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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestRunnerInterop;

namespace ReplWindowUITestsRunner {
    [TestClass, Ignore]
    public abstract class ReplWindowAdvancedUITests {
        #region UI test boilerplate
        public VsTestInvoker _vs => new VsTestInvoker(
            VsTestContext.Instance,
            // Remote container (DLL) name
            "Microsoft.PythonTools.Tests.ReplWindowUITests",
            // Remote class name
            $"ReplWindowUITests.{nameof(ReplWindowUITests)}"
        );

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() => VsTestContext.Instance.TestInitialize(TestContext.DeploymentDirectory);
        [TestCleanup]
        public void TestCleanup() => VsTestContext.Instance.TestCleanup();
        [ClassCleanup]
        public static void ClassCleanup() => VsTestContext.Instance.Dispose();
        #endregion

        protected abstract string Interpreter { get; }

        #region Advanced Miscellaneous tests

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void ClearInputHelper() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.ClearInputHelper), Interpreter);
        }

        #endregion

        #region Advanced Signature Help tests

        //[TestMethod, Priority(2)]
        //[TestCategory("Interactive")]
        //[TestCategory("Installed")]
        //public void SimpleSignatureHelp() {
        //    _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.SimpleSignatureHelp), Interpreter);
        //}

        //[Ignore] // https://github.com/Microsoft/PTVS/issues/2689
        //[TestMethod, Priority(2)]
        //[TestCategory("Interactive")]
        //[TestCategory("Installed")]
        //public void SignatureHelpDefaultValue() {
        //    _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.SignatureHelpDefaultValue), Interpreter);
        //}

        #endregion

        #region Advanced Completion tests

        [TestMethod, Priority(2)]
        [TestCategory("Interactive")]
        [TestCategory("Installed")]
        public void SimpleCompletion() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.SimpleCompletion), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Interactive")]
        [TestCategory("Installed")]
        public void SimpleCompletionSpaceNoCompletion() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.SimpleCompletionSpaceNoCompletion), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Interactive")]
        [TestCategory("Installed")]
        public void CompletionWrongText() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.CompletionWrongText), Interpreter);
        }

        //[TestMethod, Priority(2)]
        //[TestCategory("Interactive")]
        //[TestCategory("Installed")]
        //public void CompletionFullTextWithoutNewLine() {
        //    _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.CompletionFullTextWithoutNewLine), Interpreter);
        //}

        //[TestMethod, Priority(2)]
        //[TestCategory("Interactive")]
        //[TestCategory("Installed")]
        //public void CompletionFullTextWithNewLine() {
        //    _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.CompletionFullTextWithNewLine), Interpreter);
        //}

        [TestMethod, Priority(2)]
        [TestCategory("Interactive")]
        [TestCategory("Installed")]
        public void AutoListIdentifierCompletions() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.AutoListIdentifierCompletions), Interpreter);
        }

        #endregion

        #region Advanced Input/Output redirection tests

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void TestStdOutRedirected() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.TestStdOutRedirected), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Interactive")]
        [TestCategory("Installed")]
        public void TestRawInput() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.TestRawInput), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Interactive")]
        [TestCategory("Installed")]
        public void OnlyTypeInRawInput() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.OnlyTypeInRawInput), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Interactive")]
        [TestCategory("Installed")]
        public void DeleteCharactersInRawInput() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.DeleteCharactersInRawInput), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Interactive")]
        [TestCategory("Installed")]
        public void TestIndirectInput() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.TestIndirectInput), Interpreter);
        }

        #endregion

        #region Advanced Keyboard tests

        [TestMethod, Priority(2)]
        [TestCategory("Interactive")]
        [TestCategory("Installed")]
        public void EnterInMiddleOfLine() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.EnterInMiddleOfLine), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Interactive")]
        [TestCategory("Installed")]
        public void LineBreak() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.LineBreak), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Interactive")]
        [TestCategory("Installed")]
        public void LineBreakInMiddleOfLine() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.LineBreakInMiddleOfLine), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Interactive")]
        [TestCategory("Installed")]
        public void CtrlEnterCommits() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.CtrlEnterCommits), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Interactive")]
        [TestCategory("Installed")]
        public void EscapeClearsMultipleLines() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.EscapeClearsMultipleLines), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Interactive")]
        [TestCategory("Installed")]
        public void CtrlEnterOnPreviousInput() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.CtrlEnterOnPreviousInput), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Interactive")]
        [TestCategory("Installed")]
        public void CtrlEnterForceCommit() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.CtrlEnterForceCommit), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Interactive")]
        [TestCategory("Installed")]
        public void CtrlEnterMultiLineForceCommit() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.CtrlEnterMultiLineForceCommit), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Interactive")]
        [TestCategory("Installed")]
        public void BackspacePrompt() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.BackspacePrompt), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Interactive")]
        [TestCategory("Installed")]
        public void BackspaceSmartDedent() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.BackspaceSmartDedent), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Interactive")]
        [TestCategory("Installed")]
        public void BackspaceSecondaryPrompt() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.BackspaceSecondaryPrompt), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Interactive")]
        [TestCategory("Installed")]
        public void BackspaceSecondaryPromptSelected() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.BackspaceSecondaryPromptSelected), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Interactive")]
        [TestCategory("Installed")]
        public void DeleteSecondaryPromptSelected() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.DeleteSecondaryPromptSelected), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Interactive")]
        [TestCategory("Installed")]
        public void EditTypeSecondaryPromptSelected() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.EditTypeSecondaryPromptSelected), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Interactive")]
        [TestCategory("Installed")]
        public void TestDelNoTextSelected() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.TestDelNoTextSelected), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Interactive")]
        [TestCategory("Installed")]
        public void TestDelAtEndOfLine() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.TestDelAtEndOfLine), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Interactive")]
        [TestCategory("Installed")]
        public void TestDelAtEndOfBuffer() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.TestDelAtEndOfBuffer), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Interactive")]
        [TestCategory("Installed")]
        public void TestDelInOutput() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.TestDelInOutput), Interpreter);
        }

        #endregion

        #region Advanced Cancel tests

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void CtrlBreakInterrupts() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.CtrlBreakInterrupts), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void CtrlBreakInterruptsLongRunning() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.CtrlBreakInterruptsLongRunning), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void CtrlBreakNotRunning() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.CtrlBreakNotRunning), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void CursorWhileCodeIsRunning() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.CursorWhileCodeIsRunning), Interpreter);
        }

        #endregion

        #region Advanced History tests

        [TestMethod, Priority(2)]
        [TestCategory("Interactive")]
        [TestCategory("Installed")]
        public void HistoryUpdateDef() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.HistoryUpdateDef), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Interactive")]
        [TestCategory("Installed")]
        public void HistoryAppendDef() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.HistoryAppendDef), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void HistoryBackForward() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.HistoryBackForward), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void HistoryMaximumLength() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.HistoryMaximumLength), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void HistoryUncommittedInput1() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.HistoryUncommittedInput1), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void HistoryUncommittedInput2() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.HistoryUncommittedInput2), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void HistorySearch() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.HistorySearch), Interpreter);
        }

        #endregion

        #region Advanced Clipboard tests

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void CommentPaste() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.CommentPaste), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void CsvPaste() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.CsvPaste), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void EditCutIncludingPrompt() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.EditCutIncludingPrompt), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void EditPasteSecondaryPromptSelected() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.EditPasteSecondaryPromptSelected), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void EditPasteSecondaryPromptSelectedInPromptMargin() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.EditPasteSecondaryPromptSelectedInPromptMargin), Interpreter);
        }

        #endregion

        #region Advanced Command tests

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void ReplCommandUnknown() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.ReplCommandUnknown), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void ReplCommandComment() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.ReplCommandComment), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void ClearScreenCommand() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.ClearScreenCommand), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void ReplCommandHelp() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.ReplCommandHelp), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void CommandsLoadScript() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.CommandsLoadScript), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void CommandsLoadScriptWithQuotes() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.CommandsLoadScriptWithQuotes), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void CommandsLoadScriptWithClass() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.CommandsLoadScriptWithClass), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void CommandsLoadScriptMultipleSubmissions() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.CommandsLoadScriptMultipleSubmissions), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void CommandsLoadScriptMultipleSubmissionsWithClearScreen() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.CommandsLoadScriptMultipleSubmissionsWithClearScreen), Interpreter);
        }

        #endregion

        #region Advanced Insert Code tests

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void InsertCode() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.InsertCode), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void InsertCodeWhileRunning() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.InsertCodeWhileRunning), Interpreter);
        }

        #endregion

        #region Advanced Launch Configuration Tests

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void PythonPathIgnored() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.PythonPathIgnored), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void PythonPathNotIgnored() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.PythonPathNotIgnored), Interpreter);
        }

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void PythonPathNotIgnoredButMissing() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.PythonPathNotIgnoredButMissing), Interpreter);
        }

        #endregion
    }

    [TestClass]
    public class ReplWindowAdvancedUITests27 : ReplWindowAdvancedUITests {
        protected override string Interpreter => "Python27|Python27_x64";
    }
}
