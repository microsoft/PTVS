using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestRunnerInterop {
    class TestFailedException : Exception {
        private readonly string _innerType;
        private readonly string _stackTrace;

        public TestFailedException(string innerType, string message, string stackTrace) {
            _innerType = innerType;
            Message = message;
            _stackTrace = stackTrace;
        }

        public override string ToString() {
            return base.ToString().Replace(GetType().FullName, _innerType);
        }

        public override string Message { get; }

        public override string StackTrace => _stackTrace ?? base.StackTrace;
    }
}
