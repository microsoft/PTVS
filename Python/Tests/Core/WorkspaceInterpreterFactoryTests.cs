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
	public class WorkspaceInterpreterFactoryTests
	{
		[ClassInitialize]
		public static void DoDeployment(TestContext context)
		{
			AssertListener.Initialize();
		}

		[TestMethod, Priority(UnitTestPriority.P1)]
		public void WatchWorkspaceFolderChanged()
		{
			var workspaceFolder1 = TestData.GetTempPath();
			Directory.CreateDirectory(workspaceFolder1);
			File.WriteAllText(Path.Combine(workspaceFolder1, "app1.py"), string.Empty);

			var workspaceFolder2 = TestData.GetTempPath();
			Directory.CreateDirectory(workspaceFolder2);
			File.WriteAllText(Path.Combine(workspaceFolder2, "app2.py"), string.Empty);

			var workspace1 = new WorkspaceTestHelper.MockWorkspace(workspaceFolder1);
			var workspace2 = new WorkspaceTestHelper.MockWorkspace(workspaceFolder2);

			var workspaceContext1 = new WorkspaceTestHelper.MockWorkspaceContext(workspace1);
			var workspaceContext2 = new WorkspaceTestHelper.MockWorkspaceContext(workspace2);

			var workspaceContextProvider = new WorkspaceTestHelper.MockWorkspaceContextProvider(workspaceContext1);

			using (var factoryProvider = new WorkspaceInterpreterFactoryProvider(workspaceContextProvider))
			{
				// Load a different workspace
				Action triggerDiscovery = () =>
				{
					workspaceContextProvider.SimulateChangeWorkspace(workspaceContext2);
				};

				TestTriggerDiscovery(workspaceContext1, triggerDiscovery, workspaceContextProvider, true);
			}
		}

		[TestMethod, Priority(UnitTestPriority.P1)]
		public void WatchWorkspaceSettingsChanged()
		{
			var workspaceFolder = TestData.GetTempPath();
			Directory.CreateDirectory(workspaceFolder);
			File.WriteAllText(Path.Combine(workspaceFolder, "app.py"), string.Empty);

			var workspace = new WorkspaceTestHelper.MockWorkspace(workspaceFolder);
			var workspaceContext = new WorkspaceTestHelper.MockWorkspaceContext(workspace);

			// Modify settings
			Action triggerDiscovery = () =>
			{
				workspaceContext.SimulateChangeInterpreterSetting("Global|PythonCore|3.7");
			};

			TestTriggerDiscovery(workspaceContext, triggerDiscovery, null, true);
		}

		[TestMethod, Priority(UnitTestPriority.P0)]
		public void WatchWorkspaceVirtualEnvCreated()
		{
			var python = PythonPaths.Python37_x64 ?? PythonPaths.Python37;

			var workspaceFolder = TestData.GetTempPath();
			Directory.CreateDirectory(workspaceFolder);
			File.WriteAllText(Path.Combine(workspaceFolder, "app.py"), string.Empty);

			string envFolder = Path.Combine(workspaceFolder, "env");

			var workspace = new WorkspaceTestHelper.MockWorkspace(workspaceFolder);
			var workspaceContext = new WorkspaceTestHelper.MockWorkspaceContext(workspace);

			// Create virtual env inside the workspace folder (one level from root)
			var configs = TestTriggerDiscovery(
				workspaceContext,
				() => python.CreateVirtualEnv(Path.Combine(workspaceFolder, "env"))
			).ToArray();

			Assert.AreEqual(1, configs.Length);
			Assert.IsTrue(PathUtils.IsSamePath(
				Path.Combine(envFolder, "scripts", "python.exe"),
				configs[0].InterpreterPath
			));
			Assert.AreEqual("Workspace|Workspace|env", configs[0].Id);
		}

		[TestMethod, Priority(UnitTestPriority.P0)]
		public void DetectLocalEnvOutsideWorkspace()
		{
			var python = PythonPaths.Python37_x64 ?? PythonPaths.Python37;

			var tempFolder = TestData.GetTempPath();
			var workspaceFolder = Path.Combine(tempFolder, "workspace");
			var envFolder = Path.Combine(tempFolder, "outside");

			Directory.CreateDirectory(workspaceFolder);
			File.WriteAllText(Path.Combine(workspaceFolder, "app.py"), string.Empty);

			// Create virtual env outside the workspace folder
			using (var p = ProcessOutput.RunHiddenAndCapture(python.InterpreterPath, "-m", "venv", envFolder))
			{
				Console.WriteLine(p.Arguments);
				p.Wait();
				Console.WriteLine(string.Join(Environment.NewLine, p.StandardOutputLines.Concat(p.StandardErrorLines)));
				Assert.AreEqual(0, p.ExitCode);
			}

			// Normally a local virtual environment outside the workspace
			// wouldn't be detected, but it is when it's referenced from
			// the workspace python settings.
			var workspace = new WorkspaceTestHelper.MockWorkspace(workspaceFolder);
			var workspaceContext = new WorkspaceTestHelper.MockWorkspaceContext(workspace, @"..\outside\scripts\python.exe");
			var workspaceContextProvider = new WorkspaceTestHelper.MockWorkspaceContextProvider(workspaceContext);

			using (var factoryProvider = new WorkspaceInterpreterFactoryProvider(workspaceContextProvider))
			{
				workspaceContextProvider.SimulateChangeWorkspace(workspaceContext);
				var configs = factoryProvider.GetInterpreterConfigurations().ToArray();

				Assert.AreEqual(1, configs.Length);
				Assert.IsTrue(PathUtils.IsSamePath(
					Path.Combine(envFolder, "scripts", "python.exe"),
					configs[0].InterpreterPath
				));
				Assert.AreEqual("Workspace|Workspace|outside", configs[0].Id);
			}
		}

		[TestMethod, Priority(UnitTestPriority.P1_FAILING)]
		public void WatchWorkspaceVirtualEnvRenamed()
		{
			const string ENV_NAME = "env";
			var workspaceContext = CreateEnvAndGetWorkspaceService(ENV_NAME);

			string envPath = Path.Combine(workspaceContext.Location, ENV_NAME);
			string renamedEnvPath = Path.Combine(workspaceContext.Location, string.Concat(ENV_NAME, "1"));
			var configs = TestTriggerDiscovery(workspaceContext, () => Directory.Move(envPath, renamedEnvPath)).ToArray();

			Assert.AreEqual(1, configs.Length);
			Assert.IsTrue(PathUtils.IsSamePath(Path.Combine(renamedEnvPath, "scripts", "python.exe"), configs[0].InterpreterPath));
			Assert.AreEqual("Workspace|Workspace|env1", configs[0].Id);
		}

		[TestMethod, Priority(UnitTestPriority.P1)]
		public void WatchWorkspaceVirtualEnvDeleted()
		{
			const string ENV_NAME = "env";
			var workspaceContext = CreateEnvAndGetWorkspaceService(ENV_NAME);

			string envPath = Path.Combine(workspaceContext.Location, ENV_NAME);
			var configs = TestTriggerDiscovery(workspaceContext, () => Directory.Delete(envPath, true)).ToArray();

			Assert.AreEqual(0, configs.Length);
		}

		private static IEnumerable<InterpreterConfiguration> TestTriggerDiscovery(
				IPythonWorkspaceContext workspaceContext,
				Action triggerDiscovery,
				IPythonWorkspaceContextProvider workspaceContextProvider = null,
				bool useDiscoveryStartedEvent = false
		)
		{
			workspaceContextProvider = workspaceContextProvider
				?? new WorkspaceTestHelper.MockWorkspaceContextProvider(workspaceContext);

			using (var provider = new WorkspaceInterpreterFactoryProvider(workspaceContextProvider))
			using (var evt = new AutoResetEvent(false))
			{
				// This initializes the provider, discovers the initial set
				// of factories and starts watching the filesystem.
				provider.GetInterpreterFactories();

				if (useDiscoveryStartedEvent)
				{
					provider.DiscoveryStarted += (sender, e) =>
					{
						evt.Set();
					};
				}
				else
				{
					provider.InterpreterFactoriesChanged += (sender, e) =>
					{
						evt.Set();
					};
				}

				triggerDiscovery();
				Assert.IsTrue(evt.WaitOne(5000), "Failed to trigger discovery.");
				return provider.GetInterpreterConfigurations();
			}
		}

		private WorkspaceTestHelper.MockWorkspaceContext CreateEnvAndGetWorkspaceService(string envName)
		{
			var python = PythonPaths.Python37_x64 ?? PythonPaths.Python37;

			var workspacePath = TestData.GetTempPath();
			Directory.CreateDirectory(workspacePath);
			File.WriteAllText(Path.Combine(workspacePath, "app.py"), string.Empty);

			python.CreateVirtualEnv(Path.Combine(workspacePath, envName));

			return new WorkspaceTestHelper.MockWorkspaceContext(new WorkspaceTestHelper.MockWorkspace(workspacePath));
		}

	}
}
