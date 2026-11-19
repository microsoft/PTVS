using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
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
using StreamJsonRpc;
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
        public void InsertReplaceCompletionUsesItsOwnTextAndReplacementRange() {
            var item = JsonSerializer.Deserialize<LSP.CompletionItem>("""
                {"label":"apple","insertText":"ignored","textEdit":{
                    "newText":"apple",
                    "insert":{"start":{"line":0,"character":0},"end":{"line":0,"character":2}},
                    "replace":{"start":{"line":0,"character":0},"end":{"line":0,"character":7}}
                }}
                """, ProtocolJsonSerialization.Options);
            var buffer = new MockTextBuffer("apricot");
            var edit = Microsoft.PythonTools.Repl.Completion.AsyncCompletionSource.GetCompletionTextEdit(item);
            Microsoft.PythonTools.Repl.Completion.Utilities.ApplyTextEdit(edit, buffer.CurrentSnapshot, buffer);
            Assert.AreEqual("apple", buffer.CurrentSnapshot.GetText());

            var insertion = JsonSerializer.Deserialize<LSP.CompletionItem>("""
                {"label":"sqrt","textEdit":{"newText":"sqrt",
                "range":{"start":{"line":0,"character":5},"end":{"line":0,"character":5}}}}
                """, ProtocolJsonSerialization.Options);
            var insertionBuffer = new MockTextBuffer("math.");
            Microsoft.PythonTools.Repl.Completion.Utilities.ApplyTextEdit(
                Microsoft.PythonTools.Repl.Completion.AsyncCompletionSource.GetCompletionTextEdit(insertion),
                insertionBuffer.CurrentSnapshot, insertionBuffer);
            Assert.AreEqual("math.sqrt", insertionBuffer.CurrentSnapshot.GetText());
        }

        [TestMethod, Priority(0)]
        public async Task ClientRequestsRoundTripThroughTheStjHost() {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var pair = await RpcPair.CreateAsync(timeout.Token);
            var server = new ProtocolServer();
            using var services = new ServiceContainer();
            using var context = new JoinableTaskContext();
            var callbacks = new PythonLanguageClientCustomTarget(services, context);
            callbacks.WorkspaceConfiguration += async (_, args) => {
                await Task.Yield();
                Assert.AreEqual("python", args.requestParams.items[0].section);
                args.requestResult = new object[] { new { pythonPath = @"C:\env\python.exe" }, null };
            };
            callbacks.WorkspaceFolders += (_, args) => {
                args.requestResult = new[] { new WorkspaceFolder { uri = new Uri("file:///C:/workspace/"), name = "workspace" } };
                return Task.CompletedTask;
            };
            pair.Host.AddLocalRpcTarget(callbacks);
            pair.Server.AddLocalRpcTarget(server);
            pair.Host.StartListening();
            pair.Server.StartListening();
            using var client = new PythonLanguageClient();
            typeof(PythonLanguageClient).GetField("_rpc", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(client, pair.Host);
            var uri = new Uri("file:///C:/folder%20with%20spaces/%CE%BB.py");
            var request = new LSP.CompletionParams {
                TextDocument = new LSP.TextDocumentIdentifier { Uri = uri },
                Position = new LSP.Position(2, 3),
                Context = new LSP.CompletionContext { TriggerKind = LSP.CompletionTriggerKind.Invoked }
            };
            var result = await client.InvokeTextDocumentCompletionAsync(request, timeout.Token);
            Assert.AreEqual(uri.AbsoluteUri, server.CompletionRequest.GetProperty("textDocument").GetProperty("uri").GetString());
            Assert.AreEqual(2, server.CompletionRequest.GetProperty("position").GetProperty("line").GetInt32());
            var completion = ((LSP.CompletionList[])ResultConverter.ConvertResult(result))[0].Items[0];
            var resolved = await client.InvokeResolveAsync(completion, timeout.Token);
            Assert.AreEqual("example", resolved.Label);
            Assert.AreEqual("plain documentation", resolved.Documentation.Value);
            using var resolvedJson = JsonSerializer.SerializeToDocument(resolved, ProtocolJsonSerialization.Options);
            Assert.AreEqual(42, resolvedJson.RootElement.GetProperty("data").GetProperty("id").GetInt32());
            Assert.IsTrue(resolvedJson.RootElement.GetProperty("textEdit").TryGetProperty("range", out _));
            var references = await client.InvokeReferencesAsync(new LSP.ReferenceParams {
                TextDocument = request.TextDocument,
                Position = request.Position,
                Context = new LSP.ReferenceContext { IncludeDeclaration = true }
            }, timeout.Token);
            Assert.AreEqual(uri, references[0].Uri);
            Assert.AreEqual(2, references[0].Range.Start.Line);
            var command = (JsonElement)await client.InvokeCommandAsync(new LSP.ExecuteCommandParams {
                Command = "test.command",
                Arguments = new object[] { "argument", 42 }
            }, timeout.Token);
            Assert.AreEqual("ok", command[0].GetString());
            var configuration = await pair.Server.InvokeWithParameterObjectAsync<JsonElement>(
                "workspace/configuration", new { items = new[] { new { section = "python" }, new { section = "unknown" } } }, timeout.Token);
            Assert.AreEqual(@"C:\env\python.exe", configuration[0].GetProperty("pythonPath").GetString());
            Assert.AreEqual(JsonValueKind.Null, configuration[1].ValueKind);
            var folders = await pair.Server.InvokeWithParameterObjectAsync<JsonElement>(
                "workspace/workspaceFolders", null, timeout.Token);
            Assert.AreEqual("file:///C:/workspace/", folders[0].GetProperty("uri").GetString());
            Assert.AreEqual("workspace", folders[0].GetProperty("name").GetString());

            var changed = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
            server.Changed = changed;
            using var cancel = timeout.Token.Register(() => changed.TrySetCanceled());
            await client.InvokeTextDocumentDidChangeAsync(new LSP.DidChangeTextDocumentParams {
                TextDocument = new LSP.VersionedTextDocumentIdentifier { Uri = uri, Version = 3 },
                ContentChanges = new[] {
                    new LSP.TextDocumentContentChangeEvent(new LSP.TextDocumentContentChangePartial {
                        Text = "new text", Range = new LSP.Range {
                            Start = new LSP.Position(2, 0), End = new LSP.Position(2, 1)
                        }, RangeLength = 1
                    })
                }
            });
            var change = await changed.Task;
            Assert.AreEqual(3, change.GetProperty("textDocument").GetProperty("version").GetInt32());
            Assert.AreEqual("new text", change.GetProperty("contentChanges")[0].GetProperty("text").GetString());
            Assert.IsTrue(change.GetProperty("contentChanges")[0].TryGetProperty("range", out _));
            server.ReturnNullCompletion = true;
            Assert.IsNull(await client.InvokeTextDocumentCompletionAsync(request, timeout.Token));
            pair.Host.Dispose();
            Assert.IsNull(await client.InvokeTextDocumentCompletionAsync(request, timeout.Token));
        }

        [TestMethod, Priority(0)]
        public void RelativeGlobPatternsRejectMalformedJsonExplicitly() {
            foreach (var json in new[] {
                """{"pattern":"*.py"}""",
                """{"baseUri":42,"pattern":"*.py"}""",
                """{"baseUri":"relative/path","pattern":"*.py"}""",
                """{"baseUri":"file:///C:/root/","pattern":42}"""
            }) {
                TestUtilities.AssertUtil.Throws<JsonException>(() =>
                    JsonSerializer.Deserialize<GlobPattern>(json, ProtocolJsonSerialization.Options));
            }
        }

        [TestMethod, Priority(0)]
        public void EmptyAndNullServerResultsRemainEmpty() {
            foreach (var json in new[] { "null", "[]" }) {
                using var document = JsonDocument.Parse(json);
                Assert.AreEqual(0, ResultConverter.ConvertResult(document.RootElement).Length);
            }
            using var empty = JsonDocument.Parse("""{"isIncomplete":false,"items":[]}""");
            var list = (LSP.CompletionList[])ResultConverter.ConvertResult(empty.RootElement);
            Assert.AreEqual(0, list[0].Items.Length);
        }

        [TestMethod, Priority(0)]
        public async Task StjRpcRegistersAndSendsRealFileChanges() {
            var directory = Path.Combine(Path.GetTempPath(), "PTVS LSP " + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var filePath = Path.Combine(directory, "test file.py");
            try {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                using var pair = await RpcPair.CreateAsync(timeout.Token);
                var hostRpc = pair.Host;
                var serverRpc = pair.Server;
                using var services = new ServiceContainer();
                using var context = new JoinableTaskContext();
                using var listener = new Listener(hostRpc, null, services);
                var customTarget = new PythonLanguageClientCustomTarget(services, context);
                var registered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                customTarget.WatchedFilesRegistered += (_, options) => {
                    listener.RegisterPatterns(options.RegistrationId, options.Watchers);
                    registered.TrySetResult(true);
                };
                customTarget.WatchedFilesUnregistered += (_, args) => listener.UnregisterPatterns(args.RegistrationId);
                var notifications = new FileChangeTarget(new Uri(filePath));
                hostRpc.AddLocalRpcTarget(customTarget);
                serverRpc.AddLocalRpcTarget(notifications);
                hostRpc.StartListening();
                serverRpc.StartListening();
                using var cancelRegistration = timeout.Token.Register(() => registered.TrySetCanceled());
                using var cancelNotification = timeout.Token.Register(() => notifications.Created.TrySetCanceled());

                var registration = JsonSerializer.SerializeToElement(new {
                    registrations = new[] {
                        new {
                            id = "files",
                            method = "workspace/didChangeWatchedFiles",
                            registerOptions = new {
                                watchers = new[] {
                                    new {
                                        globPattern = new {
                                            baseUri = new Uri(directory + Path.DirectorySeparatorChar).AbsoluteUri,
                                            pattern = "**/*.py"
                                        }
                                    }
                                }
                            }
                        }
                    }
                });
                await serverRpc.InvokeWithParameterObjectAsync<object>(
                    "client/registerCapability", registration, timeout.Token);
                await registered.Task;
                File.WriteAllText(filePath, "value = 1\r\n");
                var created = await notifications.Created.Task;
                Assert.AreEqual(new Uri(filePath).AbsoluteUri, created.GetProperty("uri").GetString());
                Assert.AreEqual(1, created.GetProperty("type").GetInt32());
                Assert.IsFalse(created.TryGetProperty("fileChangeType", out _));

                using var unregistration = JsonDocument.Parse(
                    """{"unregisterations":[{"id":"files","method":"workspace/didChangeWatchedFiles"}]}""");
                await serverRpc.InvokeWithParameterObjectAsync<object>(
                    "client/unregisterCapability", unregistration.RootElement, timeout.Token);
            } finally {
                if (File.Exists(filePath)) {
                    File.Delete(filePath);
                }
                Directory.Delete(directory);
            }
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

        private sealed class RpcPair : IDisposable {
            private readonly NamedPipeServerStream _hostPipe;
            private readonly NamedPipeClientStream _serverPipe;

            private RpcPair(NamedPipeServerStream hostPipe, NamedPipeClientStream serverPipe) {
                _hostPipe = hostPipe;
                _serverPipe = serverPipe;
                var hostFormatter = new SystemTextJsonFormatter();
                LSP.ProtocolJsonSerializerOptions.Configure(hostFormatter.JsonSerializerOptions);
                var serverFormatter = new SystemTextJsonFormatter();
                LSP.ProtocolJsonSerializerOptions.Configure(serverFormatter.JsonSerializerOptions);
                Host = new JsonRpc(new HeaderDelimitedMessageHandler(hostPipe, hostPipe, hostFormatter));
                Server = new JsonRpc(new HeaderDelimitedMessageHandler(serverPipe, serverPipe, serverFormatter));
            }

            public JsonRpc Host { get; }
            public JsonRpc Server { get; }

            public static async Task<RpcPair> CreateAsync(CancellationToken cancellationToken) {
                var pipeName = "PTVS-LSP-" + Guid.NewGuid().ToString("N");
                var host = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                var server = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                try {
                    await Task.WhenAll(host.WaitForConnectionAsync(cancellationToken), server.ConnectAsync(10000, cancellationToken));
                    return new RpcPair(host, server);
                } catch {
                    host.Dispose();
                    server.Dispose();
                    throw;
                }
            }

            public void Dispose() {
                Host.Dispose();
                Server.Dispose();
                _hostPipe.Dispose();
                _serverPipe.Dispose();
            }
        }

        private sealed class ProtocolServer {
            public JsonElement CompletionRequest { get; private set; }
            public bool ReturnNullCompletion { get; set; }
            public TaskCompletionSource<JsonElement> Changed { get; set; }

            [JsonRpcMethod("textDocument/completion", UseSingleObjectParameterDeserialization = true)]
            public JsonElement Complete(JsonElement parameters) {
                CompletionRequest = parameters.Clone();
                using var result = JsonDocument.Parse(ReturnNullCompletion ? "null" : """
                    {"isIncomplete":false,"items":[{"label":"example","documentation":"plain documentation",
                    "textEdit":{"range":{"start":{"line":2,"character":0},"end":{"line":2,"character":3}},"newText":"example"},
                    "data":{"id":42}}]}
                    """);
                return result.RootElement.Clone();
            }

            [JsonRpcMethod("completionItem/resolve", UseSingleObjectParameterDeserialization = true)]
            public JsonElement Resolve(JsonElement parameters) => parameters.Clone();

            [JsonRpcMethod("textDocument/references", UseSingleObjectParameterDeserialization = true)]
            public JsonElement References(JsonElement parameters) {
                Assert.IsTrue(parameters.GetProperty("context").GetProperty("includeDeclaration").GetBoolean());
                return JsonSerializer.SerializeToElement(new[] {
                    new {
                        uri = parameters.GetProperty("textDocument").GetProperty("uri").GetString(),
                        range = new { start = new { line = 2, character = 0 }, end = new { line = 2, character = 3 } }
                    }
                });
            }

            [JsonRpcMethod("workspace/executeCommand", UseSingleObjectParameterDeserialization = true)]
            public string[] Execute(JsonElement parameters) {
                Assert.AreEqual("test.command", parameters.GetProperty("command").GetString());
                Assert.AreEqual("argument", parameters.GetProperty("arguments")[0].GetString());
                return new[] { "ok" };
            }

            [JsonRpcMethod("textDocument/didChange", UseSingleObjectParameterDeserialization = true)]
            public void DidChange(JsonElement parameters) => Changed.TrySetResult(parameters.Clone());
        }

        private sealed class FileChangeTarget {
            private readonly Uri _uri;

            public FileChangeTarget(Uri uri) {
                _uri = uri;
            }

            public TaskCompletionSource<JsonElement> Created { get; } =
                new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);

            [JsonRpcMethod("workspace/didChangeWatchedFiles", UseSingleObjectParameterDeserialization = true)]
            public void OnFilesChanged(JsonElement parameters) {
                foreach (var change in parameters.GetProperty("changes").EnumerateArray()) {
                    if (new Uri(change.GetProperty("uri").GetString()) == _uri &&
                        change.TryGetProperty("type", out var type) && type.GetInt32() == 1) {
                        Created.TrySetResult(change.Clone());
                    }
                }
            }
        }
    }
}
