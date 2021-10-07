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

namespace TestUtilities.Python
{
	public class MockPythonInterpreter : IPythonInterpreter2
	{
		public readonly Dictionary<string, IPythonModule> _modules;
		public readonly HashSet<string> _moduleNames;
		public bool IsDatabaseInvalid;

		public MockPythonInterpreter(IPythonInterpreterFactory factory)
		{
			_modules = new Dictionary<string, IPythonModule>();
			_moduleNames = new HashSet<string>(StringComparer.Ordinal);
		}

		public void Dispose() { }

		public void AddModule(string name, IPythonModule module)
		{
			_modules[name] = module;
			ModuleNamesChanged?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>
		/// Removes a module. If <c>retainName</c> is true, keeps returning
		/// the module name from <see cref="GetModuleNames"/>.
		/// </summary>
		public void RemoveModule(string name, bool retainName = false)
		{
			if (retainName)
			{
				_moduleNames.Add(name);
			}
			if (_modules.Remove(name))
			{
				ModuleNamesChanged?.Invoke(this, EventArgs.Empty);
			}
		}

		public void Initialize(PythonAnalyzer state) { }

		public IPythonType GetBuiltinType(BuiltinTypeId id)
		{
			throw new KeyNotFoundException();
		}

		public IList<string> GetModuleNames()
		{
			return _modules.Keys.Concat(_moduleNames).ToArray();
		}

		public event EventHandler ModuleNamesChanged;

		public Task<IPythonModule> ImportModuleAsync(string name, CancellationToken token)
		{
			return Task.FromResult(ImportModule(name));
		}

		public IPythonModule ImportModule(string name)
		{
			_modules.TryGetValue(name, out IPythonModule res);
			return res;
		}

		public IModuleContext CreateModuleContext()
		{
			return null;
		}

		public Task AddReferenceAsync(ProjectReference reference, CancellationToken cancellationToken = default(CancellationToken))
		{
			throw new NotImplementedException();
		}

		public void RemoveReference(ProjectReference reference)
		{
			throw new NotImplementedException();
		}
	}
}
