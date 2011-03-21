using System;
using System.IO;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.PythonTools {
    class ProvideFileFilterAttribute : RegistrationAttribute {
        private readonly string _id, _name, _filter;
        private readonly int _sortPriority;
        private readonly bool _commonOpenFilesFilter;

        public ProvideFileFilterAttribute(string projectGuid, string name, string filter, int sortPriority, bool commonOpenFilesFilter = false) {
            _name = name;
            _id = projectGuid;
            _filter = filter;
            _sortPriority = sortPriority;
            _commonOpenFilesFilter = commonOpenFilesFilter;
        }

        public override void Register(RegistrationContext context) {
            using (var engineKey = context.CreateKey("Projects\\" + _id + "\\Filters\\" + _name)) {
                engineKey.SetValue("", _filter);
                engineKey.SetValue("SortPriority", _sortPriority);
                if (_commonOpenFilesFilter) {
                    engineKey.SetValue("CommonOpenFilesFilter", 1);
                }
            }
        }

        public override void Unregister(RegistrationContext context) {
        }
    }
}
