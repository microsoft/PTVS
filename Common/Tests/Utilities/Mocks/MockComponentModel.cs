// Visual Studio Shared Project
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

namespace TestUtilities.Mocks
{
	public class MockComponentModel : ExportProvider, IComponentModel
	{
		public readonly Dictionary<Type, List<Lazy<object>>> Extensions = new Dictionary<Type, List<Lazy<object>>>();

		public void AddExtension<T>(Func<T> creator) where T : class
		{
			AddExtension(typeof(T), creator);
		}

		public void AddExtension<T>(Type key, Func<T> creator) where T : class
		{
			if (!Extensions.TryGetValue(key, out List<Lazy<global::System.Object>> extensions))
			{
				Extensions[key] = extensions = new List<Lazy<object>>();
			}
			extensions.Add(new Lazy<object>(creator));
		}

		public T GetService<T>() where T : class
		{
			if (Extensions.TryGetValue(typeof(T), out List<Lazy<global::System.Object>> extensions))
			{
				Debug.Assert(extensions.Count == 1, "Multiple extensions were registered");
				return (T)extensions[0].Value;
			}
			Console.WriteLine("Unregistered component model service " + typeof(T).FullName);
			return null;
		}

		public object GetService(Type t)
		{
			if (Extensions.TryGetValue(t, out List<Lazy<global::System.Object>> extensions))
			{
				Debug.Assert(extensions.Count == 1, "Multiple extensions were registered");
				return extensions[0].Value;
			}
			Console.WriteLine("Unregistered component model service " + t.FullName);
			return null;
		}

		public ComposablePartCatalog DefaultCatalog
		{
			get { throw new NotImplementedException(); }
		}

		public ICompositionService DefaultCompositionService
		{
			get { throw new NotImplementedException(); }
		}

		public System.ComponentModel.Composition.Hosting.ExportProvider DefaultExportProvider
		{
			get { return this; }
		}

		public System.ComponentModel.Composition.Primitives.ComposablePartCatalog GetCatalog(string catalogName)
		{
			throw new NotImplementedException();
		}

		public IEnumerable<T> GetExtensions<T>() where T : class
		{
			if (Extensions.TryGetValue(typeof(T), out List<Lazy<global::System.Object>> res))
			{
				foreach (var t in res)
				{
					yield return (T)t.Value;
				}
			}
		}

		protected override IEnumerable<Export> GetExportsCore(ImportDefinition definition, AtomicComposition atomicComposition)
		{
			foreach (var keyValue in Extensions)
			{
				if (keyValue.Key.FullName == definition.ContractName)
				{
					foreach (var value in keyValue.Value)
					{
						yield return new Export(
							new ExportDefinition(keyValue.Key.FullName, new Dictionary<string, object>()),
							() => value.Value
						);
					}
				}
			}
		}
	}
}
