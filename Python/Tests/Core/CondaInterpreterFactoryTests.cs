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
	public class CondaInterpreterFactoryTests
	{
		[ClassInitialize]
		public static void DoDeployment(TestContext context)
		{
			AssertListener.Initialize();
		}

		[TestMethod, Priority(UnitTestPriority.P1)]
		public void CondaWatchEnvironmentsTxtWithoutCondafolder()
		{
			// We start with no .conda folder
			var userProfileFolder = TestData.GetTempPath();
			string condaFolder = Path.Combine(userProfileFolder, ".conda");

			// We create .conda folder and environments.txt
			Action triggerDiscovery = () =>
			{
				Directory.CreateDirectory(condaFolder);
				File.WriteAllText(Path.Combine(condaFolder, "environments.txt"), string.Empty);
			};

			TestTriggerDiscovery(userProfileFolder, triggerDiscovery);
		}

		[TestMethod, Priority(UnitTestPriority.P1)]
		public void CondaWatchEnvironmentsTxtWithCondafolder()
		{
			// We start with a .conda folder but no environments.txt
			var userProfileFolder = TestData.GetTempPath();
			string condaFolder = Path.Combine(userProfileFolder, ".conda");
			Directory.CreateDirectory(condaFolder);

			// We create environments.txt
			Action triggerDiscovery = () =>
			{
				File.WriteAllText(Path.Combine(condaFolder, "environments.txt"), string.Empty);
			};

			TestTriggerDiscovery(userProfileFolder, triggerDiscovery);
		}

		[TestMethod, Priority(UnitTestPriority.P0)]
		public void CondaWatchEnvironmentsTxtWithCondafolderAndEnvTxt()
		{
			// We start with a .conda folder and environments.txt
			var userProfileFolder = TestData.GetTempPath();
			string condaFolder = Path.Combine(userProfileFolder, ".conda");
			Directory.CreateDirectory(condaFolder);
			File.WriteAllText(Path.Combine(condaFolder, "environments.txt"), string.Empty);

			// We modify environments.txt
			Action triggerDiscovery = () =>
			{
				File.WriteAllText(Path.Combine(condaFolder, "environments.txt"), string.Empty);
			};

			TestTriggerDiscovery(userProfileFolder, triggerDiscovery);
		}

		private static void TestTriggerDiscovery(string userProfileFolder, Action triggerDiscovery)
		{
			using (var evt = new AutoResetEvent(false))
			using (var globalProvider = new CPythonInterpreterFactoryProvider(null, false))
			using (var condaProvider = new CondaEnvironmentFactoryProvider(globalProvider, null, new JoinableTaskFactory(new JoinableTaskContext()), true, userProfileFolder))
			{
				// This initializes the provider, discovers the initial set
				// of factories and starts watching the filesystem.
				var configs = condaProvider.GetInterpreterConfigurations();
				condaProvider.DiscoveryStarted += (sender, e) =>
				{
					evt.Set();
				};
				triggerDiscovery();
				Assert.IsTrue(evt.WaitOne(5000), "Failed to trigger discovery.");
			}
		}
	}
}
