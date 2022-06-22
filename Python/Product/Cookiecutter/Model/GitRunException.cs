using System;
using System.Threading.Tasks;

namespace Microsoft.CookiecutterTools.Model {
    [Serializable]
    class GitRunException : Exception{
        public GitRunException() { }

        public GitRunException(string message) : base(message) { }

        public GitRunException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
