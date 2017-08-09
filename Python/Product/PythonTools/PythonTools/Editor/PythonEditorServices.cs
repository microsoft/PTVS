using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Editor {
    /// <summary>
    /// Provides centralized access to services used by the editor.
    /// </summary>
    /// <remarks>
    /// MEF services provided by VS should be imported eagerly using the
    /// [Import] attribute on a public field.
    /// 
    /// Traditional services should be lazily imported on first use, and
    /// uses should be audited to ensure they all occur on the UI thread.
    /// 
    /// Services provided by PTVS should be lazily loaded on first access.
    /// Otherwise, we may end up with circular imports in MEF composition.
    /// </remarks>
    [Export]
    sealed class PythonEditorServices {
        [ImportingConstructor]
        public PythonEditorServices([Import(typeof(SVsServiceProvider))] IServiceProvider site) {
            Site = site;
            ComponentModel = Site.GetComponentModel();
            _errorTaskProvider = new Lazy<ErrorTaskProvider>(CreateTaskProvider<ErrorTaskProvider>);
            _commentTaskProvider = new Lazy<CommentTaskProvider>(CreateTaskProvider<CommentTaskProvider>);
            _unresolvedImportSquiggleProvider = new Lazy<UnresolvedImportSquiggleProvider>(CreateImportSquiggleProvider);
            _analysisEntryService = new Lazy<AnalysisEntryService>(() => ComponentModel.GetService<AnalysisEntryService>());
        }

        public readonly IServiceProvider Site;

        #region PythonToolsService

        private PythonToolsService _python;

        internal void SetPythonToolsService(PythonToolsService service) {
            if (_python != null) {
                throw new InvalidOperationException("Multiple services created");
            }
            _python = service;
        }

        internal PythonToolsService TryGetPythonToolsService() {
            _python = Site.GetUIThread().Invoke(() => Site.GetPythonToolsService());
            return _python;
        }

        public PythonToolsService Python => _python ?? TryGetPythonToolsService();

        #endregion

        public PythonTextBufferInfo GetBufferInfo(ITextBuffer textBuffer) {
            return PythonTextBufferInfo.ForBuffer(this, textBuffer);
        }

        public IComponentModel ComponentModel { get; }
        [Import]
        public IClassificationTypeRegistryService ClassificationTypeRegistryService;
        [Import]
        public IContentTypeRegistryService ContentTypeRegistryService;

        private readonly Lazy<AnalysisEntryService> _analysisEntryService;
        public AnalysisEntryService AnalysisEntryService => _analysisEntryService.Value;

        #region Task Providers

        private readonly Lazy<ErrorTaskProvider> _errorTaskProvider;
        public ErrorTaskProvider ErrorTaskProvider => _errorTaskProvider.Value;
        public ErrorTaskProvider MaybeErrorTaskProvider => _errorTaskProvider.IsValueCreated ? _errorTaskProvider.Value : null;

        private readonly Lazy<CommentTaskProvider> _commentTaskProvider;
        public CommentTaskProvider CommentTaskProvider => _commentTaskProvider.Value;
        public CommentTaskProvider MaybeCommentTaskProvider => _commentTaskProvider.IsValueCreated ? _commentTaskProvider.Value : null;

        private readonly Lazy<UnresolvedImportSquiggleProvider> _unresolvedImportSquiggleProvider;
        public UnresolvedImportSquiggleProvider UnresolvedImportSquiggleProvider => _unresolvedImportSquiggleProvider.Value;
        public UnresolvedImportSquiggleProvider MaybeUnresolvedImportSquiggleProvider => _unresolvedImportSquiggleProvider.IsValueCreated ? _unresolvedImportSquiggleProvider.Value : null;

        private T CreateTaskProvider<T>() where T : class {
            if (VsProjectAnalyzer.SuppressTaskProvider) {
                return null;
            }
            return (T)Site.GetService(typeof(T));
        }

        private UnresolvedImportSquiggleProvider CreateImportSquiggleProvider() {
            if (VsProjectAnalyzer.SuppressTaskProvider) {
                return null;
            }
            var errorProvider = ErrorTaskProvider;
            if (errorProvider == null) {
                return null;
            }
            return new UnresolvedImportSquiggleProvider(Site, errorProvider);
        }

        #endregion
    }
}
