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

namespace Microsoft.PythonTools
{
	internal class OnPortOpenedHandler
	{
		private class OnPortOpenedInfo
		{
			public readonly int Port;
			public readonly TimeSpan Timeout;
			public readonly int Sleep;
			public readonly Func<bool> ShortCircuitPredicate;
			public readonly Action Action;
			public readonly DateTime StartTime;

			public OnPortOpenedInfo(
				int port,
				int? timeout = null,
				int? sleep = null,
				Func<bool> shortCircuitPredicate = null,
				Action action = null
			)
			{
				Port = port;
				Timeout = TimeSpan.FromMilliseconds(timeout ?? 60000);  // 1 min timeout
				Sleep = sleep ?? 500;                                   // 1/2 second sleep
				ShortCircuitPredicate = shortCircuitPredicate ?? (() => false);
				Action = action ?? (() => { });
				StartTime = System.DateTime.Now;
			}
		}

		internal static void CreateHandler(
			int port,
			int? timeout = null,
			int? sleep = null,
			Func<bool> shortCircuitPredicate = null,
			Action action = null
		)
		{
			ThreadPool.QueueUserWorkItem(
				OnPortOpened,
				new OnPortOpenedInfo(
					port,
					timeout,
					sleep,
					shortCircuitPredicate,
					action
				)
			);
		}

		private static void OnPortOpened(object infoObj)
		{
			OnPortOpenedInfo info = (OnPortOpenedInfo)infoObj;

			using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
			{
				socket.Blocking = true;
				while (true)
				{
					// Short circuit
					if (info.ShortCircuitPredicate())
					{
						return;
					}

					// Try connect
					try
					{
						socket.Connect(IPAddress.Loopback, info.Port);
						break;
					}
					catch
					{
						// Connect failure
						// Fall through
					}

					// Timeout
					if ((System.DateTime.Now - info.StartTime) >= info.Timeout)
					{
						break;
					}

					// Sleep
					System.Threading.Thread.Sleep(info.Sleep);
				}
			}

			// Launch browser (if not short-circuited)
			if (!info.ShortCircuitPredicate())
			{
				info.Action();
			}
		}
	}
}
