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

namespace Microsoft.VisualStudioTools.MockVsTests
{
	internal class MockDTEProperty : Property
	{
		private object _value;

		public MockDTEProperty(object value)
		{
			_value = value;
		}

		public object Application
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public Properties Collection
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public DTE DTE
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public string Name
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public short NumIndices
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public object Object
		{
			get
			{
				throw new NotImplementedException();
			}

			set
			{
				throw new NotImplementedException();
			}
		}

		public Properties Parent
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public object Value
		{
			get
			{
				return _value;
			}

			set
			{
				_value = value;
			}
		}

		public object get_IndexedValue(object Index1, object Index2, object Index3, object Index4)
		{
			throw new NotImplementedException();
		}

		public void let_Value(object lppvReturn)
		{
			throw new NotImplementedException();
		}

		public void set_IndexedValue(object Index1, object Index2, object Index3, object Index4, object Val)
		{
			throw new NotImplementedException();
		}
	}
}