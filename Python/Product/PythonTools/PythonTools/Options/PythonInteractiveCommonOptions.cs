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

using Microsoft.PythonTools.Repl;

namespace Microsoft.PythonTools.Options {
    /// <summary>
    /// Stores options related to the all interactive windows.
    /// </summary>
    class PythonInteractiveCommonOptions {
        private bool _smartHistory, _interpreterPrompts, _inlinePrompts, _liveCompletionsOnly;
        private ReplIntellisenseMode _replIntellisenseMode;
        private string _priPrompt, _secPrompt;

        public string PrimaryPrompt {
            get { return _priPrompt; }
            set { _priPrompt = value; }
        }

        public string SecondaryPrompt {
            get { return _secPrompt; }
            set { _secPrompt = value; }
        }

        public bool UseInterpreterPrompts {
            get { return _interpreterPrompts; }
            set { _interpreterPrompts = value; }
        }

        public bool InlinePrompts {
            get { return _inlinePrompts; }
            set { _inlinePrompts = value; }
        }

        internal ReplIntellisenseMode ReplIntellisenseMode {
            get { return _replIntellisenseMode; }
            set { _replIntellisenseMode = value; }
        }

        public bool ReplSmartHistory {
            get { return _smartHistory; }
            set { _smartHistory = value; }
        }

        public bool LiveCompletionsOnly {
            get { return _liveCompletionsOnly; }
            set { _liveCompletionsOnly = value; }
        }
    }
}
