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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Repl;
using System.ComponentModel.Composition;
using System.IO;
using System.Windows.Threading;

namespace Microsoft.VisualStudio.Repl {
    [Export(typeof(IReplCommand))]
    class WaitReplCommand : IReplCommand {
        #region IReplCommand Members

        public void Execute(IReplWindow window, string arguments) {
            var delay = new TimeSpan(0, 0, 0, 0, int.Parse(arguments));
            var start = DateTime.UtcNow;
            while ((start + delay) > DateTime.UtcNow) {
                var frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(
                    DispatcherPriority.Background,
                    new Action<DispatcherFrame>(f => f.Continue = false),
                    frame
                    );
                Dispatcher.PushFrame(frame);
            }
        }

        public string Description {
            get { return "Wait for at least the specified number of milliseconds"; }
        }

        public string Command {
            get { return "wait"; }
        }

        public object ButtonContent {
            get {
                return null;
            }
        }

        #endregion
    }
}
