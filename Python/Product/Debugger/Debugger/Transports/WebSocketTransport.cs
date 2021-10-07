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

using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Debugger.Transports
{
	internal class WebSocketTransport : IDebuggerTransport
	{
		public Exception Validate(Uri uri)
		{
			return null;
		}

		public virtual Stream Connect(Uri uri, bool requireAuthentication)
		{
			// IIS starts python.exe processes lazily on the first incoming request, and will terminate them after a period
			// of inactivity, making it impossible to attach. So before trying to connect to the debugger, "ping" the website
			// via HTTP to ensure that we have something to connect to.
			try
			{
				var httpRequest = WebRequest.Create(new UriBuilder(uri) { Scheme = "http", Port = -1, Path = "/" }.Uri);
				httpRequest.Method = WebRequestMethods.Http.Head;
				httpRequest.Timeout = 5000;
				httpRequest.GetResponse().Dispose();
			}
			catch (WebException)
			{
				// If it fails or times out, just go ahead and try to connect anyway, and rely on normal error reporting path.
			}

			var webSocket = new ClientWebSocket();
			try
			{
				webSocket.ConnectAsync(uri, CancellationToken.None).GetAwaiter().GetResult();
				WebSocketStream stream = new WebSocketStream(webSocket, ownsSocket: true);
				webSocket = null;
				return stream;
			}
			catch (WebSocketException ex)
			{
				throw new ConnectionException(ConnErrorMessages.RemoteNetworkError, ex);
			}
			catch (IOException ex)
			{
				throw new ConnectionException(ConnErrorMessages.RemoteNetworkError, ex);
			}
			catch (PlatformNotSupportedException ex)
			{
				throw new ConnectionException(ConnErrorMessages.RemoteUnsupportedTransport, ex);
			}
			finally
			{
				if (webSocket != null)
				{
					webSocket.Dispose();
				}
			}
		}
	}
}
