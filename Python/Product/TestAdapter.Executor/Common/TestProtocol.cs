// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using Microsoft.PythonTools.Ipc.Json;

namespace Microsoft.PythonTools.TestAdapter {
    static class TestProtocol {
        public static readonly Dictionary<string, Type> RegisteredTypes = CollectCommands();

        private static Dictionary<string, Type> CollectCommands() {
            Dictionary<string, Type> all = new Dictionary<string, Type>();
            foreach (var type in typeof(TestProtocol).GetNestedTypes()) {
                if (type.IsSubclassOf(typeof(Request))) {
                    var command = type.GetField("Command");
                    if (command != null) {
                        all["request." + (string)command.GetRawConstantValue()] = type;
                    }
                } else if (type.IsSubclassOf(typeof(Event))) {
                    var name = type.GetField("Name");
                    if (name != null) {
                        all["event." + (string)name.GetRawConstantValue()] = type;
                    }
                }
            }
            return all;
        }

#pragma warning disable 0649

        public class StdOutEvent : Event {
            public const string Name = "stdout";
            public string content;

            public override string name => Name;
        }

        public class StdErrEvent : Event {
            public const string Name = "stderr";
            public string content;

            public override string name => Name;
        }

        public class StartEvent : Event {
            public const string Name = "start";
            public string test, file, method, classname;

            public override string name => Name;
        }

        public class ResultEvent : Event {
            public const string Name = "result";
            public string test, outcome, traceback, message;
            public double durationInSecs;

            public override string name => Name;
        }

        public class DoneEvent : Event {
            public const string Name = "done";

            public override string name => Name;
        }

#pragma warning restore 0649
    }
}
