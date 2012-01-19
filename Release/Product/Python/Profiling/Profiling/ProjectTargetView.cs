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

namespace Microsoft.PythonTools.Profiling {
    /// <summary>
    /// Provides a view model for the ProjectTarget class.
    /// </summary>
    public class ProjectTargetView {
        readonly string _name;
        readonly Guid _guid;
        
        /// <summary>
        /// Create a ProjectTargetView with values from an EnvDTE.Project.
        /// </summary>
        public ProjectTargetView(EnvDTE.Project project) {
            _name = project.Name;
            _guid = new Guid((string)project.Properties.Item("Guid").Value);
        }

        /// <summary>
        /// Create a ProjectTargetView with values from a ProjectTarget.
        /// </summary>
        public ProjectTargetView(ProjectTarget project) {
            _name = project.FriendlyName;
            _guid = project.TargetProject;
        }

        /// <summary>
        /// Returns a ProjectTarget created with the values from the view model.
        /// </summary>
        public ProjectTarget GetTarget() {
            return new ProjectTarget {
                FriendlyName = _name,
                TargetProject = _guid
            };
        }

        /// <summary>
        /// The display name of the project.
        /// </summary>
        public string Name {
            get {
                return _name;
            }
        }

        /// <summary>
        /// The Guid identifying the project.
        /// </summary>
        public Guid Guid {
            get {
                return _guid;
            }
        }

        public override string ToString() {
            return Name;
        }

        public override bool Equals(object obj) {
            var other = obj as ProjectTargetView;
            if (other == null) {
                return false;
            } else {
                return Guid.Equals(other.Guid);
            }
        }

        public override int GetHashCode() {
            return Guid.GetHashCode();
        }
    }
}

 
