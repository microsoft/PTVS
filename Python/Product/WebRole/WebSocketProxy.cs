/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

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
                context.Response.Write(html.Replace("{{WS_URI}}", wsUri));
                context.Response.End();
            }
        }
    }
}

