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

using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Intellisense {
    internal class SignatureHelpSource : ISignatureHelpSource {
        private readonly ITextBuffer _textBuffer;
        private readonly SignatureHelpSourceProvider _provider;

        public SignatureHelpSource(SignatureHelpSourceProvider provider, ITextBuffer textBuffer) {
            _textBuffer = textBuffer;
            _provider = provider;
        }

        public ISignature GetBestMatch(ISignatureHelpSession session) {
            return null;
        }

        public void AugmentSignatureHelpSession(ISignatureHelpSession session, System.Collections.Generic.IList<ISignature> signatures) {
            var span = session.GetApplicableSpan(_textBuffer);
            
            var sigs = _textBuffer.CurrentSnapshot.GetSignatures(span);

            ISignature curSig = null;
            
            foreach (var sig in sigs.Signatures) {
                if (sigs.ParameterIndex == 0 || sig.Parameters.Count > sigs.ParameterIndex) {
                    curSig = sig;
                    break;
                }
            }
            
            foreach (var sig in sigs.Signatures) {
                signatures.Add(sig);
            }

            if (curSig != null) {
                // save the current sig so we don't need to recalculate it (we can't set it until
                // the signatures are added by our caller).
                session.Properties.AddProperty(typeof(PythonSignature), curSig);
            }
        }

        public void Dispose() {
        }
    }
}
