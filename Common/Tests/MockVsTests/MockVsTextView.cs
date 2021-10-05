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

using IServiceProvider = System.IServiceProvider;

namespace Microsoft.VisualStudioTools.MockVsTests
{
	public class MockVsTextView : IVsTextView, IFocusable, IEditor, IOleCommandTarget, IDisposable
	{
		private readonly MockTextView _view;
		private readonly IEditorOperations _editorOps;
		private readonly IServiceProvider _serviceProvider;
		private readonly MockVs _vs;
		private IOleCommandTarget _commandTarget;
		private IClassifier _classifier;
		private bool _isDisposed;

		public MockVsTextView(IServiceProvider serviceProvier, MockVs vs, MockTextView view)
		{
			_view = view;
			_serviceProvider = serviceProvier;
			_vs = vs;
			var compModel = (IComponentModel)_serviceProvider.GetService(typeof(SComponentModel));
			var editorOpsFact = compModel.GetService<IEditorOperationsFactoryService>();
			_editorOps = editorOpsFact.GetEditorOperations(_view);
			_commandTarget = new EditorCommandTarget(this);
		}

		public MockTextView View
		{
			get
			{
				return _view;
			}
		}

		public void Dispose()
		{
			if (!_isDisposed)
			{
				_isDisposed = true;
				Close();
			}
		}

		public IIntellisenseSessionStack IntellisenseSessionStack
		{
			get
			{
				var compModel = (IComponentModel)_serviceProvider.GetService(typeof(SComponentModel));
				var stackMap = compModel.GetService<IIntellisenseSessionStackMapService>();
				return stackMap.GetStackForTextView(View);
			}
		}

		public IIntellisenseSession TopSession
		{
			get
			{
				return IntellisenseSessionStack.TopSession;
			}
		}

		public string Text
		{
			get
			{
				return View.TextBuffer.CurrentSnapshot.GetText();
			}
		}

		int IVsTextView.AddCommandFilter(IOleCommandTarget pNewCmdTarg, out IOleCommandTarget ppNextCmdTarg)
		{
			ppNextCmdTarg = _commandTarget;
			_commandTarget = pNewCmdTarg;
			return VSConstants.S_OK;
		}

		int IVsTextView.CenterColumns(int iLine, int iLeftCol, int iColCount)
		{
			throw new NotImplementedException();
		}

		int IVsTextView.CenterLines(int iTopLine, int iCount)
		{
			throw new NotImplementedException();
		}

		int IVsTextView.ClearSelection(int fMoveToAnchor)
		{
			throw new NotImplementedException();
		}

		public void Close()
		{
			var rdt = (IVsRunningDocumentTable)_serviceProvider.GetService(typeof(SVsRunningDocumentTable));
			rdt.UnlockDocument(0, ((MockVsTextLines)GetBuffer())._docCookie);
			var disposable = _classifier as IDisposable;
			if (disposable != null)
			{
				disposable.Dispose();
			}
			_view.Close();
		}

		int IVsTextView.CloseView()
		{
			Close();
			return VSConstants.S_OK;
		}

		int IVsTextView.EnsureSpanVisible(TextSpan span)
		{
			throw new NotImplementedException();
		}

		int IVsTextView.GetBuffer(out IVsTextLines ppBuffer)
		{
			ppBuffer = GetBuffer();
			return VSConstants.S_OK;
		}

		private IVsTextLines GetBuffer()
		{
			IVsTextLines ppBuffer;
			var compModel = (IComponentModel)_serviceProvider.GetService(typeof(SComponentModel));
			ppBuffer = (IVsTextLines)compModel.GetService<IVsEditorAdaptersFactoryService>().GetBufferAdapter(_view.TextBuffer);
			return ppBuffer;
		}

		int IVsTextView.GetCaretPos(out int piLine, out int piColumn)
		{
			throw new NotImplementedException();
		}

		int IVsTextView.GetLineAndColumn(int iPos, out int piLine, out int piIndex)
		{
			throw new NotImplementedException();
		}

		int IVsTextView.GetLineHeight(out int piLineHeight)
		{
			throw new NotImplementedException();
		}

		int IVsTextView.GetNearestPosition(int iLine, int iCol, out int piPos, out int piVirtualSpaces)
		{
			throw new NotImplementedException();
		}

