using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.UI;
using TestUtilities.UI.Python;

namespace PythonToolsUITests {
    public class IntellisenseTests {
        private PythonVersion Version => PythonPaths.Python37_x64 ?? PythonPaths.Python38_x64 ?? PythonPaths.Python39_x64;


        public void AutoComplete(PythonVisualStudioApp app) {
            EventHandler<CompletionTriggeredEventArgs> handler = null;
            var languageName = PythonVisualStudioApp.TemplateLanguageName;
            var slnPath = PrepareProject(app);

            var project = app.OpenProject(slnPath);
            var item = project.ProjectItems.Item("document.py");
            var window = item.Open();
            window.Activate();
            var doc = app.GetDocument(item.Document.FullName);
            try { 
                doc.Invoke(() => {
                    using (var e = doc.TextView.TextBuffer.CreateEdit()) {
                        e.Insert(e.Snapshot.Length, "import sys\r\nsys.");
                        e.Apply();
                    }

                    // Move cursor
                    doc.TextView.Caret.MoveTo(new Microsoft.VisualStudio.Text.SnapshotPoint(doc.TextView.TextSnapshot, doc.TextView.TextSnapshot.Length));
                });

                // Wait until pylance is ready
                Assert.IsTrue(Microsoft.PythonTools.LanguageServerClient.PythonLanguageClient.ReadyTask.Wait(60000), "Pylance did not start");

                // Bring up auto complete
                Assert.IsTrue(doc.AsyncCompletionBroker.IsCompletionSupported(doc.TextView.TextBuffer.ContentType));
                var completionTask = new TaskCompletionSource<IAsyncCompletionSession>();
                handler = (s, e) => {
                    completionTask.TrySetResult(e.CompletionSession);
                };
                doc.AsyncCompletionBroker.CompletionTriggered += handler;
                doc.Invoke(() => {
                    return doc.AsyncCompletionBroker.TriggerCompletion(
                        doc.TextView,
                        new CompletionTrigger(CompletionTriggerReason.Invoke, doc.TextView.TextSnapshot),
                        new Microsoft.VisualStudio.Text.SnapshotPoint(doc.TextView.TextSnapshot, doc.TextView.TextSnapshot.Length),
                        CancellationToken.None);
                });

                // Wait for it to show
                Assert.IsTrue(completionTask.Task.Wait(60000), "Completion session did not show");
                Assert.IsNotNull(completionTask.Task.Result, "Completion not triggerable");
                var items = completionTask.Task.Result.GetComputedItems(CancellationToken.None);
                Assert.IsTrue(items.Items.Any(i => i.InsertText == "executable"), "Executable member of sys not found");

            } finally {
                app.Dte.Solution.Close(false);
                if (handler != null && doc != null && doc.AsyncCompletionBroker != null) {
                    doc.AsyncCompletionBroker.CompletionTriggered -= handler;
                }
            }
        }

        private static string PrepareProject(PythonVisualStudioApp app) {
            // Use the formatting tests project.
            var slnPath = app.CopyProjectForTest(@"TestData\FormattingTests\FormattingTests.sln");
            var projFolder = Path.GetDirectoryName(slnPath);
            var projPath = Path.Combine(projFolder, "FormattingTests.pyproj");

            // The project file has a placeholder for the formatter which we must replace
            var projContents = File.ReadAllText(projPath);
            projContents = projContents.Replace("$$FORMATTER$$", "black");
            File.WriteAllText(projPath, projContents);

            return slnPath;
        }
    }
}
