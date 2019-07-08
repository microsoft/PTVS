using System;

namespace Microsoft.PythonTools.Projects {

    /// <summary>
    /// Argument of the event raised when a project property is changed.
    /// </summary>
    public class PythonProjectPropertyChangedArgs : EventArgs {
        private string propertyName;
        private string oldValue;
        private string newValue;
        internal PythonProjectPropertyChangedArgs(string propertyName, string oldValue, string newValue) {
            this.propertyName = propertyName;
            this.oldValue = oldValue;
            this.newValue = newValue;
        }

        public string NewValue {
            get { return newValue; }
        }

        public string OldValue {
            get { return oldValue; }
        }

        public string PropertyName {
            get { return propertyName; }
        }
    }
}
