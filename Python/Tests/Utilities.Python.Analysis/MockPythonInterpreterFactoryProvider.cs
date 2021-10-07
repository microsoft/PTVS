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
	public class MockPythonInterpreterFactoryProvider : IPythonInterpreterFactoryProvider
	{
		private readonly string _name;
		private readonly List<IPythonInterpreterFactory> _factories;

		public MockPythonInterpreterFactoryProvider(string name, params IPythonInterpreterFactory[] factories)
		{
			_name = name;
			_factories = factories.ToList();
		}

		public override string ToString()
		{
			return string.Format("{0}: {1}", GetType().Name, _name);
		}

		public void AddFactory(IPythonInterpreterFactory factory)
		{
			lock (_factories)
			{
				_factories.Add(factory);
			}
			var evt = InterpreterFactoriesChanged;
			if (evt != null)
			{
				evt(this, EventArgs.Empty);
			}
		}

		public bool RemoveFactory(IPythonInterpreterFactory factory)
		{
			bool changed;
			lock (_factories)
			{
				changed = _factories.Remove(factory);
			}
			if (changed)
			{
				var evt = InterpreterFactoriesChanged;
				if (evt != null)
				{
					evt(this, EventArgs.Empty);
				}
				return true;
			}
			return false;
		}

		public void RemoveAllFactories()
		{
			bool changed = false;
			lock (_factories)
			{
				if (_factories.Any())
				{
					_factories.Clear();
					changed = true;
				}
			}
			if (changed)
			{
				var evt = InterpreterFactoriesChanged;
				if (evt != null)
				{
					evt(this, EventArgs.Empty);
				}
			}
		}

		public IEnumerable<IPythonInterpreterFactory> GetInterpreterFactories()
		{
			// Deliberately not locked so we simulate testing against 3rd-party
			// implementations that don't protect this function call.
			return _factories.Where(x => x != null);
		}

		public IEnumerable<InterpreterConfiguration> GetInterpreterConfigurations()
		{
			return GetInterpreterFactories().Select(x => x.Configuration);
		}

		public IPythonInterpreterFactory GetInterpreterFactory(string id)
		{
			return GetInterpreterFactories()
				.Where(x => x.Configuration.Id == id)
				.FirstOrDefault();
		}

		public object GetProperty(string id, string propName)
		{
			return (GetInterpreterFactory(id) as MockPythonInterpreterFactory)?.GetProperty(propName);
		}

		public event EventHandler InterpreterFactoriesChanged;
	}
}
