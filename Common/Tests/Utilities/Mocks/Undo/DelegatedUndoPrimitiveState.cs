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

namespace TestUtilities.Mocks
{
	/// <summary>
	/// These are the three states for the DelegatedUndoPrimitives. If Redoing or Undoing, a Redo or undo is in progress. In the 
	/// inactive case, it is illegal to send new operations to the primitive.
	/// </summary>
	internal enum DelegatedUndoPrimitiveState
	{
		/// <summary>
		/// No redo or undo is in progress, and it is illegal to send new operations to the primitive.
		/// </summary>
		Inactive,

		/// <summary>
		/// A redo is in progress. New operations go into the undo list.
		/// </summary>
		Redoing,

		/// <summary>
		/// An undo is in progress. New operations go into the redo list.
		/// </summary>
		Undoing
	}
}
