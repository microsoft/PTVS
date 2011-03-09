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

using System.ComponentModel.Composition;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.VisualStudio.Repl;

namespace Microsoft.VisualStudio.Repl {
    [Export(typeof(IReplCommand))]
    class ResetReplCommand : IReplCommand {
        #region IReplCommand Members

        public void Execute(IReplWindow window, string arguments) {
            window.Reset();
        }

        public string Description {
            get { return "Reset to an empty execution engine, but keep REPL history"; }
        }

        public string Command {
            get { return "reset"; }
        }

        public object ButtonContent {
            get {
                var image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = Assembly.GetExecutingAssembly().GetManifestResourceStream("Microsoft.VisualStudio.Resources.ResetSession.gif");
                image.EndInit();

                var res = new Image();
                res.Source = image;
                res.Opacity = 1.0;
                res.Width = res.Height = 16;
                return res;
            }
        }

        #endregion
    }
}
