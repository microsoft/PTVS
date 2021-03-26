using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.Client;
using Newtonsoft.Json.Linq;

namespace Microsoft.PythonTools.LanguageServerClient {
    class MiddleLayer : ILanguageClientMiddleLayer {
        public bool CanHandle(string methodName) {
            return true;
        }
        public Task HandleNotificationAsync(string methodName, JToken methodParam, Func<JToken, Task> sendNotification) {
            System.Diagnostics.Debug.WriteLine($"*** HandleNotificationAsync for {methodName}");
            return sendNotification(methodParam);
        }
        public Task<JToken> HandleRequestAsync(string methodName, JToken methodParam, Func<JToken, Task<JToken>> sendRequest) {
            System.Diagnostics.Debug.WriteLine($"*** HandleRequestAsync for {methodName}");
            return sendRequest(methodParam);
        }
    }
}
