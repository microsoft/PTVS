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

namespace Microsoft.PythonTools.Debugger.Transports
{
	internal class TcpTransport : IDebuggerTransport
	{
		public const ushort DefaultPort = 5678;

		public Exception Validate(Uri uri)
		{
			if (uri.AbsolutePath != "/")
			{
				return new FormatException(Strings.DebugTcpTransportUriCannotContainPath);
			}
			return null;
		}

		public virtual Stream Connect(Uri uri, bool requireAuthentication)
		{
			if (uri.Port < 0)
			{
				uri = new UriBuilder(uri) { Port = DefaultPort }.Uri;
			}

			// PTVSD is using AF_INET by default, so lets make sure to try the IPv4 address in lieu of IPv6 address
			var tcpClient = new TcpClient(AddressFamily.InterNetwork);

			try
			{
				tcpClient.Connect(uri.Host, uri.Port);
				var stream = tcpClient.GetStream();
				tcpClient = null;
				return stream;
			}
			catch (IOException ex)
			{
				throw new ConnectionException(ConnErrorMessages.RemoteNetworkError, ex);
			}
			finally
			{
				if (tcpClient != null)
				{
					tcpClient.Close();
				}
			}
		}
	}
}
