// Visual Studio Shared Project
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.VisualStudioTools.Project.Automation {
    /// <summary>
    /// This object defines a so called null object that is returned as instead of null. This is because callers in VSCore usually crash if a null propery is returned for them.
    /// </summary>
    [ComVisible(true)]
    public class OANullProperty : EnvDTE.Property {
        #region fields
        private OAProperties parent;
        #endregion

        #region ctors

        public OANullProperty(OAProperties parent) {
            this.parent = parent;
        }
        #endregion

        #region EnvDTE.Property

        public object Application {
            get { return String.Empty; }
        }

        public EnvDTE.Properties Collection {
            get {
                //todo: EnvDTE.Property.Collection
                return this.parent;
            }
        }

        public EnvDTE.DTE DTE {
            get { return null; }
        }

        public object get_IndexedValue(object index1, object index2, object index3, object index4) {
            return String.Empty;
        }

        public void let_Value(object value) {
            //todo: let_Value
        }

        public string Name {
            get { return String.Empty; }
        }

        public short NumIndices {
            get { return 0; }
        }

        public object Object {
            get { return this.parent.Target; }
            set {
            }
        }

        public EnvDTE.Properties Parent {
            get { return this.parent; }
        }

        public void set_IndexedValue(object index1, object index2, object index3, object index4, object value) {

        }

        public object Value {
            get { return String.Empty; }
            set { }
        }
        #endregion
    }
}
