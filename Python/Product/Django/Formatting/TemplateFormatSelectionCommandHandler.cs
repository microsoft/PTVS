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

#if DEV12_OR_LATER

using System;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.Web.Editor;

#if DEV14_OR_LATER
using Microsoft.VisualStudio;
using Microsoft.Web.Editor.Controller.Command;
#endif

namespace Microsoft.PythonTools.Django.Formatting {
    internal class TemplateFormatSelectionCommandHandler : EditingCommand {
        public TemplateFormatSelectionCommandHandler(ITextView textView)
            : base(textView, new CommandId(VSConstants.VSStd2K, (int)VSConstants.VSStd2KCmdID.FORMATSELECTION)) {
        }

        public override CommandStatus Status(Guid group, int id) {
            return CommandStatus.NotSupported;
        }

        public override CommandResult Invoke(Guid group, int id, object inputArg, ref object outputArg) {
            return CommandResult.NotSupported;
        }
    }
}

#endif
