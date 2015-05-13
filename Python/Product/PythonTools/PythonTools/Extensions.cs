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
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Editor.Core;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.InterpreterList;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.PythonTools.Project;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
#if DEV14_OR_LATER
using Microsoft.VisualStudio.InteractiveWindow;
#else
using Microsoft.VisualStudio.Repl;
#endif
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;

// Disables obsolete warning for ISmartTag* interfaces - http://go.microsoft.com/fwlink/?LinkId=394601
#pragma warning disable 618

namespace Microsoft.PythonTools {
#if DEV14_OR_LATER
    using IReplEvaluator = IInteractiveEvaluator;
#endif

    public static class Extensions {
        internal static bool IsAppxPackageableProject(this ProjectNode projectNode) {
            var appxProp = projectNode.BuildProject.GetPropertyValue(ProjectFileConstants.AppxPackage);
            var containerProp = projectNode.BuildProject.GetPropertyValue(ProjectFileConstants.WindowsAppContainer);
            var appxFlag = false;
            var containerFlag = false;

            if (bool.TryParse(appxProp, out appxFlag) && bool.TryParse(containerProp, out containerFlag)) {
                return appxFlag && containerFlag;
            } else {
                return false;
            }
        }

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
                case PythonMemberType.Keyword: group = StandardGlyphGroup.GlyphKeyword; break;
                case PythonMemberType.Function:
                case PythonMemberType.Method:
                default:
                    group = StandardGlyphGroup.GlyphGroupMethod;
                    break;
            }
            return group;
        }

        internal static bool CanComplete(this ClassificationSpan token) {
            return token.ClassificationType.IsOfType(PredefinedClassificationTypeNames.Keyword) |
                token.ClassificationType.IsOfType(PredefinedClassificationTypeNames.Identifier);
        }

        /// <summary>
        /// Returns the span to use for the provided intellisense session.
        /// </summary>
        /// <returns>A tracking span. The span may be of length zero if there
        /// is no suitable token at the trigger point.</returns>
        internal static ITrackingSpan GetApplicableSpan(this IIntellisenseSession session, ITextBuffer buffer) {
            var snapshot = buffer.CurrentSnapshot;
            var triggerPoint = session.GetTriggerPoint(buffer);

            var span = snapshot.GetApplicableSpan(triggerPoint);
            if (span != null) {
                return span;
            }
            return snapshot.CreateTrackingSpan(triggerPoint.GetPosition(snapshot), 0, SpanTrackingMode.EdgeInclusive);
        }

        /// <summary>
        /// Returns the applicable span at the provided position.
        /// </summary>
        /// <returns>A tracking span, or null if there is no token at the
        /// provided position.</returns>
        internal static ITrackingSpan GetApplicableSpan(this ITextSnapshot snapshot, ITrackingPoint point) {
            return snapshot.GetApplicableSpan(point.GetPosition(snapshot));
        }

        /// <summary>
        /// Returns the applicable span at the provided position.
        /// </summary>
        /// <returns>A tracking span, or null if there is no token at the
        /// provided position.</returns>
        internal static ITrackingSpan GetApplicableSpan(this ITextSnapshot snapshot, int position) {
            var classifier = snapshot.TextBuffer.GetPythonClassifier();
            var line = snapshot.GetLineFromPosition(position);
            if (classifier == null || line == null) {
                return null;
            }

            var spanLength = position - line.Start.Position;
            // Increase position by one to include 'fob' in: "abc.|fob"
            if (spanLength < line.Length) {
                spanLength += 1;
            }
            
            var classifications = classifier.GetClassificationSpans(new SnapshotSpan(line.Start, spanLength));
            // Handle "|"
            if (classifications == null || classifications.Count == 0) {
                return null;
            }

            var lastToken = classifications[classifications.Count - 1];
            // Handle "fob |"
            if (lastToken == null || position > lastToken.Span.End) {
                return null;
            }

            if (position > lastToken.Span.Start) {
                if (lastToken.CanComplete()) {
                    // Handle "fo|o"
                    return snapshot.CreateTrackingSpan(lastToken.Span, SpanTrackingMode.EdgeInclusive);
                } else {
                    // Handle "<|="
                    return null;
                }
            }

            var secondLastToken = classifications.Count >= 2 ? classifications[classifications.Count - 2] : null;
            if (lastToken.Span.Start == position && lastToken.CanComplete() && 
                (secondLastToken == null ||             // Handle "|fob"
                 position > secondLastToken.Span.End || // Handle "if |fob"
                 !secondLastToken.CanComplete())) {     // Handle "abc.|fob"
                return snapshot.CreateTrackingSpan(lastToken.Span, SpanTrackingMode.EdgeInclusive);
            }

            // Handle "abc|."
            // ("ab|c." would have been treated as "ab|c")
            if (secondLastToken != null && secondLastToken.Span.End == position && secondLastToken.CanComplete()) {
                return snapshot.CreateTrackingSpan(secondLastToken.Span, SpanTrackingMode.EdgeInclusive);
            }

            return null;
        }

        internal static ITrackingSpan CreateTrackingSpan(this IQuickInfoSession session, ITextBuffer buffer) {
            var triggerPoint = session.GetTriggerPoint(buffer);
            var position = triggerPoint.GetPosition(buffer.CurrentSnapshot);
            if (position == buffer.CurrentSnapshot.Length) {
                return ((IIntellisenseSession)session).GetApplicableSpan(buffer);
            }

            return buffer.CurrentSnapshot.CreateTrackingSpan(position, 1, SpanTrackingMode.EdgeInclusive);
        }

