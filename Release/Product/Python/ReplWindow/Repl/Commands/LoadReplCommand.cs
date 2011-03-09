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

using System;
using System.ComponentModel.Composition;
using System.IO;
using Microsoft.VisualStudio.Repl;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;

namespace Microsoft.VisualStudio.Repl {
    [Export(typeof(IReplCommand))]
    class LoadReplCommand : IReplCommand {
        #region IReplCommand Members

        public void Execute(IReplWindow window, string arguments) {
            using (var stream = new StreamReader(arguments)) {
                string line;
                while ((line = stream.ReadLine()) != null) {
                    if (!line.StartsWith("%%")) {
                        window.PasteText(line);
                    }
                }
            }
            window.PasteText(window.TextView.Options.GetNewLineCharacter());
        }

        public string Description {
            get { return "Loads commands from file and executes until complete"; }
        }

        public string Command {
            get { return "load"; }
        }

        public object ButtonContent {
            get {
                return null;
            }
        }

        #endregion
    }
}
