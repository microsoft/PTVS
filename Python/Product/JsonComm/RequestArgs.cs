using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Cdp {
    public sealed class RequestArgs {
        private readonly string _command;
        private readonly Request _request;

        public RequestArgs(string command, Request request) {
            _command = command;
            _request = request;
        }

        public Request Request => _request;
        public string Command => _command;
    }
}
