using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Intellisense {
    public sealed class ProjectFileInfo {
        public readonly int _fileId;
        public readonly string _path;
        public readonly VsProjectAnalyzer ProjectState;
        private readonly Dictionary<int, ITextSnapshot> _parsedSnapshots = new Dictionary<int, ITextSnapshot>();
        public ITextSnapshot _lastSentSnapshot, _lastParsedSnapshot;
        public IIntellisenseCookie AnalysisCookie;

        private readonly Dictionary<object, object> _properties = new Dictionary<object, object>();
        internal BufferParser BufferParser;

        public ProjectFileInfo(VsProjectAnalyzer analyzer, string path, int fileId) {
            ProjectState = analyzer;
            _path = path;
            _fileId = fileId;
        }

        public event EventHandler OnNewAnalysis;

        public string FilePath => _path;
        public int FileId => _fileId;

        internal void RaiseOnNewAnalysis() {
            OnNewAnalysis?.Invoke(this, EventArgs.Empty);
        }

        internal void BeginParsingTree() {
        }

        internal void UpdateTree(object p1, object p2) {
        }

        internal ITextSnapshot LastSentSnapshot {
            get {
                return _lastSentSnapshot;
            }
            set {
                _lastSentSnapshot = value;
            }
        }

        internal ITextSnapshot LastParsedSnapshot {
            get {
                return _lastParsedSnapshot;
            }
            set {
                _lastParsedSnapshot = value;
            }
        }

        public Dictionary<object, object> Properties => _properties;

        public bool IsAnalyzed { get; internal set; }

        public IEnumerable<MemberResult> GetAllAvailableMembers(SourceLocation location, GetMemberOptions options) {
            return ProjectState.GetAllAvailableMembers(this, location, options);
        }

        public IEnumerable<MemberResult> GetMembers(string text, SourceLocation location, GetMemberOptions options) {
            return ProjectState.GetMembers(this, text, location, options);
        }

        public IEnumerable<MemberResult> GetModuleMembers(string[] package, bool v) {
            return ProjectState.GetModuleMembers(this, package, v);
        }

        public IEnumerable<MemberResult> GetModules(bool v) {
            return ProjectState.GetModules(this, v);
        }

        internal IEnumerable<IAnalysisVariable> GetVariables(string expr, SourceLocation translatedLocation) {
            return ProjectState.GetVariables(this, expr, translatedLocation);
        }

        internal IEnumerable<AnalysisValue> GetValues(string expr, SourceLocation translatedLocation) {
            return ProjectState.GetValues(this, expr, translatedLocation);
        }

        internal PythonAst GetAstFromText(string _expr, SourceLocation translatedLocation) {
            throw new NotImplementedException();
        }

        internal void PushSnapshot(ITextSnapshot snapshot) {
            lock(_parsedSnapshots) {
                _parsedSnapshots[snapshot.Version.VersionNumber] = snapshot;
            }
        }
        internal ITextSnapshot PopSnapshot(int version) {
            lock (_parsedSnapshots) {
                var res = _parsedSnapshots[version];
                _parsedSnapshots.Remove(version);
                return res;
            }
        }

        internal string GetLine(int line) {
            return AnalysisCookie.GetLine(line);
        }

        internal event EventHandler OnNewParseTree;

        internal void RaiseOnNewParseTree() {
            OnNewParseTree?.Invoke(this, EventArgs.Empty);
        }
    }
}
