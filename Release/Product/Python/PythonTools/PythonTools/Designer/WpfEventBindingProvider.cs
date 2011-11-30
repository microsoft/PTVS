/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

#if !DEV11

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using Microsoft.Windows.Design.Host;

namespace Microsoft.PythonTools.Designer {
    class WpfEventBindingProvider : EventBindingProvider {
        private Project.PythonFileNode _pythonFileNode;

        public WpfEventBindingProvider(Project.PythonFileNode pythonFileNode) {
            _pythonFileNode = pythonFileNode;
        }

        public override bool AddEventHandler(EventDescription eventDescription, string objectName, string methodName) {
            // we return false here which causes the event handler to always be wired up via XAML instead of via code.
            return false;
        }

        public override bool AllowClassNameForMethodName() {
            return true;
        }

        public override void AppendStatements(EventDescription eventDescription, string methodName, string statements, int relativePosition) {
            throw new NotImplementedException();
        }

        public override string CodeProviderLanguage {
            get { return "Python"; }
        }

        public override bool CreateMethod(EventDescription eventDescription, string methodName, string initialStatements) {
            // build the new method handler
            var view = _pythonFileNode.GetTextView();
            var textBuffer = _pythonFileNode.GetTextBuffer();
            PythonAst ast;
            var classDef = GetClassForEvents(out ast);
            if (classDef != null) {
                int end = classDef.Body.EndIndex;
                
                // insert after the newline at the end of the last statement of the class def
                if (textBuffer.CurrentSnapshot[end] == '\r') {
                    if (end + 1 < textBuffer.CurrentSnapshot.Length &&
                        textBuffer.CurrentSnapshot[end + 1] == '\n') {
                        end += 2;
                    } else {
                        end++;
                    }
                } else if (textBuffer.CurrentSnapshot[end] == '\n') {
                    end++;
                }

                using (var edit = textBuffer.CreateEdit()) {
                    var text = BuildMethod(
                        eventDescription,
                        methodName,
                        new string(' ', classDef.Body.GetStart(ast).Column - 1),
                        view.Options.IsConvertTabsToSpacesEnabled() ?
                            view.Options.GetIndentSize() :
                            -1);

                    edit.Insert(end, text);
                    edit.Apply();
                    return true;
                }
            }


            return false;
        }

        private ClassDefinition GetClassForEvents() {
            PythonAst ast;
            return GetClassForEvents(out ast);
        }

        private ClassDefinition GetClassForEvents(out PythonAst ast) {
            ast = null;
            var analysis = _pythonFileNode.GetAnalysis() as IPythonProjectEntry;

            if (analysis != null) {
                // TODO: Wait for up to date analysis
                ast = analysis.WaitForCurrentTree();
                var suiteStmt = ast.Body as SuiteStatement;
                foreach (var stmt in suiteStmt.Statements) {
                    var classDef = stmt as ClassDefinition;
                    // TODO: Make sure this is the right class
                    if (classDef != null) {
                        return classDef;
                    }
                }
            }
            return null;
        }

        private static string BuildMethod(EventDescription eventDescription, string methodName, string indentation, int tabSize) {
            StringBuilder text = new StringBuilder();
            text.AppendLine(indentation);
            text.Append(indentation);
            text.Append("def ");
            text.Append(methodName);
            text.Append('(');
            text.Append("self");
            foreach (var param in eventDescription.Parameters) {
                text.Append(", ");
                text.Append(param.Name);
            }
            text.AppendLine("):");
            if (tabSize < 0) {
                text.Append(indentation);
                text.Append("\tpass");
            } else {
                text.Append(indentation);
                text.Append(' ', tabSize);
                text.Append("pass");
            }
            text.AppendLine();

            return text.ToString();
        }

        public override string CreateUniqueMethodName(string objectName, EventDescription eventDescription) {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}_{1}", objectName, eventDescription.Name);
        }

        public override IEnumerable<string> GetCompatibleMethods(EventDescription eventDescription) {
            var classDef = GetClassForEvents();
            SuiteStatement suite = classDef.Body as SuiteStatement;

            if (suite != null) {
                foreach (var methodCandidate in suite.Statements) {
                    FunctionDefinition funcDef = methodCandidate as FunctionDefinition;
                    if (funcDef != null) {
                        if (funcDef.Name.EndsWith("_" + eventDescription.Name)) {
                            yield return funcDef.Name;
                        }
                    }
                }
            }
        }

        public override IEnumerable<string> GetMethodHandlers(EventDescription eventDescription, string objectName) {
            return new string[0];
        }

        public override bool IsExistingMethodName(EventDescription eventDescription, string methodName) {
            return FindMethod(methodName) != null;
        }

        private FunctionDefinition FindMethod(string methodName) {
            var classDef = GetClassForEvents();
            SuiteStatement suite = classDef.Body as SuiteStatement;

            if (suite != null) {
                foreach (var methodCandidate in suite.Statements) {
                    FunctionDefinition funcDef = methodCandidate as FunctionDefinition;
                    if (funcDef != null) {
                        if (funcDef.Name == methodName) {
                            return funcDef;
                        }
                    }
                }
            }

            return null;
        }

        public override bool RemoveEventHandler(EventDescription eventDescription, string objectName, string methodName) {
            var method = FindMethod(methodName);
            if (method != null) {
                var view = _pythonFileNode.GetTextView();
                var textBuffer = _pythonFileNode.GetTextBuffer();

                // appending a method adds 2 extra newlines, we want to remove those if those are still
                // present so that adding a handler and then removing it leaves the buffer unchanged.

                using (var edit = textBuffer.CreateEdit()) {
                    int start = method.StartIndex - 1;

                    // eat the newline we insert before the method
                    while (start >= 0) {
                        var curChar = edit.Snapshot[start];
                        if (!Char.IsWhiteSpace(curChar)) {
                            break;
                        } else if (curChar == ' ' || curChar == '\t') {
                            start--;
                            continue;
                        } else if (curChar == '\n') {
                            if (start != 0) {
                                if (edit.Snapshot[start - 1] == '\r') {
                                    start--;
                                }
                            }
                            start--;
                            break;
                        } else if (curChar == '\r') {
                            start--;
                            break;
                        }

                        start--;
                    }

                    
                    // eat the newline we insert at the end of the method
                    int end = method.EndIndex;                    
                    while (end < edit.Snapshot.Length) {
                        if (edit.Snapshot[end] == '\n') {
                            end++;
                            break;
                        } else if (edit.Snapshot[end] == '\r') {
                            if (end < edit.Snapshot.Length - 1 && edit.Snapshot[end + 1] == '\n') {
                                end += 2;
                            } else {
                                end++;
                            }
                            break;
                        } else if (edit.Snapshot[end] == ' ' || edit.Snapshot[end] == '\t') {
                            end++;
                            continue;
                        } else {
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

        public override bool RemoveHandlesForName(string elementName) {
            throw new NotImplementedException();
        }

        public override bool RemoveMethod(EventDescription eventDescription, string methodName) {
            throw new NotImplementedException();
        }

        public override void SetClassName(string className) {
            throw new NotImplementedException();
        }

        public override bool ShowMethod(EventDescription eventDescription, string methodName) {
            var method = FindMethod(methodName);
            if (method != null) {
                var view = _pythonFileNode.GetTextView();
                view.Caret.MoveTo(new VisualStudio.Text.SnapshotPoint(view.TextSnapshot, method.StartIndex));
                view.Caret.EnsureVisible();
                return true;
            }

            return false;
        }

        public override void ValidateMethodName(EventDescription eventDescription, string methodName) {
        }
    }
}

#endif