		int IVsTextView.GetPointOfLineColumn(int iLine, int iCol, VisualStudio.OLE.Interop.POINT[] ppt)
		{
			throw new NotImplementedException();
		}

		int IVsTextView.GetScrollInfo(int iBar, out int piMinUnit, out int piMaxUnit, out int piVisibleUnits, out int piFirstVisibleUnit)
		{
			throw new NotImplementedException();
		}

		int IVsTextView.GetSelectedText(out string pbstrText)
		{
			throw new NotImplementedException();
		}

		int IVsTextView.GetSelection(out int piAnchorLine, out int piAnchorCol, out int piEndLine, out int piEndCol)
		{
			throw new NotImplementedException();
		}

		int IVsTextView.GetSelectionDataObject(out VisualStudio.OLE.Interop.IDataObject ppIDataObject)
		{
			throw new NotImplementedException();
		}

		TextSelMode IVsTextView.GetSelectionMode()
		{
			throw new NotImplementedException();
		}

		int IVsTextView.GetSelectionSpan(TextSpan[] pSpan)
		{
			throw new NotImplementedException();
		}

		int IVsTextView.GetTextStream(int iTopLine, int iTopCol, int iBottomLine, int iBottomCol, out string pbstrText)
		{
			throw new NotImplementedException();
		}

		IntPtr IVsTextView.GetWindowHandle()
		{
			throw new NotImplementedException();
		}

		int IVsTextView.GetWordExtent(int iLine, int iCol, uint dwFlags, TextSpan[] pSpan)
		{
			throw new NotImplementedException();
		}

		int IVsTextView.HighlightMatchingBrace(uint dwFlags, uint cSpans, TextSpan[] rgBaseSpans)
		{
			throw new NotImplementedException();
		}

		int IVsTextView.Initialize(IVsTextLines pBuffer, IntPtr hwndParent, uint InitFlags, INITVIEW[] pInitView)
		{
			throw new NotImplementedException();
		}

		int IVsTextView.PositionCaretForEditing(int iLine, int cIndentLevels)
		{
			throw new NotImplementedException();
		}

		int IVsTextView.RemoveCommandFilter(VisualStudio.OLE.Interop.IOleCommandTarget pCmdTarg)
		{
			throw new NotImplementedException();
		}

		int IVsTextView.ReplaceTextOnLine(int iLine, int iStartCol, int iCharsToReplace, string pszNewText, int iNewLen)
		{
			throw new NotImplementedException();
		}

		int IVsTextView.RestrictViewRange(int iMinLine, int iMaxLine, IVsViewRangeClient pClient)
		{
			throw new NotImplementedException();
		}

		int IVsTextView.SendExplicitFocus()
		{
			throw new NotImplementedException();
		}

		int IVsTextView.SetBuffer(IVsTextLines pBuffer)
		{
			throw new NotImplementedException();
		}

		int IVsTextView.SetCaretPos(int iLine, int iColumn)
		{
			throw new NotImplementedException();
		}

		int IVsTextView.SetScrollPosition(int iBar, int iFirstVisibleUnit)
		{
			throw new NotImplementedException();
		}

		int IVsTextView.SetSelection(int iAnchorLine, int iAnchorCol, int iEndLine, int iEndCol)
		{
			throw new NotImplementedException();
		}

		int IVsTextView.SetSelectionMode(TextSelMode iSelMode)
		{
			throw new NotImplementedException();
		}

		int IVsTextView.SetTopLine(int iBaseLine)
		{
			throw new NotImplementedException();
		}

		int IVsTextView.UpdateCompletionStatus(IVsCompletionSet pCompSet, uint dwFlags)
		{
			throw new NotImplementedException();
		}

		int IVsTextView.UpdateTipWindow(IVsTipWindow pTipWindow, uint dwFlags)
		{
			throw new NotImplementedException();
		}

		int IVsTextView.UpdateViewFrameCaption()
		{
			throw new NotImplementedException();
		}

		class EditorCommandTarget : IOleCommandTarget
		{
			private readonly MockVsTextView _view;

			public EditorCommandTarget(MockVsTextView view)
			{
				_view = view;
			}

