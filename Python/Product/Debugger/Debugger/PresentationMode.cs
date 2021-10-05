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

namespace Microsoft.PythonTools.Debugger
{

	/// <summary>
	/// How variables can be presented through the IDE.
	/// 
	/// Group means variables of this type are grouped under their own collapsable element.
	/// Hide means variables of this type are not shown.
	/// Inline means variables of this type are expanded into a single flat level.
	/// </summary>
	[JsonConverter(typeof(StringEnumConverter))]
	public enum PresentationMode
	{
		[EnumMember(Value = "group")]
		Group,

		[EnumMember(Value = "hide")]
		Hide,

		[EnumMember(Value = "inline")]
		Inline,
	}
}
