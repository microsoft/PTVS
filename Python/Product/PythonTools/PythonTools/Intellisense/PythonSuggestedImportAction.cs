using Microsoft.VisualStudio.Language.Intellisense;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Media;
using Microsoft.PythonTools.Analysis;
using System.Drawing;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using System.Diagnostics;

#if DEV14_OR_LATER
namespace Microsoft.PythonTools.Intellisense {
    class PythonSuggestedImportAction : ISuggestedAction, IComparable<PythonSuggestedImportAction> {
        private readonly PythonSuggestedActionsSource _source;
        private readonly string _name;
        private readonly string _fromModule;

        public PythonSuggestedImportAction(PythonSuggestedActionsSource source, ExportedMemberInfo import) {
            _source = source;
            _fromModule = import.FromName;
            _name = import.ImportName;
        }

        public IEnumerable<SuggestedActionSet> ActionSets {
            get {
                return Enumerable.Empty<SuggestedActionSet>();
            }
        }

        public string DisplayText {
            get {
                return MissingImportAnalysis.MakeImportCode(_fromModule, _name)
                    .Replace("_", "__");
            }
        }

        public string IconAutomationText {
            get {
                return null;
            }
        }

        public ImageSource IconSource {
            get {
                // TODO: Return icon from image catalog
                return null;
            }
        }

        public string InputGestureText {
            get {
                return null;
            }
        }

        public void Dispose() { }

        public object GetPreview(CancellationToken cancellationToken) {
            return null;
        }

        public void Invoke(CancellationToken cancellationToken) {
            Debug.Assert(!string.IsNullOrEmpty(_name));

            MissingImportAnalysis.AddImport(
                _source._provider,
                _source._textBuffer,
                _source._view,
                _fromModule,
                _name
            );
        }

        public bool TryGetTelemetryId(out Guid telemetryId) {
            telemetryId = Guid.Empty;
            return false;
        }

        public int CompareTo(PythonSuggestedImportAction other) {
            if (other == null) {
                return -1;
            }

            // Sort from ... import before import ...
            if (!string.IsNullOrEmpty(_fromModule)) {
                if (string.IsNullOrEmpty(other._fromModule)) {
                    return -1;
                }
            } else if (!string.IsNullOrEmpty(other._fromModule)) {
                return 1;
            }

            var key1 = _fromModule ?? _name ?? "";
            var key2 = other._fromModule ?? other._name ?? "";

            // Name with fewer dots sorts first
            var dotCount1 = key1.Count(c => c == '.');
            var dotCount2 = key2.Count(c => c == '.');
            int r = dotCount1.CompareTo(dotCount2);
            if (r != 0) {
                return r;
            }

            // Shorter name sorts first
            r = key1.Length.CompareTo(key2.Length);
            if (r != 0) {
                return r;
            }

            // Keys sort alphabetically
            r = key1.CompareTo(key2);
            if (r != 0) {
                return r;
            }

            // Sort by display text
            return (DisplayText ?? "").CompareTo(other.DisplayText ?? "");
        }
    }
}
#endif
