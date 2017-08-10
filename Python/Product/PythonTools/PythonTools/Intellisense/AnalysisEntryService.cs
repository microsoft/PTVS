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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Projects;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Differencing;
using Microsoft.VisualStudio.Text.Editor;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Intellisense {
    public interface IAnalysisEntryService {
        /// <summary>
        /// Tries to get the analyzer and filename of the specified text buffer.
        /// </summary>
        /// <returns>True if an analyzer and filename are found.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="textBuffer"/> is null.</exception>
        bool TryGetAnalyzer(ITextBuffer textBuffer, out ProjectAnalyzer analyzer, out string filename);
        /// <summary>
        /// Tries to get the analyzer and filename of the specified text view. This is
        /// equivalent to using the view's default text buffer, with some added checks
        /// for non-standard editors.
        /// </summary>
        /// <returns>True if an analyzer and filename are found.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="textView"/> is null.</exception>
        bool TryGetAnalyzer(ITextView textView, out ProjectAnalyzer analyzer, out string filename);
        /// <summary>
        /// Gets all the analyzers that apply to the specified file. Must be
        /// called from the UI thread.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="filename"/> is null or empty.</exception>
        IEnumerable<ProjectAnalyzer> GetAnalyzersForFile(string filename);

        /// <summary>
        /// Returns a task that will be completed when an analyzer is assigned to the text buffer.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="textBuffer"/> is null.</exception>
        Task WaitForAnalyzerAsync(ITextBuffer textBuffer, CancellationToken cancellationToken);
        /// <summary>
        /// Returns a task that will be completed when an analyzer is assigned to the text view.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="textView"/> is null.</exception>
        Task WaitForAnalyzerAsync(ITextView textView, CancellationToken cancellationToken);

        /// <summary>
        /// Returns the default analyzer for the Visual Studio session. Must be accessed from
        /// the UI thread.
        /// </summary>
        ProjectAnalyzer DefaultAnalyzer { get; }
    }

    [Export(typeof(IAnalysisEntryService))]
    [Export(typeof(AnalysisEntryService))]
    class AnalysisEntryService : IAnalysisEntryService {
        private readonly PythonEditorServices _services;
        private readonly IWpfDifferenceViewerFactoryService _diffService;

        private static readonly object _waitForAnalyzerKey = new object();

        [ImportingConstructor]
        public AnalysisEntryService([Import] PythonEditorServices services) {
            _services = services;

            try {
                _diffService = _services.ComponentModel.GetService<IWpfDifferenceViewerFactoryService>();
            } catch (CompositionException) {
            } catch (ImportCardinalityMismatchException) {
            }
        }

        //public void SetAnalyzer(ITextBuffer textBuffer, VsProjectAnalyzer analyzer) {
        //    if (textBuffer == null) {
        //        throw new ArgumentNullException(nameof(textBuffer));
        //    }
        //
        //    if (analyzer == null) {
        //        textBuffer.Properties.RemoveProperty(typeof(VsProjectAnalyzer));
        //        return;
        //    }
        //
        //    textBuffer.Properties[typeof(VsProjectAnalyzer)] = analyzer;
        //
        //    TaskCompletionSource<object> tcs;
        //    if (textBuffer.Properties.TryGetProperty(_waitForAnalyzerKey, out tcs)) {
        //        tcs.TrySetResult(null);
        //        textBuffer.Properties.RemoveProperty(_waitForAnalyzerKey);
        //    }
        //}

        /// <summary>
        /// Gets the analysis entry for the given view and buffer.
        /// 
        /// For files on disk this is pretty easy - we analyze each file on it's own in a buffer parser.
        /// Therefore we map filename -> analyzer and then get the analysis from the analyzer.  If we
        /// determine an analyzer but the file isn't loaded into it for some reason this would return null.
        /// We can also apply some policy to buffers depending upon the view that they're hosted in.  For
        /// example if a buffer is outside of any projects, but hosted in a difference view with a buffer
        /// that is in a project, then we'll look in the view that has the project.
        /// 
        /// For interactive windows we will use the analyzer that's configured for the window.
        /// </summary>
        public bool TryGetAnalysisEntry(ITextView textView, ITextBuffer textBuffer, out AnalysisEntry entry) {
            var bi = PythonTextBufferInfo.TryGetForBuffer(textBuffer ?? textView?.TextBuffer);

            if (bi == null && textView != null) {
                // If we have a difference viewer we'll match the LHS w/ the RHS
                var viewer = _diffService?.TryGetViewerForTextView(textView);
                if (viewer != null) {
                    if (TryGetAnalysisEntry(viewer.DifferenceBuffer.RightBuffer, out entry)) {
                        return true;
                    }
                    if (TryGetAnalysisEntry(viewer.DifferenceBuffer.LeftBuffer, out entry)) {
                        return true;
                    }
                }
            }

            entry = bi?.AnalysisEntry;
            return entry != null;
        }

        /// <summary>
        /// Gets the analysis entry for the given buffer.
        /// 
        /// This will only succeed if the buffer is a file on disk. It is not able to support things like
        /// difference views because we don't know what view this buffer is hosted in. This method should
        /// only be used when we don't know the current view for the buffer. Otherwise, use
        /// <see cref="TryGetAnalysisEntry(ITextView, ITextBuffer, out AnalysisEntry)"/>
        /// </summary>
        public bool TryGetAnalysisEntry(ITextBuffer textBuffer, out AnalysisEntry entry) {
            return TryGetAnalysisEntry(null, textBuffer, out entry);
        }

        /// <summary>
        /// Gets the internal analyzer object from either a text view or buffer. Both arguments
        /// are optional, and <c>null</c> may be passed.
        /// </summary>
        public VsProjectAnalyzer GetVsAnalyzer(ITextView view, ITextBuffer buffer) {
            AnalysisEntry entry;
            return TryGetAnalysisEntry(view, buffer, out entry) ? entry.Analyzer : null;
        }

        #region IAnalysisEntryService members

        public ProjectAnalyzer DefaultAnalyzer => _services.Python?.DefaultAnalyzer;

        public Task WaitForAnalyzerAsync(ITextBuffer textBuffer, CancellationToken cancellationToken) {
            if (textBuffer == null) {
                throw new ArgumentNullException(nameof(textBuffer));
            }

            return _services.GetBufferInfo(textBuffer).WaitForAnalysisEntryAsync(cancellationToken);
        }

        public Task WaitForAnalyzerAsync(ITextView textView, CancellationToken cancellationToken) {
            if (textView == null) {
                throw new ArgumentNullException(nameof(textView));
            }

            return WaitForAnalyzerAsync(textView.TextBuffer, cancellationToken);
        }

        public bool TryGetAnalyzer(ITextBuffer textBuffer, out ProjectAnalyzer analyzer, out string filename) {
            if (textBuffer == null) {
                throw new ArgumentNullException(nameof(textBuffer));
            }

            // If we have an analyzer in Properties, we will use that
            // NOTE: This should only be used for tests.
            if (textBuffer.Properties.TryGetProperty(VsProjectAnalyzer._testAnalyzer, out analyzer)) {
                if (!textBuffer.Properties.TryGetProperty(VsProjectAnalyzer._testFilename, out filename)) {
                    filename = textBuffer.GetFilePath();
                }
                return true;
            }

            // If we have a REPL evaluator we'll use its analyzer
            IPythonInteractiveIntellisense evaluator;
            if ((evaluator = textBuffer.GetInteractiveWindow()?.Evaluator as IPythonInteractiveIntellisense) != null) {
                analyzer = evaluator.Analyzer;
                filename = evaluator.AnalysisFilename;
                return analyzer != null;
            }

            AnalysisEntry entry;
            if (TryGetAnalysisEntry(textBuffer, out entry)) {
                analyzer = entry.Analyzer;
                filename = entry.Path;
                return true;
            }

            // If we find an associated project, use its analyzer
            // This should only happen while racing with text view creation
            var path = textBuffer.GetFilePath();
            if (path != null) {
                analyzer = _services.Site.GetProjectFromFile(path)?.GetAnalyzer();
                if (analyzer != null) {
                    Debug.WriteLine("Found an analyzer on " + path + " that wasn't in the property bag");
                    filename = path;
                    return true;
                }
            }

            analyzer = null;
            filename = null;
            return false;
        }

        public bool TryGetAnalyzer(ITextView textView, out ProjectAnalyzer analyzer, out string filename) {
            if (textView == null) {
                throw new ArgumentNullException(nameof(textView));
            }

            // Try to get the analyzer from the main text buffer
            if (TryGetAnalyzer(textView.TextBuffer, out analyzer, out filename)) {
                return true;
            }


            return false;
        }

        public IEnumerable<ProjectAnalyzer> GetAnalyzersForFile(string filename) {
            if (string.IsNullOrEmpty(filename)) {
                throw new ArgumentNullException(nameof(filename));
            }

            var seen = new HashSet<VsProjectAnalyzer>();

            // If we have an open document, return that
            var buffer = _services.Site.GetTextBufferFromOpenFile(filename);
            if (buffer != null) {
                var analyzer = GetVsAnalyzer(null, buffer);
                if (analyzer != null && seen.Add(analyzer)) {
                    yield return analyzer;
                }
            }

            // Yield all loaded projects containing the file
            var sln = (IVsSolution)_services.Site.GetService(typeof(SVsSolution));
            if (sln != null) {
                if (Path.IsPathRooted(filename)) {
                    foreach (var project in sln.EnumerateLoadedPythonProjects()) {
                        if (project.FindNodeByFullPath(filename) != null) {
                            var analyzer = project.GetAnalyzer();
                            if (analyzer != null && seen.Add(analyzer)) {
                                yield return analyzer;
                            }
                        }
                    }
                } else {
                    var withSlash = "\\" + filename;
                    foreach (var project in sln.EnumerateLoadedPythonProjects()) {
                        if (project.AllVisibleDescendants.Any(n => n.Url.Equals(filename, StringComparison.OrdinalIgnoreCase) ||
                            n.Url.EndsWith(withSlash, StringComparison.OrdinalIgnoreCase))) {
                            var analyzer = project.GetAnalyzer();
                            if (analyzer != null && seen.Add(analyzer)) {
                                yield return analyzer;
                            }
                        }
                    }
                }
            }

            // TODO: When we add non-project attached analyzers, return them here
        }

        #endregion
    }
}
