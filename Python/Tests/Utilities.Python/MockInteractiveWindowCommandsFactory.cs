/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

#if DEV14_OR_LATER

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

#endif