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
using System.Collections.ObjectModel;
using System.Text;
using Microsoft.PythonTools.Analysis;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Intellisense {
    internal class PythonSignature : ISignature {
        private readonly ITrackingSpan _span;
        private readonly string _content;
        private readonly ReadOnlyCollection<IParameter> _parameters;
        private IParameter _currentParameter;
        private readonly IOverloadResult _overload;

        public PythonSignature(ITrackingSpan span, IOverloadResult overload, int paramIndex) {
            _span = span;
            _overload = overload;

            var content = new StringBuilder(overload.Name);
            content.Append('(');
            int start = content.Length;
            var parameters = new IParameter[overload.Parameters.Length];
            for (int i = 0; i < overload.Parameters.Length; i++) {
                var param = overload.Parameters[i];
                if (param.IsOptional) {
                    content.Append("[");
                }
                if (i > 0) {
                    content.Append(", ");
                    start = content.Length;
                }

                content.Append(param.Name);
                if (param.Type != null && param.Type != "object") {
                    content.Append(": ");
                    content.Append(param.Type);
                }

                var paramSpan = new Span(start, content.Length - start);

                if (param.IsOptional) {
                    content.Append("]");
                }

                parameters[i] = new PythonParameter(this, param, paramSpan);
            }
            content.Append(')');
            _content = content.ToString();

            _parameters = new ReadOnlyCollection<IParameter>(parameters);
            if (paramIndex < parameters.Length) {
                _currentParameter = parameters[paramIndex];
            } else {
                _currentParameter = null;
            }
        }

        internal void SetCurrentParameter(IParameter newValue) {
            if (newValue != _currentParameter) {
                var args = new CurrentParameterChangedEventArgs(_currentParameter, newValue);
                _currentParameter = newValue;
                var changed = CurrentParameterChanged;
                if (changed != null) {
                    changed(this, args);
                }
            }
        }

        public ITrackingSpan ApplicableToSpan {
            get { return _span; }
        }

        public string Content {
            get { return _content; }
        }

        public IParameter CurrentParameter {
            get { return _currentParameter; }
        }

        public event EventHandler<CurrentParameterChangedEventArgs> CurrentParameterChanged;

        public string Documentation {
            get { return _overload.Documentation; }
        }

        public ReadOnlyCollection<IParameter> Parameters {
            get { return _parameters; }
        }

        #region ISignature Members


        public string PrettyPrintedContent {
            get { return Content; }
        }

        #endregion
    }
}
