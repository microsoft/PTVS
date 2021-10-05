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

namespace TestRunnerInterop
{
	static partial class FileUtils
	{
		private sealed class FileRestorer : IDisposable
		{
			private readonly string _original, _backup;

			public FileRestorer(string original, string backup)
			{
				_original = original;
				_backup = backup;
			}

			public void Dispose()
			{
				for (int retries = 10; retries > 0; --retries)
				{
					try
					{
						File.Delete(_original);
						File.Move(_backup, _original);
						return;
					}
					catch (IOException)
					{
					}
					catch (UnauthorizedAccessException)
					{
						try
						{
							File.SetAttributes(_original, FileAttributes.Normal);
						}
						catch (IOException)
						{
						}
						catch (UnauthorizedAccessException)
						{
						}
					}
					Thread.Sleep(100);
				}

				Debug.Fail($"Failed to restore {_original} from {_backup}");
			}
		}
	}
}
