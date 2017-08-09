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

namespace Microsoft.PythonTools.Editor {
    [Export]
    sealed class PythonEditorServices {
        [ImportingConstructor]
        public PythonEditorServices([Import(typeof(SVsServiceProvider))] IServiceProvider site) {
            Site = site;
            ComponentModel = Site.GetComponentModel();
            Python = Site.GetPythonToolsService();
            _errorTaskProvider = new Lazy<ErrorTaskProvider>(() => (ErrorTaskProvider)Site.GetService(typeof(ErrorTaskProvider)));
            _commentTaskProvider = new Lazy<CommentTaskProvider>(() => (CommentTaskProvider)Site.GetService(typeof(CommentTaskProvider)));
        }

        public readonly IServiceProvider Site;

        public readonly PythonToolsService Python;

        public PythonTextBufferInfo GetBufferInfo(ITextBuffer textBuffer) {
            return PythonTextBufferInfo.ForBuffer(this, textBuffer);
        }

        public IComponentModel ComponentModel { get; }
        [Import]
        public IClassificationTypeRegistryService ClassificationTypeRegistryService;
        [Import]
        public IContentTypeRegistryService ContentTypeRegistryService;
        [Import]
        public AnalysisEntryService AnalysisEntryService;

        private readonly Lazy<ErrorTaskProvider> _errorTaskProvider;
        private readonly Lazy<CommentTaskProvider> _commentTaskProvider;
        public ErrorTaskProvider ErrorTaskProvider => _errorTaskProvider.Value;

        public CommentTaskProvider CommentTaskProvider => _commentTaskProvider.Value;
    }
}
