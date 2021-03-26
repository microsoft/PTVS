using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StreamJsonRpc.Protocol;
using StreamJsonRpc.Reflection;

namespace Microsoft.PythonTools.LanguageServerClient {
    // Basic idea:
    // LC writes a notification. This will write directly (just a pass through to the write stream)
    // RPC server receives a response. We handle in our target first, then turn into a message and write to the read stream?
    // How to handle all messages? Use message handler. 
    class LanguageServerProxy: IDisposable {
        private System.IO.Stream _output;
        private StreamJsonRpc.HeaderDelimitedMessageHandler _handler;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly object syncObject = new object();
        private Task readLinesTask;
        private bool disposedValue;

        /// <summary>
        /// The source for the <see cref="DisconnectedToken"/> property.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
#pragma warning disable CA2213 // Disposable fields should be disposed
        private readonly CancellationTokenSource disconnectedSource = new CancellationTokenSource();
#pragma warning restore CA2213 // Disposable fields should be disposed

        /// <summary>
        /// Gets a token that is cancelled when the connection is lost.
        /// </summary>
        internal CancellationToken DisconnectedToken => this.disconnectedSource.Token;

        public LanguageServerProxy(System.IO.Stream readStream, System.IO.Stream writeStream) {
            _handler = new StreamJsonRpc.HeaderDelimitedMessageHandler(null, readStream);
        }

        public void StartListening() {
            lock (this.syncObject) {
                this.readLinesTask = Task.Run(this.ReadAndHandleRequestsAsync, this.DisconnectedToken);
            }
        }

        public System.IO.Stream InputStream => new System.IO.MemoryStream();
        public System.IO.Stream OutputStream => _output;

        private async Task ReadAndHandleRequestsAsync() {
            lock (this.syncObject) {
                // This block intentionally left blank.
                // It ensures that this thread will not receive messages before our caller (StartListening)
                // assigns the Task we return to a field before we go any further,
                // since our caller holds this lock until the field assignment completes.
                // See the StartListening_ShouldNotAllowIncomingMessageToRaceWithInvokeAsync test.
            }

            try {
                while (!this.disposedValue && !this.DisconnectedToken.IsCancellationRequested) {
                    JsonRpcMessage? protocolMessage = null;
                    try {
                        protocolMessage = await _handler.ReadAsync(this.DisconnectedToken).ConfigureAwait(false);
                        if (protocolMessage == null) {
                            return;
                        }
                    } catch (OperationCanceledException) {
                        break;
                    } catch (ObjectDisposedException) {
                        break;
                    }
#pragma warning disable CA1031 // Do not catch general exception types
                    catch
#pragma warning restore CA1031 // Do not catch general exception types
                    {
                        return;
                    }

                    this.HandleRpcAsync(protocolMessage).Forget(); // all exceptions are handled internally

                    // We must clear buffers before reading the next message.
                    // HandleRpcAsync must do whatever deserialization it requires before it yields.
                    (this._handler as IJsonRpcMessageBufferManager)?.DeserializationComplete(protocolMessage);
                }

            } catch {
                throw;
            }
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~LanguageServerProxy()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose() {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
