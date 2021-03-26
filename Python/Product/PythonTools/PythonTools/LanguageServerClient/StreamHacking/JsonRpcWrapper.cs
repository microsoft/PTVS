using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StreamJsonRpc;

namespace Microsoft.PythonTools.LanguageServerClient.StreamHacking {
    class JsonRpcWrapper : IDisposable {
        private System.IO.Stream _output;
        private bool disposedValue;
        private JsonRpc _rpc;
        private object _target;
        private Func<StreamData, StreamData> _writeHandler;


        public JsonRpcWrapper(System.IO.Stream readStream, System.IO.Stream writeStream, object target, Func<StreamData, StreamData> writeHandler) {
            _writeHandler = writeHandler;
            _output = new StreamIntercepter(writeStream, this.MimicWrites, (a) => { });
            _rpc = new JsonRpc(_output, readStream, target);
            _target = target;
        }

        public void StartListening(JsonRpc passThrough) {
            _rpc.AddRemoteRpcTarget(passThrough);
            _rpc.StartListening();
        }

        // Reading from this will do nothing as all reading should happen as a result of the remote rpc target.
        public System.IO.Stream InputStream => new BlockingReadStream();
        public System.IO.Stream OutputStream => _output;
        public object Target => _target;


        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    _rpc.Dispose();
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

        private StreamData MimicWrites(StreamData data) {
            // Outer jsonrpc is writing a message. We have to send this to our inner rpc so it sees it too. Otherwise responses
            // that come in will cause a disconnection because they don't match a request
            var message = MessageParser.Deserialize(data);
            if (message != null && message["method"] != null) {
                _rpc.InvokeAsync(message["method"].ToString(), message["params"]);
            }
            return _writeHandler(data);
        }
    }
}
