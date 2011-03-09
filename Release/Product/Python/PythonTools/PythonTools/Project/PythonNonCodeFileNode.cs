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
using Microsoft.PythonTools.Project;

using Microsoft.Windows.Design.Host;
using Microsoft.PythonTools.Designer;

namespace Microsoft.PythonTools.Project {
    class PythonNonCodeFileNode : CommonNonCodeFileNode {
        private DesignerContext _designerContext;

        public PythonNonCodeFileNode(CommonProjectNode root, ProjectElement e)
            : base(root, e) {
        }

        protected internal Microsoft.Windows.Design.Host.DesignerContext DesignerContext {
            get {
                if (_designerContext == null) {
                    _designerContext = new DesignerContext();
                    //Set the EventBindingProvider for this XAML file so the designer will call it
                    //when event handlers need to be generated
                    _designerContext.EventBindingProvider = new WpfEventBindingProvider(this.Parent.FindChild(this.Url.Replace(".xaml", ".py")) as PythonFileNode);
                }
                return _designerContext;
            }
        }

        protected override object CreateServices(Type serviceType) {
            object service = null;
            if (typeof(DesignerContext) == serviceType) {
                service = this.DesignerContext;
            } else {
                return base.CreateServices(serviceType);
            }
            return service;
        }


    }
}
