using System;

namespace Microsoft.PythonTools.Cdp {
    class FailedRequestException : Exception {
        private readonly Response _response;

        public FailedRequestException(string message, Response response) : base(message) {
            _response = response;
        }

        public Response Response => _response;
    }
}
