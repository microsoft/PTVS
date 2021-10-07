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
	[Export(typeof(IClassificationTypeRegistryService))]
	public class MockClassificationTypeRegistryService : IClassificationTypeRegistryService
	{
		private static Dictionary<string, MockClassificationType> _types = new Dictionary<string, MockClassificationType>();

		public MockClassificationTypeRegistryService()
		{
			foreach (FieldInfo fi in typeof(PredefinedClassificationTypeNames).GetFields())
			{
				string name = (string)fi.GetValue(null);
				_types[name] = new MockClassificationType(name, new IClassificationType[0]);
			}
		}

		[ImportingConstructor]
		public MockClassificationTypeRegistryService([ImportMany] IEnumerable<Lazy<ClassificationTypeDefinition, IClassificationTypeDefinitionMetadata>> classTypeDefs)
			: this()
		{
			foreach (var def in classTypeDefs)
			{
				string name = def.Metadata.Name;
				MockClassificationType type = GetClasificationType(name);
				foreach (var baseType in def.Metadata.BaseDefinition ?? Enumerable.Empty<string>())
				{
					type.AddBaseType(GetClasificationType(baseType));
				}
			}
		}

		private static MockClassificationType GetClasificationType(string name)
		{
			if (!_types.TryGetValue(name, out MockClassificationType type))
			{
				_types[name] = type = new MockClassificationType(name, new IClassificationType[0]);
			}
			return type;
		}

		public IClassificationType CreateClassificationType(string type, IEnumerable<IClassificationType> baseTypes)
		{
			return _types[type] = new MockClassificationType(type, baseTypes.ToArray());
		}

		public IClassificationType CreateTransientClassificationType(params IClassificationType[] baseTypes)
		{
			return new MockClassificationType(String.Empty, baseTypes);
		}

		public IClassificationType CreateTransientClassificationType(IEnumerable<IClassificationType> baseTypes)
		{
			return new MockClassificationType(String.Empty, baseTypes.ToArray());
		}

		public IClassificationType GetClassificationType(string type)
		{
			return _types.TryGetValue(type, out MockClassificationType result) ? result : null;
		}
	}
}
