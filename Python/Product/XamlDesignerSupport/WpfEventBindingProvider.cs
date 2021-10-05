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

namespace Microsoft.PythonTools.XamlDesignerSupport
{
	class WpfEventBindingProvider : EventBindingProvider
	{
		private readonly IXamlDesignerCallback _callback;

		public WpfEventBindingProvider(IXamlDesignerCallback callback)
		{
			_callback = callback;
		}

		public override bool AddEventHandler(EventDescription eventDescription, string objectName, string methodName)
		{
			// we return false here which causes the event handler to always be wired up via XAML instead of via code.
			return false;
		}

		public override bool AllowClassNameForMethodName()
		{
			return true;
		}

		public override void AppendStatements(EventDescription eventDescription, string methodName, string statements, int relativePosition)
		{
			throw new NotImplementedException();
		}

		public override string CodeProviderLanguage
		{
			get { return "Python"; }
		}

		public override bool CreateMethod(EventDescription eventDescription, string methodName, string initialStatements)
		{
			if (!_callback.EnsureDocumentIsOpen())
			{
				return false;
			}

			// build the new method handler
			var insertPoint = _callback.GetInsertionPoint(null);

			if (insertPoint != null)
			{
				var view = _callback.TextView;
				var textBuffer = _callback.Buffer;
				using (var edit = textBuffer.CreateEdit())
				{
					var text = BuildMethod(
						eventDescription,
						methodName,
						new string(' ', insertPoint.Indentation),
						view.Options.IsConvertTabsToSpacesEnabled() ?
							view.Options.GetIndentSize() :
							-1);

					edit.Insert(insertPoint.Location, text);
					edit.Apply();
					return true;
				}
			}

			return false;
		}

		private static string BuildMethod(EventDescription eventDescription, string methodName, string indentation, int tabSize)
		{
			StringBuilder text = new StringBuilder();
			text.AppendLine(indentation);
			text.Append(indentation);
			text.Append("def ");
			text.Append(methodName);
			text.Append('(');
			text.Append("self");
			foreach (var param in eventDescription.Parameters)
			{
				text.Append(", ");
				text.Append(param.Name);
			}
			text.AppendLine("):");
			if (tabSize < 0)
			{
				text.Append(indentation);
				text.Append("\tpass");
			}
			else
			{
				text.Append(indentation);
				text.Append(' ', tabSize);
				text.Append("pass");
			}
			text.AppendLine();

			return text.ToString();
		}

		public override string CreateUniqueMethodName(string objectName, EventDescription eventDescription)
		{
			var name = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}_{1}", objectName, eventDescription.Name);
			int count = 0;

			var methods = _callback.FindMethods(
			   null,
			   null
		   );

			while (methods.Contains(name))
			{
				name = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}_{1}{2}", objectName, eventDescription.Name, ++count);
			}
			return name;
		}

		public override IEnumerable<string> GetCompatibleMethods(EventDescription eventDescription)
		{
			return _callback.FindMethods(null, eventDescription.Parameters.Count() + 1);
		}

		public override IEnumerable<string> GetMethodHandlers(EventDescription eventDescription, string objectName)
		{
			return new string[0];
		}

		public override bool IsExistingMethodName(EventDescription eventDescription, string methodName)
		{
			return _callback.FindMethods(null, null).Contains(methodName);
		}

		private MethodInformation FindMethod(string methodName)
		{
			return _callback.GetMethodInfo(null, methodName);
		}

		public override bool RemoveEventHandler(EventDescription eventDescription, string objectName, string methodName)
		{
			var method = FindMethod(methodName);
			if (method != null && method.IsFound)
			{
				var view = _callback.TextView;
				var textBuffer = _callback.Buffer;

				// appending a method adds 2 extra newlines, we want to remove those if those are still
				// present so that adding a handler and then removing it leaves the buffer unchanged.

				using (var edit = textBuffer.CreateEdit())
				{
					int start = method.Start - 1;

					// eat the newline we insert before the method
					while (start >= 0)
					{
						var curChar = edit.Snapshot[start];
						if (!Char.IsWhiteSpace(curChar))
						{
							break;
						}
						else if (curChar == ' ' || curChar == '\t')
						{
							start--;
							continue;
						}
						else if (curChar == '\n')
						{
							if (start != 0)
							{
								if (edit.Snapshot[start - 1] == '\r')
								{
									start--;
								}
							}
							start--;
							break;
						}
						else if (curChar == '\r')
						{
							start--;
							break;
						}

						start--;
					}


					// eat the newline we insert at the end of the method
					int end = method.End;
					while (end < edit.Snapshot.Length)
					{
						if (edit.Snapshot[end] == '\n')
						{
							end++;
							break;
						}
						else if (edit.Snapshot[end] == '\r')
						{
							if (end < edit.Snapshot.Length - 1 && edit.Snapshot[end + 1] == '\n')
							{
								end += 2;
							}
							else
							{
								end++;
							}
							break;
						}
						else if (edit.Snapshot[end] == ' ' || edit.Snapshot[end] == '\t')
						{
							end++;
							continue;
						}
						else
						{
							break;
						}
					}

					// delete the method and the extra whitespace that we just calculated.
					edit.Delete(Span.FromBounds(start + 1, end));
					edit.Apply();
				}

				return true;
			}
			return false;
		}

		public override bool RemoveHandlesForName(string elementName)
		{
			throw new NotImplementedException();
		}

		public override bool RemoveMethod(EventDescription eventDescription, string methodName)
		{
			throw new NotImplementedException();
		}

		public override void SetClassName(string className)
		{
		}

		public override bool ShowMethod(EventDescription eventDescription, string methodName)
		{
			var method = FindMethod(methodName);
			if (method != null && method.IsFound)
			{
				var view = _callback.TextView;
				view.Caret.MoveTo(new SnapshotPoint(view.TextSnapshot, method.Start));
				view.Caret.EnsureVisible();
				return true;
			}

			return false;
		}

		public override void ValidateMethodName(EventDescription eventDescription, string methodName)
		{
		}
	}
}