#if DEV14_OR_LATER
#pragma warning disable 0618
#endif

        // TODO: Switch from smart tags to Light Bulb: http://go.microsoft.com/fwlink/?LinkId=394601
        internal static ITrackingSpan CreateTrackingSpan(this ISmartTagSession session, ITextBuffer buffer) {
            var triggerPoint = session.GetTriggerPoint(buffer);
            var position = triggerPoint.GetPosition(buffer.CurrentSnapshot);
            if (position == buffer.CurrentSnapshot.Length) {
                return ((IIntellisenseSession)session).GetApplicableSpan(buffer);
            }

            var triggerChar = triggerPoint.GetCharacter(buffer.CurrentSnapshot);
            if (position != 0 && !char.IsLetterOrDigit(triggerChar)) {
                // end of line, back up one char as we may have an identifier
                return buffer.CurrentSnapshot.CreateTrackingSpan(position - 1, 1, SpanTrackingMode.EdgeInclusive);
            }

            return buffer.CurrentSnapshot.CreateTrackingSpan(position, 1, SpanTrackingMode.EdgeInclusive);
        }

#if DEV14_OR_LATER
#pragma warning restore 0618
#endif

        public static IPythonInterpreterFactory GetPythonInterpreterFactory(this IVsHierarchy self) {
            var node = (self.GetProject().GetCommonProject() as PythonProjectNode);
            if (node != null) {
                return node.GetInterpreterFactory();
            }
            return null;
        }

        public static IEnumerable<IVsProject> EnumerateLoadedProjects(this IVsSolution solution) {
            var guid = new Guid(PythonConstants.ProjectFactoryGuid);
            IEnumHierarchies hierarchies;
            ErrorHandler.ThrowOnFailure((solution.GetProjectEnum(
                (uint)(__VSENUMPROJFLAGS.EPF_MATCHTYPE | __VSENUMPROJFLAGS.EPF_LOADEDINSOLUTION),
                ref guid,
                out hierarchies)));
            IVsHierarchy[] hierarchy = new IVsHierarchy[1];
            uint fetched;
            while (ErrorHandler.Succeeded(hierarchies.Next(1, hierarchy, out fetched)) && fetched == 1) {
                var project = hierarchy[0] as IVsProject;
                if (project != null) {
                    yield return project;
                }
            }
        }

        internal static IEnumerable<PythonProjectNode> EnumerateLoadedPythonProjects(this IVsSolution solution) {
            return EnumerateLoadedProjects(solution)
                .Select(p => p.GetPythonProject())
                .Where(p => p != null);
        }

        public static IModuleContext GetModuleContext(this ITextBuffer buffer, IServiceProvider serviceProvider) {
            if (buffer == null) {
                return null;
            }

            var analyzer = buffer.GetAnalyzer(serviceProvider);
            if (analyzer == null) {
                return null;
            }

            var path = buffer.GetFilePath();
            if (string.IsNullOrEmpty(path)) {
                return null;
            }

            var entry = analyzer.GetEntryFromFile(path);
            if (entry == null) {
                return null;
            }
            return entry.AnalysisContext;
        }

        internal static PythonProjectNode GetPythonProject(this IVsProject project) {
            return ((IVsHierarchy)project).GetProject().GetCommonProject() as PythonProjectNode;
        }

        internal static PythonProjectNode GetPythonProject(this EnvDTE.Project project) {
            return project.GetCommonProject() as PythonProjectNode;
        }
        
        internal static void GotoSource(this LocationInfo location, IServiceProvider serviceProvider) {
            string zipFileName = VsProjectAnalyzer.GetZipFileName(location.ProjectEntry);
            if (zipFileName == null) {
                PythonToolsPackage.NavigateTo(
                    serviceProvider,
                    location.FilePath,
                    Guid.Empty,
                    location.Line - 1,
                    location.Column - 1);
            }
        }

        internal static bool TryGetProjectEntry(this ITextBuffer buffer, out IProjectEntry entry) {
            return buffer.Properties.TryGetProperty<IProjectEntry>(typeof(IProjectEntry), out entry);
        }

        internal static bool TryGetPythonProjectEntry(this ITextBuffer buffer, out IPythonProjectEntry entry) {
            IProjectEntry e;
            if (buffer.TryGetProjectEntry(out e) && (entry = e as IPythonProjectEntry) != null) {
                return true;
            }
            entry = null;
            return false;
        }

        internal static IProjectEntry GetProjectEntry(this ITextBuffer buffer) {
            IProjectEntry res;
            buffer.TryGetProjectEntry(out res);
            return res;
        }

        internal static IPythonProjectEntry GetPythonProjectEntry(this ITextBuffer buffer) {
            IPythonProjectEntry res;
            buffer.TryGetPythonProjectEntry(out res);
            return res;
        }

        internal static PythonProjectNode GetProject(this ITextBuffer buffer, IServiceProvider serviceProvider) {
            var path = buffer.GetFilePath();
            if (path != null) {
                var sln = serviceProvider.GetService(typeof(SVsSolution)) as IVsSolution;
                if (sln != null) {
                    foreach (var proj in sln.EnumerateLoadedPythonProjects()) {
                        int found;
                        var priority = new VSDOCUMENTPRIORITY[1];
                        uint itemId;
                        ErrorHandler.ThrowOnFailure(proj.IsDocumentInProject(path, out found, priority, out itemId));
                        if (found != 0) {
                            return proj;
                        }
                    }
                }
            }
            return null;
        }

        internal static VsProjectAnalyzer GetAnalyzer(this ITextView textView, IServiceProvider serviceProvider) {
            PythonReplEvaluator evaluator;
            if (textView.Properties.TryGetProperty<PythonReplEvaluator>(typeof(PythonReplEvaluator), out evaluator)) {
                return evaluator.ReplAnalyzer;
            }
            return textView.TextBuffer.GetAnalyzer(serviceProvider);
        }

        internal static SnapshotPoint? GetCaretPosition(this ITextView view) {
            return view.BufferGraph.MapDownToFirstMatch(
               new SnapshotPoint(view.TextBuffer.CurrentSnapshot, view.Caret.Position.BufferPosition),
               PointTrackingMode.Positive,
               EditorExtensions.IsPythonContent,
               PositionAffinity.Successor
            );
        }

        internal static ExpressionAnalysis GetExpressionAnalysis(this ITextView view, IServiceProvider serviceProvider) {
            ITrackingSpan span = GetCaretSpan(view);
            return span.TextBuffer.CurrentSnapshot.AnalyzeExpression(serviceProvider, span, false);
        }

        internal static ITrackingSpan GetCaretSpan(this ITextView view) {
            var caretPoint = view.GetCaretPosition();
            Debug.Assert(caretPoint != null);
            var snapshot = caretPoint.Value.Snapshot;
            var caretPos = caretPoint.Value.Position;

            // fob(
            //    ^
            //    +---  Caret here
            //
            // We want to lookup fob, not fob(
            //
            ITrackingSpan span;
            if (caretPos != snapshot.Length) {
                string curChar = snapshot.GetText(caretPos, 1);
                if (!IsIdentifierChar(curChar[0]) && caretPos > 0) {
                    string prevChar = snapshot.GetText(caretPos - 1, 1);
                    if (IsIdentifierChar(prevChar[0])) {
                        caretPos--;
                    }
                }
                span = snapshot.CreateTrackingSpan(
                    caretPos,
                    1,
                    SpanTrackingMode.EdgeInclusive
                );
            } else {
                span = snapshot.CreateTrackingSpan(
                    caretPos,
                    0,
                    SpanTrackingMode.EdgeInclusive
                );
            }

            return span;
        }

        private static bool IsIdentifierChar(char curChar) {
            return Char.IsLetterOrDigit(curChar) || curChar == '_';
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
                    if (filenameLen != 0) {
                        int bytesRead = 0;
                        do {
                            bytesRead += socket.Receive(buffer, bytesRead, filenameLen - bytesRead, SocketFlags.None);
                        } while (bytesRead != filenameLen);
                    }

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

        internal static VsProjectAnalyzer GetAnalyzer(this ITextBuffer buffer, IServiceProvider serviceProvider) {
            PythonProjectNode pyProj;
            if (!buffer.Properties.TryGetProperty<PythonProjectNode>(typeof(PythonProjectNode), out pyProj)) {
                pyProj = buffer.GetProject(serviceProvider);
                if (pyProj != null) {
                    buffer.Properties.AddProperty(typeof(PythonProjectNode), pyProj);
                }
            }

            if (pyProj != null) {
                return pyProj.GetAnalyzer();
            }

            VsProjectAnalyzer analyzer;
            // exists for tests where we don't run in VS and for the existing changes preview
            if (buffer.Properties.TryGetProperty<VsProjectAnalyzer>(typeof(VsProjectAnalyzer), out analyzer)) {
                return analyzer;
            }

            return serviceProvider.GetPythonToolsService().DefaultAnalyzer;
        }

        internal static PythonToolsService GetPythonToolsService(this IServiceProvider serviceProvider) {
            if (serviceProvider == null) {
                return null;
            }
            return (PythonToolsService)serviceProvider.GetService(typeof(PythonToolsService));
        }

        internal static IComponentModel GetComponentModel(this IServiceProvider serviceProvider) {
            if (serviceProvider == null) {
                return null;
            }
            return (IComponentModel)serviceProvider.GetService(typeof(SComponentModel));
        }

        public static string BrowseForFileSave(this IServiceProvider provider, IntPtr owner, string filter, string initialPath = null) {
            if (string.IsNullOrEmpty(initialPath)) {
                initialPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + Path.DirectorySeparatorChar;
            }

            IVsUIShell uiShell = provider.GetService(typeof(SVsUIShell)) as IVsUIShell;
            if (null == uiShell) {
                using (var sfd = new System.Windows.Forms.SaveFileDialog()) {
                    sfd.AutoUpgradeEnabled = true;
                    sfd.Filter = filter;
                    sfd.FileName = Path.GetFileName(initialPath);
                    sfd.InitialDirectory = Path.GetDirectoryName(initialPath);
                    DialogResult result;
                    if (owner == IntPtr.Zero) {
                        result = sfd.ShowDialog();
                    } else {
                        result = sfd.ShowDialog(NativeWindow.FromHandle(owner));
                    }
                    if (result == DialogResult.OK) {
                        return sfd.FileName;
                    } else {
                        return null;
                    }
                }
            }

            if (owner == IntPtr.Zero) {
                ErrorHandler.ThrowOnFailure(uiShell.GetDialogOwnerHwnd(out owner));
            }

            VSSAVEFILENAMEW[] saveInfo = new VSSAVEFILENAMEW[1];
            saveInfo[0].lStructSize = (uint)Marshal.SizeOf(typeof(VSSAVEFILENAMEW));
            saveInfo[0].pwzFilter = filter.Replace('|', '\0') + "\0";
            saveInfo[0].hwndOwner = owner;
            saveInfo[0].nMaxFileName = 260;
            var pFileName = Marshal.AllocCoTaskMem(520);
            saveInfo[0].pwzFileName = pFileName;
            saveInfo[0].pwzInitialDir = Path.GetDirectoryName(initialPath);
            var nameArray = (Path.GetFileName(initialPath) + "\0").ToCharArray();
            Marshal.Copy(nameArray, 0, pFileName, nameArray.Length);
            try {
                int hr = uiShell.GetSaveFileNameViaDlg(saveInfo);
                if (hr == VSConstants.OLE_E_PROMPTSAVECANCELLED) {
                    return null;
                }
                ErrorHandler.ThrowOnFailure(hr);
                return Marshal.PtrToStringAuto(saveInfo[0].pwzFileName);
            } finally {
                if (pFileName != IntPtr.Zero) {
                    Marshal.FreeCoTaskMem(pFileName);
                }
            }
        }

        public static string BrowseForFileOpen(this IServiceProvider serviceProvider, IntPtr owner, string filter, string initialPath = null) {
            if (string.IsNullOrEmpty(initialPath)) {
                initialPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + Path.DirectorySeparatorChar;
            }

            IVsUIShell uiShell = serviceProvider.GetService(typeof(SVsUIShell)) as IVsUIShell;
            if (null == uiShell) {
                using (var sfd = new System.Windows.Forms.OpenFileDialog()) {
                    sfd.AutoUpgradeEnabled = true;
                    sfd.Filter = filter;
                    sfd.FileName = Path.GetFileName(initialPath);
                    sfd.InitialDirectory = Path.GetDirectoryName(initialPath);
                    DialogResult result;
                    if (owner == IntPtr.Zero) {
                        result = sfd.ShowDialog();
                    } else {
                        result = sfd.ShowDialog(NativeWindow.FromHandle(owner));
                    }
                    if (result == DialogResult.OK) {
                        return sfd.FileName;
                    } else {
                        return null;
                    }
                }
            }

            if (owner == IntPtr.Zero) {
                ErrorHandler.ThrowOnFailure(uiShell.GetDialogOwnerHwnd(out owner));
            }

            VSOPENFILENAMEW[] openInfo = new VSOPENFILENAMEW[1];
            openInfo[0].lStructSize = (uint)Marshal.SizeOf(typeof(VSOPENFILENAMEW));
            openInfo[0].pwzFilter = filter.Replace('|', '\0') + "\0";
            openInfo[0].hwndOwner = owner;
            openInfo[0].nMaxFileName = 260;
            var pFileName = Marshal.AllocCoTaskMem(520);
            openInfo[0].pwzFileName = pFileName;
            openInfo[0].pwzInitialDir = Path.GetDirectoryName(initialPath);
            var nameArray = (Path.GetFileName(initialPath) + "\0").ToCharArray();
            Marshal.Copy(nameArray, 0, pFileName, nameArray.Length);
            try {
                int hr = uiShell.GetOpenFileNameViaDlg(openInfo);
                if (hr == VSConstants.OLE_E_PROMPTSAVECANCELLED) {
                    return null;
                }
                ErrorHandler.ThrowOnFailure(hr);
                return Marshal.PtrToStringAuto(openInfo[0].pwzFileName);
            } finally {
                if (pFileName != IntPtr.Zero) {
                    Marshal.FreeCoTaskMem(pFileName);
                }
            }
        }

        internal static IContentType GetPythonContentType(this IServiceProvider provider) {
            return provider.GetComponentModel().GetService<IContentTypeRegistryService>().GetContentType(PythonCoreConstants.ContentType);
        }

        internal static EnvDTE.DTE GetDTE(this IServiceProvider provider) {
            return (EnvDTE.DTE)provider.GetService(typeof(EnvDTE.DTE));
        }

        internal static SolutionEventsListener GetSolutionEvents(this IServiceProvider serviceProvider) {
            return (SolutionEventsListener)serviceProvider.GetService(typeof(SolutionEventsListener));
        }

        internal static void GlobalInvoke(this IServiceProvider serviceProvider, CommandID cmdID) {
            OleMenuCommandService mcs = serviceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            mcs.GlobalInvoke(cmdID);
        }

        internal static void GlobalInvoke(this IServiceProvider serviceProvider, CommandID cmdID, object arg) {
            OleMenuCommandService mcs = serviceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            mcs.GlobalInvoke(cmdID, arg);
        }

        internal static void ShowOptionsPage(this IServiceProvider serviceProvider, Type optionsPageType) {
            CommandID cmd = new CommandID(VSConstants.GUID_VSStandardCommandSet97, VSConstants.cmdidToolsOptions);
            serviceProvider.GlobalInvoke(
                cmd,
                optionsPageType.GUID.ToString()
            );
        }

        internal static void ShowInterpreterList(this IServiceProvider serviceProvider) {
            serviceProvider.ShowWindowPane(typeof(InterpreterListToolWindow), true);
        }

        internal static void ShowWindowPane(this IServiceProvider serviceProvider, Type windowPane, bool focus) {
            var toolWindowService = (IPythonToolsToolWindowService)serviceProvider.GetService(typeof(IPythonToolsToolWindowService));
            toolWindowService.ShowWindowPane(windowPane, focus);
        }

        public static string BrowseForDirectory(this IServiceProvider provider, IntPtr owner, string initialDirectory = null) {
            IVsUIShell uiShell = provider.GetService(typeof(SVsUIShell)) as IVsUIShell;
            if (null == uiShell) {
                using (var ofd = new FolderBrowserDialog()) {
                    ofd.RootFolder = Environment.SpecialFolder.Desktop;
                    ofd.ShowNewFolderButton = false;
                    DialogResult result;
                    if (owner == IntPtr.Zero) {
                        result = ofd.ShowDialog();
                    } else {
                        result = ofd.ShowDialog(NativeWindow.FromHandle(owner));
                    }
                    if (result == DialogResult.OK) {
                        return ofd.SelectedPath;
                    } else {
                        return null;
                    }
                }
            }

            if (owner == IntPtr.Zero) {
                ErrorHandler.ThrowOnFailure(uiShell.GetDialogOwnerHwnd(out owner));
            }

            VSBROWSEINFOW[] browseInfo = new VSBROWSEINFOW[1];
            browseInfo[0].lStructSize = (uint)Marshal.SizeOf(typeof(VSBROWSEINFOW));
            browseInfo[0].pwzInitialDir = initialDirectory;
            browseInfo[0].hwndOwner = owner;
            browseInfo[0].nMaxDirName = 260;
            IntPtr pDirName = Marshal.AllocCoTaskMem(520);
            browseInfo[0].pwzDirName = pDirName;
            try {
                int hr = uiShell.GetDirectoryViaBrowseDlg(browseInfo);
                if (hr == VSConstants.OLE_E_PROMPTSAVECANCELLED) {
                    return null;
                }
                ErrorHandler.ThrowOnFailure(hr);
                return Marshal.PtrToStringAuto(browseInfo[0].pwzDirName);
            } finally {
                if (pDirName != IntPtr.Zero) {
                    Marshal.FreeCoTaskMem(pDirName);
                }
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

        internal static bool IsAnalysisCurrent(this IPythonInterpreterFactory factory) {
            var interpFact = factory as IPythonInterpreterFactoryWithDatabase;
            if (interpFact != null) {
                return interpFact.IsCurrent;
            }

            return true;
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

        internal static T Pop<T>(this List<T> list) {
            if (list.Count == 0) {
                throw new InvalidOperationException();
            }
            var res = list[list.Count - 1];
            list.RemoveAt(list.Count - 1);
            return res;
        }

        internal static T Peek<T>(this List<T> list) {
            if (list.Count == 0) {
                throw new InvalidOperationException();
            }
            return list[list.Count - 1];
        }

        internal static System.Threading.Tasks.Task StartNew(this TaskScheduler scheduler, Action func) {
            return System.Threading.Tasks.Task.Factory.StartNew(func, default(CancellationToken), TaskCreationOptions.None, scheduler);
        }

        internal static int GetStartIncludingIndentation(this Node self, PythonAst ast) {
            return self.StartIndex - (self.GetIndentationLevel(ast) ?? "").Length;
        }

        internal static string LimitLines(
            this string str,
            int maxLines = 30,
            int charsPerLine = 200,
            bool ellipsisAtEnd = true,
            bool stopAtFirstBlankLine = false
        ) {
            if (string.IsNullOrEmpty(str)) {
                return str;
            }

            int lineCount = 0;
            var prettyPrinted = new StringBuilder();
            bool wasEmpty = true;

            using (var reader = new StringReader(str)) {
                for (var line = reader.ReadLine(); line != null && lineCount < maxLines; line = reader.ReadLine()) {
                    if (string.IsNullOrWhiteSpace(line)) {
                        if (wasEmpty) {
                            continue;
                        }
                        wasEmpty = true;
                        if (stopAtFirstBlankLine) {
                            lineCount = maxLines;
                            break;
                        }
                        lineCount += 1;
                        prettyPrinted.AppendLine();
                    } else {
                        wasEmpty = false;
                        lineCount += (line.Length / charsPerLine) + 1;
                        prettyPrinted.AppendLine(line);
                    }
                }
            }
            if (ellipsisAtEnd && lineCount >= maxLines) {
                prettyPrinted.AppendLine("...");
            }
            return prettyPrinted.ToString().Trim();
        }
    }
}
