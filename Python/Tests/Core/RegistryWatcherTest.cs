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
	public class RegistryWatcherTest
	{
		const string TESTKEY = @"Software\PythonToolsTestKey";
		const int TIMEOUT = 500;

		[TestInitialize]
		public void CreateTestKey()
		{
			Registry.CurrentUser.CreateSubKey(TESTKEY).Close();
		}

		[TestCleanup]
		public void RemoveTestKey()
		{
			Registry.CurrentUser.DeleteSubKeyTree(TESTKEY, throwOnMissingSubKey: false);
		}

		void SetValue(string subkey, string name, object value)
		{
			using (var reg1 = Registry.CurrentUser.OpenSubKey(TESTKEY, true))
			using (var reg2 = reg1.CreateSubKey(subkey))
			{
				reg2.SetValue(name, value);
			}
		}

		void DeleteValue(string subkey, string name)
		{
			using (var reg1 = Registry.CurrentUser.OpenSubKey(TESTKEY, true))
			using (var reg2 = reg1.CreateSubKey(subkey))
			{
				reg2.DeleteValue(name, throwOnMissingValue: false);
			}
		}

		void DeleteKey(string subkey)
		{
			using (var reg1 = Registry.CurrentUser.OpenSubKey(TESTKEY, true))
			{
				reg1.DeleteSubKeyTree(subkey, throwOnMissingSubKey: false);
			}
		}

		string GetKey(string subkey)
		{
			return TESTKEY + "\\" + subkey;
		}

		object AddWatch(RegistryWatcher watcher,
						string subkey,
						Action<RegistryChangedEventArgs> callback,
						bool recursive = false,
						bool notifyValueChange = true,
						bool notifyKeyChange = true)
		{
			return watcher.Add(
				RegistryHive.CurrentUser,
				RegistryView.Default,
				GetKey(subkey),
				(s, e) => { callback(e); },
				recursive,
				notifyValueChange,
				notifyKeyChange);
		}

		[TestMethod, Priority(UnitTestPriority.P1)]
		public void RegistryWatcherUpdateNonRecursive()
		{
			string keyName = "RegistryWatcherUpdateNonRecursive";

			SetValue(keyName, "TestValue", "ABC");
			SetValue(keyName + "\\TestKey", "Value", 123);

			using (var watcher = new RegistryWatcher())
			{
				RegistryChangedEventArgs args = null;
				var argsSet = new ManualResetEventSlim();

				var watch1 = AddWatch(watcher, keyName,
					e => { args = e; argsSet.Set(); });

				// Value is set, but does not actually change
				SetValue(keyName, "TestValue", "ABC");
				Assert.IsFalse(argsSet.Wait(TIMEOUT));
				Assert.IsNull(args);

				// Value is changed
				SetValue(keyName, "TestValue", "DEF");
				Assert.IsTrue(argsSet.Wait(TIMEOUT));
				Assert.IsNotNull(args);
				Assert.AreEqual(GetKey(keyName), args.Key);
				argsSet.Reset();
				args = null;

				// Value in subkey is changed, but we don't notice
				SetValue(keyName + "\\TestKey", "Value", 456);
				Assert.IsFalse(argsSet.Wait(TIMEOUT));
				Assert.IsNull(args);

				watcher.Remove(watch1);

				// Value is changed back, but we don't get notified
				SetValue(keyName, "TestValue", "ABC");
				Assert.IsFalse(argsSet.Wait(TIMEOUT));
				Assert.IsNull(args);

				// Value in subkey is changed back, but we don't notice
				SetValue(keyName + "\\TestKey", "Value", 123);
				Assert.IsFalse(argsSet.Wait(TIMEOUT));
				Assert.IsNull(args);
			}
		}

		[TestMethod, Priority(UnitTestPriority.P1)]
		public void RegistryWatcherAddNonRecursive()
		{
			string keyName = "RegistryWatcherAddNonRecursive";

			SetValue(keyName, "TestValue", "ABC");
			SetValue(keyName + "\\TestKey", "Value", 123);

			using (var watcher = new RegistryWatcher())
			{
				RegistryChangedEventArgs args = null;
				var argsSet = new ManualResetEventSlim();

				var watch1 = AddWatch(watcher, keyName,
					e => { args = e; argsSet.Set(); });

				// Value is created
				SetValue(keyName, "TestValue2", "DEF");
				Assert.IsTrue(argsSet.Wait(TIMEOUT));
				Assert.IsNotNull(args);
				Assert.AreEqual(GetKey(keyName), args.Key);
				argsSet.Reset();
				args = null;

				// Value in subkey is added, but we don't notice
				SetValue(keyName + "\\TestKey", "Value2", 456);
				Assert.IsFalse(argsSet.Wait(TIMEOUT));
				Assert.IsNull(args);

				// Subkey is added
				SetValue(keyName + "\\TestKey2", "Value", 789);
				Assert.IsTrue(argsSet.Wait(TIMEOUT));
				Assert.IsNotNull(args);
				Assert.AreEqual(GetKey(keyName), args.Key);
				argsSet.Reset();
				args = null;

				watcher.Remove(watch1);

				// Another value is created, but we don't get notified
				SetValue(keyName, "TestValue3", "GHI");
				Assert.IsFalse(argsSet.Wait(TIMEOUT));
				Assert.IsNull(args);

				// Another value in subkey is added, but we don't notice
				SetValue(keyName + "\\TestKey", "Value3", 789);
				Assert.IsFalse(argsSet.Wait(TIMEOUT));
				Assert.IsNull(args);
			}
		}

		[TestMethod, Priority(UnitTestPriority.P1)]
		public void RegistryWatcherDeleteNonRecursive()
		{
			string keyName = "RegistryWatcherDeleteNonRecursive";

			SetValue(keyName, "TestValue1", "ABC");
			SetValue(keyName, "TestValue2", "DEF");
			SetValue(keyName, "TestValue3", "GHI");
			SetValue(keyName + "\\TestKey1", "Value", 123);
			SetValue(keyName + "\\TestKey2", "Value", 456);

			using (var watcher = new RegistryWatcher())
			{
				RegistryChangedEventArgs args = null;
				var argsSet = new ManualResetEventSlim();

				var watch1 = AddWatch(watcher, keyName,
					e => { args = e; argsSet.Set(); });

				// Value is deleted
				DeleteValue(keyName, "TestValue2");
				Assert.IsTrue(argsSet.Wait(TIMEOUT));
				Assert.IsNotNull(args);
				Assert.AreEqual(GetKey(keyName), args.Key);
				argsSet.Reset();
				args = null;

				// Value in subkey is deleted, and we don't notice
				DeleteValue(keyName + "\\TestKey1", "Value");
				Assert.IsFalse(argsSet.Wait(TIMEOUT));
				Assert.IsNull(args);

				// Subkey is deleted
				DeleteKey(keyName + "\\TestKey1");
				Assert.IsTrue(argsSet.Wait(TIMEOUT));
				Assert.IsNotNull(args);
				Assert.AreEqual(GetKey(keyName), args.Key);
				argsSet.Reset();
				args = null;

				watcher.Remove(watch1);

				// Another value is deleted, but we don't get notified
				DeleteValue(keyName, "TestValue3");
				Assert.IsFalse(argsSet.Wait(TIMEOUT));
				Assert.IsNull(args);

				// Another key is deleted, but we don't get notified
				DeleteKey(keyName + "\\TestKey2");
				Assert.IsFalse(argsSet.Wait(TIMEOUT));
				Assert.IsNull(args);
			}
		}



		[TestMethod, Priority(UnitTestPriority.P1)]
		public void RegistryWatcherUpdateRecursive()
		{
			string keyName = "RegistryWatcherUpdateRecursive";

			SetValue(keyName, "TestValue", "ABC");
			SetValue(keyName + "\\TestKey", "Value", 123);

			using (var watcher = new RegistryWatcher())
			{
				RegistryChangedEventArgs args = null;
				var argsSet = new ManualResetEventSlim();

				var watch1 = AddWatch(watcher, keyName,
					e => { args = e; argsSet.Set(); },
					recursive: true);

				// Value is set, but does not actually change
				SetValue(keyName, "TestValue", "ABC");
				Assert.IsFalse(argsSet.Wait(TIMEOUT));
				Assert.IsNull(args);

				// Value is changed
				SetValue(keyName, "TestValue", "DEF");
				Assert.IsTrue(argsSet.Wait(TIMEOUT));
				Assert.IsNotNull(args);
				Assert.AreEqual(GetKey(keyName), args.Key);
				argsSet.Reset();
				args = null;

				// Value in subkey is changed
				SetValue(keyName + "\\TestKey", "Value", 456);
				Assert.IsTrue(argsSet.Wait(TIMEOUT));
				Assert.IsNotNull(args);
				Assert.AreEqual(GetKey(keyName), args.Key);
				argsSet.Reset();
				args = null;

				watcher.Remove(watch1);

				// Value is changed back, but we don't get notified
				SetValue(keyName, "TestValue", "ABC");
				Assert.IsFalse(argsSet.Wait(TIMEOUT));
				Assert.IsNull(args);

				// Value in subkey is changed back, but we don't notice
				SetValue(keyName + "\\TestKey", "Value", 123);
				Assert.IsFalse(argsSet.Wait(TIMEOUT));
				Assert.IsNull(args);
			}
		}

		[TestMethod, Priority(UnitTestPriority.P1)]
		public void RegistryWatcherAddRecursive()
		{
			string keyName = "RegistryWatcherAddRecursive";

			SetValue(keyName, "TestValue", "ABC");
			SetValue(keyName + "\\TestKey", "Value", 123);

			using (var watcher = new RegistryWatcher())
			{
				RegistryChangedEventArgs args = null;
				var argsSet = new ManualResetEventSlim();

				var watch1 = AddWatch(watcher, keyName,
					e => { args = e; argsSet.Set(); },
					recursive: true);

				// Value is created
				SetValue(keyName, "TestValue2", "DEF");
				Assert.IsTrue(argsSet.Wait(TIMEOUT));
				Assert.IsNotNull(args);
				Assert.IsTrue(args.IsRecursive);
				Assert.AreEqual(GetKey(keyName), args.Key);
				argsSet.Reset();
				args = null;

				// Subkey is added
				SetValue(keyName + "\\TestKey2", "Value", 789);
				Assert.IsTrue(argsSet.Wait(TIMEOUT));
				Assert.IsNotNull(args);
				Assert.AreEqual(GetKey(keyName), args.Key);
				argsSet.Reset();
				args = null;

				// Value in subkey is added
				SetValue(keyName + "\\TestKey", "Value2", 456);
				Assert.IsTrue(argsSet.Wait(TIMEOUT));
				Assert.IsNotNull(args);
				Assert.AreEqual(GetKey(keyName), args.Key);
				argsSet.Reset();
				args = null;

				watcher.Remove(watch1);

				// Another value is created, but we don't get notified
				SetValue(keyName, "TestValue3", "GHI");
				Assert.IsFalse(argsSet.Wait(TIMEOUT));
				Assert.IsNull(args);

				// Another value in subkey is added, but we don't notice
				SetValue(keyName + "\\TestKey", "Value3", 789);
				Assert.IsFalse(argsSet.Wait(TIMEOUT));
				Assert.IsNull(args);
			}
		}

		[TestMethod, Priority(UnitTestPriority.P1)]
		public void RegistryWatcherDeleteRecursive()
		{
			string keyName = "RegistryWatcherDeleteRecursive";

			SetValue(keyName, "TestValue1", "ABC");
			SetValue(keyName, "TestValue2", "DEF");
			SetValue(keyName, "TestValue3", "GHI");
			SetValue(keyName + "\\TestKey1", "Value", 123);
			SetValue(keyName + "\\TestKey2", "Value", 456);

			using (var watcher = new RegistryWatcher())
			{
				RegistryChangedEventArgs args = null;
				var argsSet = new ManualResetEventSlim();

				var watch1 = AddWatch(watcher, keyName,
					e => { args = e; argsSet.Set(); },
					recursive: true);

				// Value is deleted
				DeleteValue(keyName, "TestValue2");
				Assert.IsTrue(argsSet.Wait(TIMEOUT));
				Assert.IsNotNull(args);
				Assert.AreEqual(GetKey(keyName), args.Key);
				argsSet.Reset();
				args = null;

				// Value in subkey is deleted
				DeleteValue(keyName + "\\TestKey1", "Value");
				Assert.IsTrue(argsSet.Wait(TIMEOUT));
				Assert.IsNotNull(args);
				Assert.AreEqual(GetKey(keyName), args.Key);
				argsSet.Reset();
				args = null;

				// Subkey is deleted
				DeleteKey(keyName + "\\TestKey1");
				Assert.IsTrue(argsSet.Wait(TIMEOUT));
				Assert.IsNotNull(args);
				Assert.AreEqual(GetKey(keyName), args.Key);
				argsSet.Reset();
				args = null;

				watcher.Remove(watch1);

				// Another value is deleted, but we don't get notified
				DeleteValue(keyName, "TestValue3");
				Assert.IsFalse(argsSet.Wait(TIMEOUT));
				Assert.IsNull(args);

				// Another key is deleted, but we don't get notified
				DeleteKey(keyName + "\\TestKey2");
				Assert.IsFalse(argsSet.Wait(TIMEOUT));
				Assert.IsNull(args);
			}
		}


		class ArgSetter
		{
			readonly RegistryChangedEventArgs[] Args;
			readonly ManualResetEventSlim[] ArgsSet;
			readonly int Index;

			public ArgSetter(RegistryChangedEventArgs[] args, ManualResetEventSlim[] argsSet, int i)
			{
				Args = args;
				ArgsSet = argsSet;
				Index = i;
			}

			public void Raised(RegistryChangedEventArgs e)
			{
				Args[Index] = e;
				ArgsSet[Index].Set();
			}
		}

		[TestMethod, Priority(UnitTestPriority.P0)]
		public void RegistryWatcher100Keys()
		{
			string keyName = "RegistryWatcher100Keys";

			for (int i = 0; i < 100; ++i)
			{
				SetValue(string.Format("{0}\\Key{1}", keyName, i), "Value", "ABC");
			}

			using (var watcher = new RegistryWatcher())
			{
				var args = new RegistryChangedEventArgs[100];
				var argsSet = args.Select(_ => new ManualResetEventSlim()).ToArray();
				var tokens = new object[100];

				for (int i = 0; i < 100; ++i)
				{
					tokens[i] = AddWatch(watcher, string.Format("{0}\\Key{1}", keyName, i),
						new ArgSetter(args, argsSet, i).Raised);
				}

				// Change the first value
				SetValue(keyName + "\\Key0", "Value", "DEF");
				Assert.IsTrue(argsSet[0].Wait(TIMEOUT));
				Assert.IsNotNull(args[0]);
				Assert.AreEqual(GetKey(keyName + "\\Key0"), args[0].Key);
				argsSet[0].Reset();
				args[0] = null;

				// Change the last value
				SetValue(keyName + "\\Key99", "Value", "DEF");
				Assert.IsTrue(argsSet[99].Wait(TIMEOUT));
				Assert.IsNotNull(args[99]);
				Assert.AreEqual(GetKey(keyName + "\\Key99"), args[99].Key);
				argsSet[99].Reset();
				args[99] = null;

				watcher.Remove(tokens[0]);
				watcher.Remove(tokens[99]);

				// Change the first value
				SetValue(keyName + "\\Key0", "Value", "GHI");
				Assert.IsFalse(argsSet[0].Wait(TIMEOUT));
				Assert.IsNull(args[0]);

				// Change the last value
				SetValue(keyName + "\\Key99", "Value", "GHI");
				Assert.IsFalse(argsSet[99].Wait(TIMEOUT));
				Assert.IsNull(args[99]);
			}
		}
	}
}
