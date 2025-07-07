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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.IO;
using System.Web;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Debugger {
    public class WebSocketProxy : WebSocketProxyBase {
        public override int DebuggerPort {
            get { return 5678; }
        }

        public override bool AllowConcurrentConnections {
            // We don't actually support more than one debugger connection, but the debugger will take care of rejecting the extra ones.
            // We do however use two different connections for debugger and debug REPL, so this must be true.
            get { return true; }
        }

        public override void ProcessHelpPageRequest(HttpContext context) {
            using (var stream = GetType().Assembly.GetManifestResourceStream("Microsoft.PythonTools.WebRole.WebSocketProxy.html"))
            using (var reader = new StreamReader(stream)) {
                string html = reader.ReadToEnd();
                var wsUri = new UriBuilder(context.Request.Url) { Scheme = "wss", Port = -1, UserName = "secret" }.ToString();
                wsUri = wsUri.Replace("secret@", "<span class='secret'>secret</span>@");
                context.Response.Write(html.Replace("{{WS_URI}}", wsUri)); // CodeQL [SM00430] its the same URL they hit just with a corrected extension so safe.
                context.Response.End();
            }
        }
    }
}

