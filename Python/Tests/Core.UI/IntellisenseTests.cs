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
            EventHandler<ComputedCompletionItemsEventArgs> itemsUpdatedHandler = null;
            IAsyncCompletionSession session = null;
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
                var executableTask = new TaskCompletionSource<IEnumerable<CompletionItem>>();
                IEnumerable<CompletionItem> items = Array.Empty<CompletionItem>();
                Action<IEnumerable<CompletionItem>> updateItems = newItems => {
                    items = newItems.ToArray();
                    if (items.Any(i => i.InsertText == "executable")) {
                        executableTask.TrySetResult(items);
                    }
                };
                handler = (s, e) => {
                    session = e.CompletionSession;
                    itemsUpdatedHandler = (sender, args) => {
                        updateItems(args.Items.Items);
                    };
                    session.ItemsUpdated += itemsUpdatedHandler;
                    updateItems(session.GetComputedItems(CancellationToken.None).Items);
                    completionTask.TrySetResult(session);
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
                Assert.IsTrue(
                    executableTask.Task.Wait(60000),
                    $"Executable member of sys not found. Items: {string.Join(", ", items.Take(20).Select(i => i.InsertText))}");

            } finally {
                app.Dte.Solution.Close(false);
                if (itemsUpdatedHandler != null && session != null) {
                    session.ItemsUpdated -= itemsUpdatedHandler;
                }
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
