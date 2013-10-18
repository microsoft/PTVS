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
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense;
using TestUtilities.SharedProject;

namespace TestUtilities.UI {
    public static class TestExtensions {
        public static VisualStudioSolution ToVs(this SolutionFile self) {
            return new VisualStudioSolution(self);
        }

        public static string[] GetDisplayTexts(this ICompletionSession completionSession) {
            return completionSession.CompletionSets.First().Completions.Select(x => x.DisplayText).ToArray();
        }

        public static string[] GetInsertionTexts(this ICompletionSession completionSession) {
            return completionSession.CompletionSets.First().Completions.Select(x => x.InsertionText).ToArray();
        }

    }
}
