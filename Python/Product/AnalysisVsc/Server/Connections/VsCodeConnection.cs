// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Based on https://github.com/CXuesong/LanguageServer.NET

using System;
using System.IO;
using System.Reflection;
using System.Threading;
using JsonRpc.Standard.Client;
using JsonRpc.Standard.Contracts;
using JsonRpc.Standard.Server;
using JsonRpc.Streams;
using LanguageServer.VsCode;
using Microsoft.Common.Core.Services;
using Microsoft.R.LanguageServer.Server.Settings;

namespace Microsoft.PythonTools.VsCode.Server {
    /// <summary>
    /// Represents connection to VsCode.
    /// Listens on stdin/stdout for the language protocol JSON RPC
    /// </summary>
    internal sealed class VsCodeConnection {
        private readonly IServiceManager _serviceManager;

        public VsCodeConnection(IServiceManager serviceManager) {
            _serviceManager = serviceManager;
        }

        public void Connect(bool debugMode) {
            var logWriter = CreateLogWriter(true);

            using (logWriter)
            using (var cin = Console.OpenStandardInput())
            using (var bcin = new BufferedStream(cin))
            using (var cout = Console.OpenStandardOutput())
            using (var reader = new PartwiseStreamMessageReader(bcin))
            using (var writer = new PartwiseStreamMessageWriter(cout)) {
                var contractResolver = new JsonRpcContractResolver {
                    NamingStrategy = new CamelCaseJsonRpcNamingStrategy(),
                    ParameterValueConverter = new CamelCaseJsonValueConverter(),
                };
                var clientHandler = new StreamRpcClientHandler();
                var client = new JsonRpcClient(clientHandler);
                if (debugMode) {
                    // We want to capture log all the LSP server-to-client calls as well
                    clientHandler.MessageSending += (_, e) => {
                        lock (logWriter) {
                            logWriter.WriteLine("<C{0}", e.Message);
                        }
                    };
                    clientHandler.MessageReceiving += (_, e) => {
                        lock (logWriter) {
                            logWriter.WriteLine(">C{0}", e.Message);
                        }
                    };
                }

                var session = new LanguageServerSession(client, contractResolver);
                _serviceManager.AddService(new SettingsManager(_serviceManager));
                _serviceManager.AddService(new VsCodeClient(session.Client, _serviceManager));
                _serviceManager.AddService(new Controller(_serviceManager));

                // Configure & build service host
                var host = BuildServiceHost(logWriter, contractResolver, debugMode);
                var serverHandler = new StreamRpcServerHandler(host,
                    StreamRpcServerHandlerOptions.ConsistentResponseSequence |
                    StreamRpcServerHandlerOptions.SupportsRequestCancellation);
                serverHandler.DefaultFeatures.Set(session);

                var cts = new CancellationTokenSource();
                // If we want server to stop, just stop the "source"
                using (serverHandler.Attach(reader, writer))
                using (clientHandler.Attach(reader, writer))
                using (new RConnection(_serviceManager, cts.Token)) {
                    // Wait for the "stop" request.
                    session.CancellationToken.WaitHandle.WaitOne();
                    cts.Cancel();
                }
                logWriter?.WriteLine("Exited");
            }
        }

        private static IJsonRpcServiceHost BuildServiceHost(TextWriter logWriter,
            IJsonRpcContractResolver contractResolver, bool debugMode) {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(new DebugLoggerProvider(null));

            var builder = new JsonRpcServiceHostBuilder {
                ContractResolver = contractResolver,
                LoggerFactory = loggerFactory
            };

            builder.UseCancellationHandling();
            builder.Register(typeof(Program).GetTypeInfo().Assembly);

            if (debugMode) {
                // Log all the client-to-server calls.
                builder.Intercept(async (context, next) => {
                    lock (logWriter) {
                        logWriter.WriteLine("> {0}", context.Request);
                    }

                    await next();

                    lock (logWriter) {
                        logWriter.WriteLine("< {0}", context.Response);
                    }
                });
            }
            return builder.Build();
        }

        private static StreamWriter CreateLogWriter(bool debugMode) {
            StreamWriter logWriter = null;
            if (debugMode) {
                var tempPath = Path.GetTempPath();
                var fileName = "VSCode_R_JsonRPC-" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".log";
                logWriter = File.CreateText(Path.Combine(tempPath, fileName));
                logWriter.AutoFlush = true;
            }
            return logWriter;
        }
    }
}
