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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.PythonTools.Cdp {
    public abstract class RequestInfo : IDisposable {
        private readonly Request _request;
        private readonly Connection _connection;
        private readonly int _sequence;

        internal RequestInfo(Connection conncetion, Request request, int sequence) {
            _connection = conncetion;
            _request = request;
            _sequence = sequence;
        }

        ~RequestInfo() {
            Dispose(false);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public virtual void Dispose(bool disposing) {
            _connection.ClearSequence(_sequence);
        }

        public Request Request => _request;

        internal abstract void SetResponse(JToken obj);
        internal abstract void Cancel();
    }

    public sealed class RequestInfo<TResponse> : RequestInfo where TResponse : Response, new() {
        private TResponse _response;
        internal TaskCompletionSource<RequestInfo<TResponse>> _task;

        internal RequestInfo(Connection connection, Request request, int sequence) : base(connection, request, sequence) {
            _task = new TaskCompletionSource<RequestInfo<TResponse>>();
        }

        public override void Dispose(bool disposing) {
            _task?.Task.Dispose();
            base.Dispose(disposing);
        }

        internal override void SetResponse(JToken obj) {
            _response = obj.ToObject<TResponse>();
            _task?.TrySetResult(this);
        }

        internal override void Cancel() {
            _task?.TrySetCanceled();
        }

        public TResponse Response {
            get { return _response; }
        }
    }

    public class Response {
        public bool success;
        public string message;
    }
}