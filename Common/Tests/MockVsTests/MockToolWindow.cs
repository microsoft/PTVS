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
	class MockToolWindow : IVsWindowFrame
	{
		private readonly object _docView;

		public MockToolWindow(object docView)
		{
			_docView = docView;
		}

		public int CloseFrame(uint grfSaveOptions)
		{
			throw new NotImplementedException();
		}

		public int GetFramePos(VSSETFRAMEPOS[] pdwSFP, out Guid pguidRelativeTo, out int px, out int py, out int pcx, out int pcy)
		{
			throw new NotImplementedException();
		}

		public int GetGuidProperty(int propid, out Guid pguid)
		{
			throw new NotImplementedException();
		}

		public int GetProperty(int propid, out object pvar)
		{
			if (propid == (int)__VSFPROPID.VSFPROPID_DocView)
			{
				pvar = _docView;
				return VSConstants.S_OK;
			}

			throw new NotImplementedException();
		}

		public int Hide()
		{
			throw new NotImplementedException();
		}

		public int IsOnScreen(out int pfOnScreen)
		{
			throw new NotImplementedException();
		}

		public int IsVisible()
		{
			throw new NotImplementedException();
		}

		public int QueryViewInterface(ref Guid riid, out IntPtr ppv)
		{
			throw new NotImplementedException();
		}

		public int SetFramePos(VSSETFRAMEPOS dwSFP, ref Guid rguidRelativeTo, int x, int y, int cx, int cy)
		{
			throw new NotImplementedException();
		}

		public int SetGuidProperty(int propid, ref Guid rguid)
		{
			throw new NotImplementedException();
		}

		public int SetProperty(int propid, object var)
		{
			throw new NotImplementedException();
		}

		public int Show()
		{
			throw new NotImplementedException();
		}

		public int ShowNoActivate()
		{
			throw new NotImplementedException();
		}
	}
}
