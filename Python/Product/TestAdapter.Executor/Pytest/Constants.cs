// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using System;

namespace Microsoft.PythonTools.TestAdapter.Pytest {
    internal static class Constants {

        internal static readonly TestProperty PytestIdProperty = TestProperty.Register("PytestId", "PytestId", typeof(string), TestPropertyAttributes.Hidden, typeof(TestCase));
        internal static readonly TestProperty PyTestXmlClassNameProperty = TestProperty.Register("PytestXmlClassName", "PytestXmlClassName", typeof(string), TestPropertyAttributes.Hidden, typeof(TestCase));
        internal static readonly TestProperty PytestFileProperty = TestProperty.Register("PytestFile", "PytestFile", typeof(string), TestPropertyAttributes.Hidden, typeof(TestCase));
        internal static readonly TestProperty PytestTestExecutionPathPropertery = TestProperty.Register("PytestTestExecPath", "PytestTestExecPath", typeof(string), TestPropertyAttributes.Hidden, typeof(TestCase));

        internal static readonly Uri PytestUri = new Uri(PythonConstants.TestExecutorUriString);
    }
}
