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
	public struct HierarchyItem
	{
		public readonly IVsHierarchy Hierarchy;
		public readonly uint ItemId;

		public HierarchyItem(IVsHierarchy hierarchy, uint itemId)
		{
			Hierarchy = hierarchy;
			ItemId = itemId;
		}

		public bool IsNull => Hierarchy == null;

		public string CanonicalName => GetCanonicalName(ItemId, Hierarchy);

		public bool IsNonMemberItem => (GetProperty((int)__VSHPROPID.VSHPROPID_IsNonMemberItem) as bool?) ?? false;

		public string Caption => GetProperty((int)__VSHPROPID.VSHPROPID_Caption) as string;

		public string EditLabel
		{
			get => GetProperty((int)__VSHPROPID.VSHPROPID_EditLabel) as string;
			set => Hierarchy.SetProperty(ItemId, (int)__VSHPROPID.VSHPROPID_EditLabel, value);
		}

		public string Name => GetProperty((int)__VSHPROPID.VSHPROPID_Name) as string;

		public bool IsHidden => (GetProperty((int)__VSHPROPID.VSHPROPID_IsHiddenItem) as bool?) ?? false;

		public bool IsLinkFile => (GetProperty((int)__VSHPROPID2.VSHPROPID_IsLinkFile) as bool?) ?? false;

		public object ExtensionObject => GetProperty((int)__VSHPROPID.VSHPROPID_ExtObject);

		public IEnumerable<HierarchyItem> Children => Hierarchy.GetChildItems(ItemId);

		private object GetProperty(int propid)
		{
			return Hierarchy.GetPropertyValue(propid, ItemId);
		}

		/// <summary>
		/// Get the canonical name
		/// </summary>
		private static string GetCanonicalName(uint itemId, IVsHierarchy hierarchy)
		{
			Debug.Assert(itemId != VSConstants.VSITEMID_NIL, "ItemId cannot be nill");

			string strRet = string.Empty;
			if (ErrorHandler.Failed(hierarchy.GetCanonicalName(itemId, out strRet)))
			{
				return string.Empty;
			}
			return strRet;
		}
	}
}