			public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
			{
				if (pguidCmdGroup == VSConstants.VSStd2K)
				{
					switch ((VSConstants.VSStd2KCmdID)nCmdID)
					{
						case VSConstants.VSStd2KCmdID.TYPECHAR:
							var ch = (char)(ushort)Marshal.GetObjectForNativeVariant(pvaIn);
							_view._editorOps.InsertText(ch.ToString());
							return VSConstants.S_OK;
						case VSConstants.VSStd2KCmdID.RETURN:
							_view._editorOps.InsertNewLine();
							return VSConstants.S_OK;
					}
				}
				return NativeMethods.OLECMDERR_E_NOTSUPPORTED;
			}

			public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
			{
				throw new NotImplementedException();
			}
		}

		public void GetFocus()
		{
			_view.OnGotAggregateFocus();
		}

		public void LostFocus()
		{
			_view.OnLostAggregateFocus();
		}

		public void Type(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return;
			}

			using (var mre = new ManualResetEventSlim())
			{
				EventHandler<TextContentChangedEventArgs> evt = (s, e) => mre.SetIfNotDisposed();
				_view.TextBuffer.ChangedLowPriority += evt;
				_commandTarget.Type(text);
				Assert.IsTrue(mre.Wait(1000), "No change event seen");
				_view.TextBuffer.ChangedLowPriority -= evt;
			}
		}

		int IOleCommandTarget.Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
		{
			return _commandTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
		}

		int IOleCommandTarget.QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
		{
			return _commandTarget.QueryStatus(pguidCmdGroup, cCmds, prgCmds, pCmdText);
		}

		public IWpfTextView TextView
		{
			get
			{
				return _view;
			}
		}
		public void Select(int line, int column, int length)
		{
			throw new NotImplementedException();
		}

		public void WaitForText(string text)
		{
			for (int i = 0; i < 100; i++)
			{
				if (Text != text)
				{
					System.Threading.Thread.Sleep(100);
				}
				else
				{
					break;
				}
			}

			Assert.AreEqual(text, Text);
		}

		public void MoveCaret(int line, int column)
		{
			var textLine = _view.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(line - 1);
			if (column - 1 == textLine.Length)
			{
				MoveCaret(textLine.End);
			}
			else
			{
				MoveCaret(new SnapshotPoint(_view.TextBuffer.CurrentSnapshot, textLine.Start + column - 1));
			}
		}

		public CaretPosition MoveCaret(SnapshotPoint newPoint)
		{
			return _vs.Invoke(() => _view.Caret.MoveTo(newPoint.TranslateTo(newPoint.Snapshot.TextBuffer.CurrentSnapshot, PointTrackingMode.Positive)));
		}

		public void SetFocus()
		{
		}

		public void Invoke(Action action)
		{
			_vs.Invoke(action);
		}

		public IClassifier Classifier
		{
			get
			{
				if (_classifier == null)
				{
					var compModel = (IComponentModel)_serviceProvider.GetService(typeof(SComponentModel));

					var provider = compModel.GetService<IClassifierAggregatorService>();
					_classifier = provider.GetClassifier(TextView.TextBuffer);
				}
				return _classifier;
			}
		}

		public SessionHolder<T> WaitForSession<T>() where T : IIntellisenseSession
		{
			return WaitForSession<T>(true);
		}

		public SessionHolder<T> WaitForSession<T>(bool assertIfNoSession) where T : IIntellisenseSession
		{
			var sessionStack = IntellisenseSessionStack;
			for (int i = 0; i < 40; i++)
			{
				if (sessionStack.TopSession is T)
				{
					break;
				}
				Thread.Sleep(Debugger.IsAttached ? 1000 : 25);
			}

			if (!(sessionStack.TopSession is T))
			{
				if (assertIfNoSession)
				{
					Console.WriteLine("Buffer text:\r\n{0}", Text);
					Console.WriteLine("-----");
					Assert.Fail("failed to find session " + typeof(T).FullName);
				}
				else
				{
					return null;
				}
			}
			return new SessionHolder<T>((T)sessionStack.TopSession, this);
		}

		public void AssertNoIntellisenseSession()
		{
			Thread.Sleep(500);
			var session = IntellisenseSessionStack.TopSession;
			if (session != null)
			{
				Assert.Fail("Expected no Intellisense session, but got " + session.GetType().ToString());
			}
		}
	}
}
