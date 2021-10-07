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
	internal class MockVsMonitorSelection : IVsMonitorSelection, IVsMonitorSelection2
	{
		private readonly MockVs _vs;
		private uint _lastSelectionEventsCookie;
		private readonly Dictionary<uint, IVsSelectionEvents> _listeners = new Dictionary<uint, IVsSelectionEvents>();

		private uint _lastCmdUIContextCookie = 0;
		private readonly Dictionary<uint, Guid> _cmdUIContexts = new Dictionary<uint, Guid>();
		private readonly Dictionary<Guid, uint> _cmdUIContextsByGuid = new Dictionary<Guid, uint>();
		private readonly List<bool> _cmdUIContextsActive = new List<bool> { false };
		internal readonly MockVsTrackSelectionEx _emptyCtx;

		private const string _surfaceSelectionContext = "{64db9e55-5614-44b3-93c9-e617b95eeb5f}";

		private readonly List<SelectionContext> _selectionContexts = new List<SelectionContext>() { new SelectionContext(Guid.Parse(_surfaceSelectionContext)) };
		private IVsHierarchy _hier;
		private uint _itemid;
		private IVsMultiItemSelect _mis;
		private ISelectionContainer _container;


		public MockVsMonitorSelection(MockVs vs)
		{
			_vs = vs;
			_emptyCtx = new MockVsTrackSelectionEx(this);
		}

		public int AdviseSelectionEvents(
			IVsSelectionEvents pSink,
			[ComAliasName("Microsoft.VisualStudio.Shell.Interop.VSCOOKIE")] out uint pdwCookie)
		{
			_lastSelectionEventsCookie++;
			pdwCookie = _lastSelectionEventsCookie;
			_listeners.Add(pdwCookie, pSink);
			return VSConstants.S_OK;
		}

		internal void NotifyElementChanged(MockVsTrackSelectionEx mockVsTrackSelectionEx, uint elementid)
		{
			throw new NotImplementedException();
		}

		internal void NotifySelectionContextChanged(MockVsTrackSelectionEx mockVsTrackSelectionEx)
		{
			var oldHier = _hier;
			var oldItem = _itemid;
			var oldMis = _mis;
			var oldContainer = _container;

			MockVsTrackSelectionEx sel = mockVsTrackSelectionEx ?? _emptyCtx;
			if (sel != null)
			{
				sel.GetCurrentSelection(
					out _hier,
					out _itemid,
					out _mis,
					out _container
				);
			}

			if (oldHier != _hier ||
				oldItem != _itemid ||
				oldMis != _mis ||
				oldContainer != _container)
			{
				// something changed, tell our listeners...
				foreach (var listener in _listeners.Values)
				{
					listener.OnSelectionChanged(
						oldHier,
						oldItem,
						oldMis,
						oldContainer,
						_hier,
						_itemid,
						_mis,
						_container
					);
				}
			}
		}

		public int UnadviseSelectionEvents(
			[ComAliasName("Microsoft.VisualStudio.Shell.Interop.VSCOOKIE")] uint dwCookie)
		{
			_listeners.Remove(dwCookie);
			return VSConstants.S_OK;
		}

		public int GetCmdUIContextCookie(
			[ComAliasName("Microsoft.VisualStudio.OLE.Interop.REFGUID")] ref Guid rguidCmdUI,
			[ComAliasName("Microsoft.VisualStudio.Shell.Interop.VSCOOKIE")] out uint pdwCmdUICookie)
		{
			if (_cmdUIContextsByGuid.TryGetValue(rguidCmdUI, out pdwCmdUICookie))
			{
				return VSConstants.S_OK;
			}

			_lastCmdUIContextCookie++;
			pdwCmdUICookie = _lastCmdUIContextCookie;
			_cmdUIContexts.Add(pdwCmdUICookie, rguidCmdUI);
			_cmdUIContextsByGuid.Add(rguidCmdUI, pdwCmdUICookie);
			_cmdUIContextsActive.Add(false);
			return VSConstants.S_OK;
		}

		public int IsCmdUIContextActive(
			[ComAliasName("Microsoft.VisualStudio.Shell.Interop.VSCOOKIE")] uint dwCmdUICookie,
			[ComAliasName("Microsoft.VisualStudio.OLE.Interop.BOOL")] out int pfActive)
		{
			pfActive = _cmdUIContextsActive[(int)dwCmdUICookie] ? 1 : 0;
			return VSConstants.S_OK;
		}

		public int SetCmdUIContext(
			[ComAliasName("Microsoft.VisualStudio.Shell.Interop.VSCOOKIE")] uint dwCmdUICookie,
			[ComAliasName("Microsoft.VisualStudio.OLE.Interop.BOOL")] int fActive)
		{
			_cmdUIContextsActive[(int)dwCmdUICookie] = fActive != 0;
			foreach (var kvp in _listeners)
			{
				kvp.Value.OnCmdUIContextChanged(dwCmdUICookie, fActive);
			}

			return VSConstants.S_OK;
		}

		public int GetCurrentSelection(out IntPtr ppHier, out uint pitemid, out IVsMultiItemSelect ppMIS, out IntPtr ppSC)
		{
			ppMIS = _mis;
			ppSC = IntPtr.Zero;

			if (_hier != null)
			{
				ppHier = Marshal.GetIUnknownForObject(_hier);
				if (_mis == null)
				{
					pitemid = _itemid;
				}
				else
				{
					pitemid = VSConstants.VSITEMID_SELECTION;
				}
			}
			else
			{
				ppHier = IntPtr.Zero;
				pitemid = 0;
			}

			return VSConstants.S_OK;
		}

		public int GetCurrentElementValue([ComAliasName("Microsoft.VisualStudio.Shell.Interop.VSSELELEMID")] uint elementid, out object pvarValue)
		{
			throw new NotImplementedException();
		}

		public int GetElementID(ref Guid rguidElement, out uint pElementId)
		{
			for (int i = 0; i < _selectionContexts.Count; i++)
			{
				if (_selectionContexts[i].Id == rguidElement)
				{
					pElementId = (uint)i;
					return VSConstants.S_OK;
				}
			}
			pElementId = 0;
			return VSConstants.E_INVALIDARG;
		}

		public int GetEmptySelectionContext(out IVsTrackSelectionEx ppEmptySelCtxt)
		{
			ppEmptySelCtxt = _emptyCtx;
			return VSConstants.S_OK;
		}

		private class SelectionContext
		{
			public readonly Guid Id;

			public SelectionContext(Guid id)
			{
				Id = id;
			}
		}
	}
}
