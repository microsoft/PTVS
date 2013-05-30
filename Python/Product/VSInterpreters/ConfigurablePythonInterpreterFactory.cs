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

namespace Microsoft.PythonTools.Interpreter {
    class ConfigurablePythonInterpreterFactory : IPythonInterpreterFactory, IInterpreterWithCompletionDatabase {
        private readonly IPythonInterpreterFactory _realFactory;

        public ConfigurablePythonInterpreterFactory(IPythonInterpreterFactory realFactory) {
            _realFactory = realFactory;
        }

        #region IPythonInterpreterFactory Members

        public string Description {
            get { return _realFactory.Description; }
        }

        public InterpreterConfiguration Configuration {
            get { return _realFactory.Configuration; }
        }

        public Guid Id {
            get { return _realFactory.Id; }
        }

        public IPythonInterpreter CreateInterpreter() {
            return _realFactory.CreateInterpreter();
        }

        #endregion

        #region IInterpreterWithCompletionDatabase Members

        public void GenerateCompletionDatabase(Action failedToStart = null) {
            ((IInterpreterWithCompletionDatabase)_realFactory).GenerateCompletionDatabase(failedToStart);
        }

        public void AutoGenerateCompletionDatabase() {
            ((IInterpreterWithCompletionDatabase)_realFactory).AutoGenerateCompletionDatabase();
        }

        public bool IsCurrent {
            get { return ((IInterpreterWithCompletionDatabase)_realFactory).IsCurrent; }
        }

        public void RefreshIsCurrent() {
            ((IInterpreterWithCompletionDatabase)_realFactory).RefreshIsCurrent();
        }

        public void NotifyInvalidDatabase() {
            ((IInterpreterWithCompletionDatabase)_realFactory).NotifyInvalidDatabase();
        }

        public void NotifyGeneratingDatabase(bool isGenerating) {
            ((IInterpreterWithCompletionDatabase)_realFactory).NotifyGeneratingDatabase(isGenerating);
        }

        public void NotifyNewDatabase() {
            ((IInterpreterWithCompletionDatabase)_realFactory).NotifyNewDatabase();
        }

        public event EventHandler<NewDatabaseEventArgs> NewDatabase {
            add {
                ((IInterpreterWithCompletionDatabase)_realFactory).NewDatabase += value;
            }
            remove {
                ((IInterpreterWithCompletionDatabase)_realFactory).NewDatabase -= value;
            }
        }

        public string GetAnalysisLogContent(IFormatProvider culture) {
            return ((IInterpreterWithCompletionDatabase)_realFactory).GetAnalysisLogContent(culture);
        }

        public event EventHandler IsCurrentChanged {
            add {
                ((IInterpreterWithCompletionDatabase)_realFactory).IsCurrentChanged += value;
            }
            remove {
                ((IInterpreterWithCompletionDatabase)_realFactory).IsCurrentChanged -= value;
            }
        }

        public event EventHandler IsCurrentReasonChanged {
            add {
                ((IInterpreterWithCompletionDatabase)_realFactory).IsCurrentReasonChanged += value;
            }
            remove {
                ((IInterpreterWithCompletionDatabase)_realFactory).IsCurrentReasonChanged -= value;
            }
        }

        public string GetFriendlyIsCurrentReason(IFormatProvider culture) {
            return ((IInterpreterWithCompletionDatabase)_realFactory).GetFriendlyIsCurrentReason(culture);
        }

        public string GetIsCurrentReason(IFormatProvider culture) {
            return ((IInterpreterWithCompletionDatabase)_realFactory).GetIsCurrentReason(culture);
        }

        #endregion

    }
}
