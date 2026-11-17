using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.PythonTools.LanguageServerClient;
using Microsoft.PythonTools.LanguageServerClient.FileWatcher;
using Microsoft.PythonTools.LanguageServerClient.StreamHacking;
using Microsoft.PythonTools.LanguageServerClient.WorkspaceConfiguration;
using Microsoft.PythonTools.LanguageServerClient.WorkspaceFolderChanged;
using Microsoft.PythonTools.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;
using TestUtilities.Mocks;
using LSP = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace PythonToolsTests {
    [TestClass]
    public class ProtocolJsonSerializationTests {
        [TestMethod, Priority(0)]
        public void WorkspaceContractsKeepWireNamesAndUris() {
            var uri = new Uri(@"C:\folder with spaces\file.py");
            var folders = new DidChangeWorkspaceFoldersParams {
                changeEvent = new WorkspaceFoldersChangeEvent {
                    added = [new WorkspaceFolder { uri = uri, name = "folder" }],
                    removed = []
                }
            };
            using var json = JsonSerializer.SerializeToDocument(folders);
            Assert.AreEqual(uri.AbsoluteUri, json.RootElement.GetProperty("event").GetProperty("added")[0].GetProperty("uri").GetString());
            Assert.IsFalse(json.RootElement.TryGetProperty("changeEvent", out _));
            Assert.AreEqual(0, json.RootElement.GetProperty("event").GetProperty("removed").GetArrayLength());

            var configuration = JsonSerializer.Deserialize<ConfigurationParams>(
                """{"items":[{"scopeUri":"file:///C:/folder%20with%20spaces/file.py","section":"python","unknown":true},{"scopeUri":null,"section":"python.analysis"}]}""",
                ProtocolJsonSerialization.Options);
            Assert.AreEqual(uri, configuration.items[0].scopeUri);
            Assert.AreEqual("python.analysis", configuration.items[1].section);
            Assert.IsNull(configuration.items[1].scopeUri);

            using var fileEvent = JsonSerializer.SerializeToDocument(
                new LSP.FileEvent { Uri = uri, Type = LSP.FileChangeType.Changed });
            Assert.AreEqual(2, fileEvent.RootElement.GetProperty("type").GetInt32());
            Assert.IsFalse(fileEvent.RootElement.TryGetProperty("fileChangeType", out _));
        }

        [TestMethod, Priority(0)]
        public void CustomMessagesDeserializeRegistrationConfigurationAndTelemetry() {
            using var services = new ServiceContainer();
            var logger = new RecordingLogger();
            services.AddService(typeof(IPythonToolsLogger), logger);
            using var context = new JoinableTaskContext();
            var target = new PythonLanguageClientCustomTarget(services, context);
            DidChangeWatchedFilesRegistrationOptions registration = null;
            var unregistered = new List<string>();
            var analysisComplete = false;
            target.WatchedFilesRegistered += (_, options) => registration = options;
            target.WatchedFilesUnregistered += (_, args) => unregistered.Add(args.RegistrationId);
            target.AnalysisComplete += (_, _) => analysisComplete = true;
            using var json = JsonDocument.Parse("""
                {"registrations":[{"id":"watch","method":"workspace/didChangeWatchedFiles","registerOptions":{"watchers":[
                    {"globPattern":"**/*.py"},
                    {"globPattern":{"baseUri":"file:///C:/root/","pattern":"**/*.pyi"},"kind":2},
                    {"globPattern":{"baseUri":{"uri":"file:///C:/root/","name":"root"},"pattern":"**/*.py"}}
                ],"unknown":true}}]}
                """);
            target.OnRegisterCapability(json.RootElement);
            Assert.AreEqual("watch", registration?.RegistrationId);
            Assert.AreEqual(WatchKind.Create | WatchKind.Change | WatchKind.Delete, registration.Watchers[0].Kind);
            Assert.AreEqual("**/*.py", registration.Watchers[0].GlobPattern.Pattern);
            Assert.AreEqual(WatchKind.Change, registration.Watchers[1].Kind);
            Assert.AreEqual(new Uri("file:///C:/root/"), registration.Watchers[1].GlobPattern.BaseUri);
            Assert.AreEqual(registration.Watchers[1].GlobPattern.BaseUri, registration.Watchers[2].GlobPattern.BaseUri);

            foreach (var spelling in new[] { "unregisterations", "unregistrations" }) {
                using var unregistration = JsonDocument.Parse(
                    $$"""{"{{spelling}}":[{"id":"watch","method":"workspace/didChangeWatchedFiles"}]}""");
                target.OnUnregisterCapability(unregistration.RootElement);
            }
            CollectionAssert.AreEqual(new[] { "watch", "watch" }, unregistered);

            target.WorkspaceConfiguration += (_, args) => {
                Assert.AreEqual("python", args.requestParams.items[0].section);
                args.requestResult = new object[] { null };
                return Task.CompletedTask;
            };
            using var configuration = JsonDocument.Parse("""{"items":[{"section":"python"}]}""");
            var result = (object[])target.OnWorkspaceConfiguration(configuration.RootElement).GetAwaiter().GetResult();
            Assert.AreEqual(1, result.Length);
            Assert.IsNull(result[0]);

            using var telemetry = JsonDocument.Parse("""
                {"eventName":"language_server/analysis_complete","properties":{"text":"value","count":2,"flag":true,"empty":null},"measurements":{"duration":1.5},"exception":null,"unknown":true}
                """);
            target.OnTelemetryEvent(telemetry.RootElement);
            Assert.IsTrue(analysisComplete);
            Assert.AreEqual("value", logger.Properties["text"]);
            Assert.AreEqual(2L, logger.Properties["count"]);
            Assert.AreEqual(true, logger.Properties["flag"]);
            Assert.IsNull(logger.Properties["empty"]);
        }

        [TestMethod, Priority(0)]
        public void CompletionUnionsAndNavigationResultsKeepEditorTypes() {
            using var json = JsonDocument.Parse("""
                {"isIncomplete":true,"items":[
                    {"label":"first","documentation":"plain","textEdit":{"range":{"start":{"line":0,"character":0},"end":{"line":0,"character":1}},"newText":"first"},"data":{"id":42}},
                    {"label":"second","documentation":{"kind":"markdown","value":"**second**"},"textEdit":{"insert":{"start":{"line":0,"character":0},"end":{"line":0,"character":0}},"replace":{"start":{"line":0,"character":0},"end":{"line":0,"character":1}},"newText":"second"}}
                ],"unknown":true}
                """);
            var list = ((LSP.CompletionList[])ResultConverter.ConvertResult(json.RootElement))[0];
            Assert.IsTrue(list.IsIncomplete);
            Assert.IsInstanceOfType(list.Items[0].TextEdit.Value, typeof(LSP.TextEdit));
            Assert.AreEqual("plain", list.Items[0].Documentation.Value);
            Assert.IsInstanceOfType(list.Items[1].TextEdit.Value, typeof(LSP.InsertReplaceEdit));
            Assert.AreEqual(LSP.MarkupKind.Markdown, ((LSP.MarkupContent)list.Items[1].Documentation.Value).Kind);

            using var resolved = JsonSerializer.SerializeToDocument(list.Items[1]);
            Assert.AreEqual("markdown", resolved.RootElement.GetProperty("documentation").GetProperty("kind").GetString());
            Assert.IsTrue(resolved.RootElement.GetProperty("textEdit").TryGetProperty("replace", out _));
            using var first = JsonSerializer.SerializeToDocument(list.Items[0]);
            Assert.AreEqual(42, first.RootElement.GetProperty("data").GetProperty("id").GetInt32());
            var bareItems = (LSP.CompletionList[])ResultConverter.ConvertResult(json.RootElement.GetProperty("items"));
            Assert.AreEqual(2, bareItems[0].Items.Length);
            Assert.AreEqual(0, ResultConverter.ConvertResult(null).Length);

            using var location = JsonDocument.Parse("""[{"uri":"file:///C:/a%20b.py","range":{"start":{"line":1,"character":0},"end":{"line":1,"character":2}}}]""");
            var locations = (LSP.Location[])ResultConverter.ConvertResult(location.RootElement);
            Assert.AreEqual(new Uri("file:///C:/a%20b.py"), locations[0].Uri);
            Assert.AreEqual(1, locations[0].Range.Start.Line);
        }

        [TestMethod, Priority(0)]
        public void InitializeStreamKeepsUtf8FramingAndFollowingMessages() {
            var message = """{"jsonrpc":"2.0","id":1,"result":{"capabilities":{"textDocumentSync":2,"unknown":"\u03bb"}}}""".Replace("\\u03bb", "\u03bb");
            var frame = Encoding.UTF8.GetBytes($"Content-Length: {Encoding.UTF8.GetByteCount(message)}\r\n\r\n{message}");
            var following = MessageParser.Serialize(JsonNode.Parse("""{"method":"test","params":{"text":"\u03bb"}}""").AsObject()).bytes;
            using var input = new MemoryStream(frame.Concat(following).ToArray());
            using var stream = new PylanceInitializeResponseStream(input);
            using var output = new MemoryStream();
            stream.CopyTo(output, 7);
            var bytes = output.ToArray();
            var normalized = MessageParser.Deserialize(new StreamData { bytes = bytes, count = bytes.Length });
            Assert.AreEqual(2, normalized["result"]["capabilities"]["textDocumentSync"]["change"].GetValue<int>());
            Assert.AreEqual("\u03bb", normalized["result"]["capabilities"]["unknown"].GetValue<string>());
            CollectionAssert.AreEqual(following, bytes.Skip(bytes.Length - following.Length).ToArray());
            var headerEnd = Encoding.UTF8.GetString(bytes).IndexOf("\r\n\r\n", StringComparison.Ordinal);
            var contentLength = int.Parse(Encoding.ASCII.GetString(bytes, 16, headerEnd - 16));
            Assert.AreEqual(bytes.Length - following.Length - headerEnd - 4, contentLength);
        }

        [TestMethod, Priority(0)]
        public void MiddleLayerNormalizesStjEditsAndTracksStreamVersions() {
            var layer = new PythonLanguageClientMiddleLayer();
            var buffer = new MockTextBuffer("abc\r\n");
            layer.Properties[typeof(ITextSnapshot)] = buffer.CurrentSnapshot;
            using var opened = JsonDocument.Parse("""{"textDocument":{"uri":"file:///C:/test.py","version":1}}""");
            layer.HandleNotificationAsync("textDocument/didOpen", opened, _ => Task.CompletedTask).GetAwaiter().GetResult();
            layer.TrackDocumentMessage(JsonNode.Parse("""{"method":"textDocument/didChange","params":{"textDocument":{"uri":"file:///C:/test.py","version":3}}}""").AsObject());
            using var edit = JsonDocument.Parse("""
                {"label":"edit","edit":{"changes":{"file:///C:/test.py":[{"range":{"start":{"line":0,"character":0},"end":{"line":0,"character":4}},"newText":"x","unknown":true}]}}}
                """);
            layer.HandleNotificationAsync("workspace/applyEdit", edit, async normalized => {
                await Task.Yield();
                var textEdit = normalized.RootElement.GetProperty("edit").GetProperty("changes").GetProperty("file:///C:/test.py")[0];
                Assert.AreEqual(3, textEdit.GetProperty("range").GetProperty("end").GetProperty("character").GetInt32());
                Assert.IsTrue(textEdit.GetProperty("unknown").GetBoolean());
            }).GetAwaiter().GetResult();
            Assert.AreEqual(4, edit.RootElement.GetProperty("edit").GetProperty("changes").GetProperty("file:///C:/test.py")[0].GetProperty("range").GetProperty("end").GetProperty("character").GetInt32());
            using var diagnostics = JsonDocument.Parse("""{"uri":"file:///C:/test.py","version":2}""");
            var sent = false;
            layer.HandleNotificationAsync("textDocument/publishDiagnostics", diagnostics, _ => {
                sent = true;
                return Task.CompletedTask;
            }).GetAwaiter().GetResult();
            Assert.IsFalse(sent);
        }

        private sealed class RecordingLogger : IPythonToolsLogger {
            public IReadOnlyDictionary<string, object> Properties { get; private set; }
            public void LogEvent(PythonLogEvent logEvent, object argument) { }
            public void LogEvent(string eventName, IReadOnlyDictionary<string, object> properties, IReadOnlyDictionary<string, double> measurements) =>
                Properties = properties;
            public void LogFault(Exception ex, string description, bool dumpProcess) => Assert.Fail(description);
        }
    }
}
