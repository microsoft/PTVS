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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Project;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Repl;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.PythonTools {
    public static class Extensions {
        public static StandardGlyphGroup ToGlyphGroup(this PythonMemberType objectType) {
            StandardGlyphGroup group;
            switch (objectType) {
                case PythonMemberType.Class: group = StandardGlyphGroup.GlyphGroupClass; break;
                case PythonMemberType.DelegateInstance: 
                case PythonMemberType.Delegate: group = StandardGlyphGroup.GlyphGroupDelegate; break;
                case PythonMemberType.Enum: group = StandardGlyphGroup.GlyphGroupEnum; break;
                case PythonMemberType.Namespace: group = StandardGlyphGroup.GlyphGroupNamespace; break;
                case PythonMemberType.Multiple: group = StandardGlyphGroup.GlyphGroupOverload; break;
                case PythonMemberType.Field: group = StandardGlyphGroup.GlyphGroupField; break;
                case PythonMemberType.Module: group = StandardGlyphGroup.GlyphGroupModule; break;
                case PythonMemberType.Property: group = StandardGlyphGroup.GlyphGroupProperty; break;
                case PythonMemberType.Instance: group = StandardGlyphGroup.GlyphGroupVariable; break;
                case PythonMemberType.Constant: group = StandardGlyphGroup.GlyphGroupVariable; break;
                case PythonMemberType.EnumInstance: group = StandardGlyphGroup.GlyphGroupEnumMember; break;
                case PythonMemberType.Event: group = StandardGlyphGroup.GlyphGroupEvent; break;
                case PythonMemberType.Function:
                case PythonMemberType.Method:
                default:
                    group = StandardGlyphGroup.GlyphGroupMethod;
                    break;
            }
            return group;
        }

        internal static ITrackingSpan CreateTrackingSpan(this IIntellisenseSession session, ITextBuffer buffer) {
            var triggerPoint = session.GetTriggerPoint(buffer);
            
            var position = session.GetTriggerPoint(buffer).GetPosition(buffer.CurrentSnapshot);

            return buffer.CurrentSnapshot.CreateTrackingSpan(position, 0, SpanTrackingMode.EdgeInclusive);
        }

        internal static ITrackingSpan CreateTrackingSpan(this IQuickInfoSession session, ITextBuffer buffer) {
            var triggerPoint = session.GetTriggerPoint(buffer);
            var position = session.GetTriggerPoint(buffer).GetPosition(buffer.CurrentSnapshot);
            if (position == buffer.CurrentSnapshot.Length) {
                return ((IIntellisenseSession)session).CreateTrackingSpan(buffer);
            }

            return buffer.CurrentSnapshot.CreateTrackingSpan(position, 1, SpanTrackingMode.EdgeInclusive);
        }

        internal static PythonProjectNode GetPythonProject(this EnvDTE.Project project) {
            return project.GetCommonProject() as PythonProjectNode;
        }

        internal static EnvDTE.Project GetProject(this IVsHierarchy hierarchy) {
            object project;

            ErrorHandler.ThrowOnFailure(
                hierarchy.GetProperty(
                    VSConstants.VSITEMID_ROOT,
                    (int)__VSHPROPID.VSHPROPID_ExtObject,
                    out project
                )
            );

            return (project as EnvDTE.Project);
        }
        
        internal static void GotoSource(this LocationInfo location) {
            PythonToolsPackage.NavigateTo(
                location.FilePath,
                Guid.Empty,
                location.Line - 1,
                location.Column - 1);
        }

        internal static bool TryGetAnalysis(this ITextBuffer buffer, out IProjectEntry analysis) {
            return buffer.Properties.TryGetProperty<IProjectEntry>(typeof(IProjectEntry), out analysis);
        }

        internal static bool TryGetPythonAnalysis(this ITextBuffer buffer, out IPythonProjectEntry analysis) {
            IProjectEntry entry;
            if (buffer.TryGetAnalysis(out entry) && (analysis = entry as IPythonProjectEntry) != null) {
                return true;
            }
            analysis = null;
            return false;
        }

        internal static IProjectEntry GetAnalysis(this ITextBuffer buffer) {
            IProjectEntry res;
            buffer.TryGetAnalysis(out res);
            return res;
        }

        internal static IPythonProjectEntry GetPythonAnalysis(this ITextBuffer buffer) {
            IPythonProjectEntry res;
            buffer.TryGetPythonAnalysis(out res);
            return res;
        }

        internal static EnvDTE.Project GetProject(this ITextBuffer buffer) {
            var path = buffer.GetFilePath();
            if (path != null && PythonToolsPackage.Instance != null) {
                var item = PythonToolsPackage.Instance.DTE.Solution.FindProjectItem(path);
                if (item != null) {
                    return item.ContainingProject;
                }
            }
            return null;
        }

        internal static ProjectAnalyzer GetAnalyzer(this ITextView textView) {
            PythonReplEvaluator evaluator;
            if (textView.Properties.TryGetProperty<PythonReplEvaluator>(typeof(PythonReplEvaluator), out evaluator)) {
                return evaluator.ReplAnalyzer;
            }
            return textView.TextBuffer.GetAnalyzer();
        }

        /// <summary>
        /// Reads a string from the socket which is encoded as:
        ///     U, byte count, bytes 
        ///     A, byte count, ASCII
        ///     
        /// Which supports either UTF-8 or ASCII strings.
        /// </summary>
        internal static string ReadString(this Socket socket) {
            byte[] cmd_buffer = new byte[4];
            if (socket.Receive(cmd_buffer, 1, SocketFlags.None) == 1) {
                bool isUnicode = cmd_buffer[0] == 'U';

                if (socket.Receive(cmd_buffer) == 4) {
                    int filenameLen = BitConverter.ToInt32(cmd_buffer, 0);
                    byte[] buffer = new byte[filenameLen];
                    int bytesRead = 0;
                    do {
                        bytesRead += socket.Receive(buffer, bytesRead, filenameLen - bytesRead, SocketFlags.None);
                    } while (bytesRead != filenameLen);

                    if (isUnicode) {
                        return Encoding.UTF8.GetString(buffer);
                    } else {
                        char[] chars = new char[buffer.Length];
                        for (int i = 0; i < buffer.Length; i++) {
                            chars[i] = (char)buffer[i];
                        }
                        return new string(chars);
                    }
                } else {
                    Debug.Assert(false, "Failed to read length");
                }
            } else {
                Debug.Assert(false, "Failed to read unicode/ascii byte");
            }
            return null;
        }

        internal static int ReadInt(this Socket socket) {
            byte[] cmd_buffer = new byte[4];
            if (socket.Receive(cmd_buffer) == 4) {
                return BitConverter.ToInt32(cmd_buffer, 0);
            }
            throw new InvalidOperationException();
        }

        internal static ProjectAnalyzer GetAnalyzer(this ITextBuffer buffer) {
            var project = buffer.GetProject();
            if (project != null) {
                var pyProj = project.GetPythonProject();
                if (pyProj != null) {
                    return pyProj.GetAnalyzer();
                }
            }

            // exists for tests where we don't run in VS.
            ProjectAnalyzer analyzer;
            if (buffer.Properties.TryGetProperty<ProjectAnalyzer>(typeof(ProjectAnalyzer), out analyzer)) {
                return analyzer;
            }

            return PythonToolsPackage.Instance.DefaultAnalyzer;
        }

        internal static string GetFilePath(this ITextBuffer textBuffer) {
            ITextDocument textDocument;
            if (textBuffer.Properties.TryGetProperty<ITextDocument>(typeof(ITextDocument), out textDocument)) {
                return textDocument.FilePath;
            } else {
                return null;
            }
        }

        /// <summary>
        /// Checks to see if this is a REPL buffer starting with a extensible command such as %cls, %load, etc...
        /// </summary>
        internal static bool IsReplBufferWithCommand(this ITextSnapshot snapshot) {
            return snapshot.TextBuffer.Properties.ContainsProperty(typeof(IReplEvaluator)) &&
                   snapshot.Length != 0 &&
                   (snapshot[0] == '%' || snapshot[0] == '$'); // IPython and normal repl commands
        }

        internal static IPythonInterpreterFactory[] GetAllPythonInterpreterFactories(this IComponentModel model) {
            var interpreters = new List<IPythonInterpreterFactory>();
            foreach (var provider in model.GetExtensions<IPythonInterpreterFactoryProvider>()) {
                interpreters.AddRange(provider.GetInterpreterFactories());
            }
            interpreters.Sort((x, y) => x.GetInterpreterDisplay().CompareTo(y.GetInterpreterDisplay()));
            return interpreters.ToArray();
        }

        internal static T[] Append<T>(this T[] list, T item) {
            T[] res = new T[list.Length + 1];
            list.CopyTo(res, 0);
            res[res.Length - 1] = item;
            return res;
        }

        internal static bool IsOpenGrouping(this ClassificationSpan span) {
            return span.ClassificationType.IsOfType(PythonPredefinedClassificationTypeNames.Grouping) &&
                span.Span.Length == 1 &&
                (span.Span.GetText() == "{" || span.Span.GetText() == "[" || span.Span.GetText() == "(");
        }

        internal static bool IsCloseGrouping(this ClassificationSpan span) {
            return span.ClassificationType.IsOfType(PythonPredefinedClassificationTypeNames.Grouping) &&
                span.Span.Length == 1 &&
                (span.Span.GetText() == "}" || span.Span.GetText() == "]" || span.Span.GetText() == ")");
        }
    }
}
