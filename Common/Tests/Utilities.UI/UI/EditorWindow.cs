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

using Task = System.Threading.Tasks.Task;

namespace TestUtilities.UI
{
	public class EditorWindow : AutomationWrapper, IEditor
	{
		private readonly string _filename;

		public EditorWindow(VisualStudioApp app, string filename, AutomationElement element)
			: base(element)
		{
			VisualStudioApp = app;
			_filename = filename;
		}

		public VisualStudioApp VisualStudioApp { get; }

		public string Text => TextView.TextSnapshot.GetText();

		public virtual IWpfTextView TextView => GetTextView(_filename);

		public void MoveCaret(SnapshotPoint newPoint)
		{
			Invoke((Action)(() =>
			{
				TextView.Caret.MoveTo(newPoint.TranslateTo(newPoint.Snapshot.TextBuffer.CurrentSnapshot, PointTrackingMode.Positive));
			}));
		}

		public void Select(int line, int column, int length)
		{
			var textLine = TextView.TextViewLines[line - 1];
			Span span;
			if (column - 1 == textLine.Length)
			{
				span = new Span(textLine.End, length);
			}
			else
			{
				span = new Span(textLine.Start + column - 1, length);
			}

			((UIElement)TextView).Dispatcher.Invoke((Action)(() =>
			{
				TextView.Selection.Select(
					new SnapshotSpan(TextView.TextBuffer.CurrentSnapshot, span),
					false
				);
			}));
		}

		/// <summary>
		/// Moves the caret to the 1 based line and column
		/// </summary>
		public void MoveCaret(int line, int column)
		{
			var textLine = TextView.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(line - 1);
			if (column - 1 == textLine.Length)
			{
				MoveCaret(textLine.End);
			}
			else
			{
				MoveCaret(new SnapshotPoint(TextView.TextBuffer.CurrentSnapshot, textLine.Start + column - 1));
			}
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

		public void WaitForTextStart(params string[] text)
		{
			string expected = GetExpectedText(text);

			for (int i = 0; i < 100; i++)
			{
				string curText = Text;

				if (curText.StartsWith(expected, StringComparison.CurrentCulture))
				{
					return;
				}
				Thread.Sleep(100);
			}

			FailWrongText(expected);
		}

		public void WaitForTextEnd(params string[] text)
		{
			string expected = GetExpectedText(text).TrimEnd();

			for (int i = 0; i < 100; i++)
			{
				string curText = Text.TrimEnd();

				if (curText.EndsWith(expected, StringComparison.CurrentCulture))
				{
					return;
				}
				Thread.Sleep(100);
			}

			FailWrongText(expected);
		}

		public static string GetExpectedText(IList<string> text)
		{
			StringBuilder finalString = new StringBuilder();
			for (int i = 0; i < text.Count; i++)
			{
				if (i != 0)
				{
					finalString.Append(Environment.NewLine);
				}

				finalString.Append(text[i]);
			}

			string expected = finalString.ToString();
			return expected;
		}

		private void FailWrongText(string expected)
		{
			StringBuilder msg = new StringBuilder("Did not get text: <");
			AppendRepr(msg, expected);
			msg.Append("> instead got <");
			AppendRepr(msg, Text);
			msg.Append(">");
			Assert.Fail(msg.ToString());
		}

		public static void AppendRepr(StringBuilder msg, string str)
		{
			for (int i = 0; i < str.Length; i++)
			{
				if (str[i] >= 32)
				{
					msg.Append(str[i]);
				}
				else
				{
					switch (str[i])
					{
						case '\n': msg.Append("\\n"); break;

						case '\r': msg.Append("\\r"); break;
						case '\t': msg.Append("\\t"); break;
						default: msg.AppendFormat("\\u00{0:D2}", (int)str[i]); break;
					}
				}
			}
		}

		public void StartLightBulbSessionNoSession()
		{
			ShowLightBulb();
			Thread.Sleep(100);
			Assert.IsNotInstanceOfType(
				IntellisenseSessionStack.TopSession,
				typeof(ILightBulbSession)
			);
		}

		private void ShowLightBulb()
		{
			Task.Run(() =>
			{
				for (int i = 0; i < 40; i++)
				{
					try
					{
						VisualStudioApp.ExecuteCommand("View.ShowSmartTag");
						break;
					}
					catch
					{
						Thread.Sleep(250);
					}
				}
			}).Wait();
		}

		public SessionHolder<LightBulbSessionWrapper> StartLightBulbSession()
		{
			ShowLightBulb();
			var sh = WaitForSession<ILightBulbSession>();
			return sh == null ? null : new SessionHolder<LightBulbSessionWrapper>(new LightBulbSessionWrapper(sh), this);
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
				System.Threading.Thread.Sleep(250);
			}

			if (!(sessionStack.TopSession is T))
			{
				if (assertIfNoSession)
				{
					Console.WriteLine("Buffer text:\r\n{0}", TextView.TextBuffer.CurrentSnapshot.GetText());
					Console.WriteLine("-----");
					AutomationWrapper.DumpVS();
					Assert.Fail("failed to find session " + typeof(T).FullName);
				}
				else
				{
					return null;
				}
			}
			return new SessionHolder<T>((T)sessionStack.TopSession, this);
		}

