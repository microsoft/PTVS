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

namespace Microsoft.IronPythonTools.Interpreter
{
	/// <summary>
	/// Represents an object in a remote domain whos identity has been captured.
	/// 
	/// Comparison of the handles compares object identity.  It is the responsibility
	/// of the consumer of the object identity handle to make sure that they are comparing
	/// only handles that came from the same source, otherwise the identities could bleed
	/// across sources and compare incorrectly.
	/// </summary>
	[Serializable]
	struct ObjectIdentityHandle : IEquatable<ObjectIdentityHandle>
	{
		private readonly int _identity;

		public ObjectIdentityHandle(int identity)
		{
			_identity = identity;
		}

		public bool IsNull
		{
			get
			{
				return _identity == 0;
			}
		}

		public int Id
		{
			get
			{
				return _identity;
			}
		}

		public override bool Equals(object obj)
		{
			if (obj is ObjectIdentityHandle)
			{
				return this.Equals((ObjectIdentityHandle)obj);
			}
			return false;
		}

		public override int GetHashCode()
		{
			return _identity;
		}

		#region IEquatable<ObjectIdentityHandle> Members

		public bool Equals(ObjectIdentityHandle other)
		{
			return other._identity == _identity;
		}

		#endregion
	}
}
