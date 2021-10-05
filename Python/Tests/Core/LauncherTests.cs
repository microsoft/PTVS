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

namespace PythonToolsTests
{
	[TestClass]
	public class LauncherTests
	{
		[TestMethod, Priority(UnitTestPriority.P0)]
		public void LaunchWebBrowserUriTests()
		{
			var testCases = new[] {
				new { Url = "/fob", Port = 1, Expected = "http://localhost:1/fob" },
				new { Url = "http://localhost:9999/fob", Port = 9999, Expected = "http://localhost:9999/fob" },
				new { Url = "http://localhost/fob", Port = 9999, Expected = "http://localhost:9999/fob" },
				new { Url = "fob", Port = 9999, Expected = "http://localhost:9999/fob" },
				new { Url = "/hello/world", Port = 367, Expected = "http://localhost:367/hello/world" },
				new { Url = "/fob", Port = -1, Expected = "http://localhost:{port}/fob" },
			};

			foreach (var testCase in testCases)
			{
				Console.WriteLine("{0} {1} == {2}", testCase.Url, testCase.Port, testCase.Expected);


				var config = new LaunchConfiguration(null, new Dictionary<string, string> {
					{ PythonConstants.WebBrowserUrlSetting, testCase.Url }
				});
				if (testCase.Port >= 0)
				{
					config.LaunchOptions[PythonConstants.WebBrowserPortSetting] = testCase.Port.ToString();
				}
				PythonWebLauncher.GetFullUrl(null, config, out Uri url, out global::System.Int32 port);
				Assert.AreEqual(
					testCase.Expected.Replace("{port}", port.ToString()),
					url.AbsoluteUri
				);
			}
		}
	}
}