		public IIntellisenseSessionStack IntellisenseSessionStack
		{
			get
			{
				var compModel = (IComponentModel)VisualStudioApp.ServiceProvider.GetService(typeof(SComponentModel));
				var stackMapService = compModel.GetService<IIntellisenseSessionStackMapService>();

				return stackMapService.GetStackForTextView(TextView);
			}
		}

		public void AssertNoIntellisenseSession()
		{
			Thread.Sleep(500);
			Assert.IsNull(IntellisenseSessionStack.TopSession);
		}

		public IClassifier Classifier
		{
			get
			{

				var compModel = (IComponentModel)VisualStudioApp.ServiceProvider.GetService(typeof(SComponentModel));

				var provider = compModel.GetService<IClassifierAggregatorService>();
				return provider.GetClassifier(TextView.TextBuffer);
			}
		}

		public ITagAggregator<T> GetTaggerAggregator<T>(ITextBuffer buffer) where T : ITag
		{
			var compModel = (IComponentModel)VisualStudioApp.ServiceProvider.GetService(typeof(SComponentModel));

			return compModel.GetService<Microsoft.VisualStudio.Text.Tagging.IBufferTagAggregatorFactoryService>().CreateTagAggregator<T>(buffer);
		}

		internal IWpfTextView GetTextView(string filePath)
		{

			if (VsShellUtilities.IsDocumentOpen(VisualStudioApp.ServiceProvider, filePath, Guid.Empty, out IVsUIHierarchy uiHierarchy, out global::System.UInt32 itemID, out IVsWindowFrame windowFrame))
			{
				var textView = VsShellUtilities.GetTextView(windowFrame);
				IComponentModel compModel = (IComponentModel)VisualStudioApp.ServiceProvider.GetService(typeof(SComponentModel));
				var adapterFact = compModel.GetService<IVsEditorAdaptersFactoryService>();
				return adapterFact.GetWpfTextView(textView);
			}

			return null;
		}

		public void Invoke(Action action)
		{
			ExceptionDispatchInfo excep = null;
			((UIElement)TextView).Dispatcher.Invoke(
				(Action)(() =>
				{
					try
					{
						action();
					}
					catch (Exception e)
					{
						excep = ExceptionDispatchInfo.Capture(e);
					}
				})
			);

			excep?.Throw();
		}

		public void InvokeTask(Func<Task> asyncAction)
		{
			ExceptionDispatchInfo excep = null;
			ThreadHelper.JoinableTaskFactory.Run(async () =>
			{
				await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
				try
				{
					Assert.AreSame(((UIElement)TextView).Dispatcher.Thread, Thread.CurrentThread);
					await asyncAction();
				}
				catch (Exception e)
				{
					excep = ExceptionDispatchInfo.Capture(e);
				}
			});

			excep?.Throw();
		}

		public T Invoke<T>(Func<T> action)
		{
			ExceptionDispatchInfo excep = null;
			T res = default(T);
			((UIElement)TextView).Dispatcher.Invoke(
				(Action)(() =>
				{
					try
					{
						res = action();
					}
					catch (Exception e)
					{
						excep = ExceptionDispatchInfo.Capture(e);
					}
				})
			);

			excep?.Throw();
			return res;
		}

		public IIntellisenseSession TopSession => IntellisenseSessionStack.TopSession;

		public void Type(string text)
		{
			Keyboard.Type(text);
		}
	}
}
