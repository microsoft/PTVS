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
	public class MockClassificationType : IClassificationType
	{
		private readonly string _name;
		private readonly List<IClassificationType> _bases;

		public MockClassificationType(string name, IClassificationType[] bases)
		{
			_name = name;
			_bases = new List<IClassificationType>(bases);
		}

		public IEnumerable<IClassificationType> BaseTypes => _bases;

		public string Classification => _name;

		public bool IsOfType(string type)
		{
			if (type == _name)
			{
				return true;
			}

			foreach (var baseType in BaseTypes)
			{
				if (baseType.IsOfType(type))
				{
					return true;
				}
			}
			return false;
		}

		public void AddBaseType(MockClassificationType mockClassificationType)
		{
			_bases.Add(mockClassificationType);
		}
	}
}
