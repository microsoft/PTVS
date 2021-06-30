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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.InteractiveWindow.Commands;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;

namespace TestUtilities.Python {
    class MockInteractiveWindowCommandsFactory : IInteractiveWindowCommandsFactory {
        public IInteractiveWindowCommands CreateInteractiveCommands(
            IInteractiveWindow window,
            string prefix,
            IEnumerable<IInteractiveWindowCommand> commands
        ) {
            return window.Properties.GetOrCreateSingletonProperty(() => new MockInteractiveWindowCommands(
                window,
                prefix,
                commands
            ));
        }
    }

    class MockInteractiveWindowCommands : IInteractiveWindowCommands {
        private readonly List<IInteractiveWindowCommand> _commands;
        private readonly IInteractiveWindow _window;

        public MockInteractiveWindowCommands(
            IInteractiveWindow window,
            string prefix,
            IEnumerable<IInteractiveWindowCommand> commands
        ) {
            CommandPrefix = prefix;
            _window = window;
            _commands = commands.ToList();
        }

        public IInteractiveWindowCommand this[string name] {
            get {
                return _commands.FirstOrDefault(c => c.Names.Contains(name));
            }
        }

        public string CommandPrefix { get; private set; }

        public bool InCommand {
            get;
            private set;
        }

        public IEnumerable<ClassificationSpan> Classify(SnapshotSpan span) {
            throw new NotImplementedException();
        }

        public void DisplayCommandHelp(IInteractiveWindowCommand command) {
            throw new NotImplementedException();
        }

        public void DisplayCommandUsage(IInteractiveWindowCommand command, TextWriter writer, bool displayDetails) {
            throw new NotImplementedException();
        }

        public void DisplayHelp() {
            throw new NotImplementedException();
        }

        public IEnumerable<IInteractiveWindowCommand> GetCommands() {
            return _commands;
        }

        public Task<ExecutionResult> TryExecuteCommand() {
            var input = _window.CurrentLanguageBuffer.CurrentSnapshot.GetText();
            Console.WriteLine("Executing {0} in REPL", input);
            var args = input.Split(new[] { ' ' }, 2);
            var command = this[args[0]];
            InCommand = true;
            var res = command == null ? null : command.Execute(_window, args[1]);
            InCommand = false;
            return res;
        }
    }
}
