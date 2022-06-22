using System;
using System.Threading.Tasks;

namespace Microsoft.CookiecutterTools.Model {
    [Serializable]
    class GitRunException : Exception{
        public GitRunException() { }

        public GitRunException(string message) : base(message) { }
    }
}
