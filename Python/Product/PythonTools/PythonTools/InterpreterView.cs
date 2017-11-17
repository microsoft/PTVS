// Python Tools for Visual Studio
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
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Project;

namespace Microsoft.PythonTools {
    internal class InterpreterView : DependencyObject {
        public static readonly IEqualityComparer<InterpreterView> EqualityComparer = new InterpreterViewComparer();
        public static readonly IComparer<InterpreterView> Comparer = (IComparer<InterpreterView>)EqualityComparer;

        public static IEnumerable<InterpreterView> GetInterpreters(
            IServiceProvider serviceProvider,
            PythonProjectNode project, 
            bool onlyGlobalEnvironments = false
        ) {
            var knownProviders = serviceProvider.GetComponentModel().GetService<IInterpreterRegistryService>();

            var res = knownProviders.Configurations
                .Where(PythonInterpreterFactoryExtensions.IsUIVisible)
                .Where(PythonInterpreterFactoryExtensions.IsRunnable)
                .OrderBy(c => c.Description)
                .ThenBy(c => c.Version)
                .Select(c => new InterpreterView(c.Id, c.Description, project));

            if (onlyGlobalEnvironments) {
                res = res.Where(v => string.IsNullOrEmpty(knownProviders.GetProperty(v.Id, "ProjectMoniker") as string));
            }

            if (project != null) {
                res = res.Concat(project.InvalidInterpreterIds
                    .Select(i => new InterpreterView(i, FormatInvalidId(i), project))
                    .OrderBy(v => v.Name));
            }

            return res;
        }

        private static string FormatInvalidId(string id) {
            string company, tag;
            if (CPythonInterpreterFactoryConstants.TryParseInterpreterId(id, out company, out tag)) {
                if (company == PythonRegistrySearch.PythonCoreCompany) {
                    company = "Python";
                }
                return "{0} {1}".FormatUI(company, tag);
            }
            return id;
        }

        public InterpreterView(string id, string name, PythonProjectNode project) {
            Id = id;
            Name = name;
            Project = project;
        }

        public override string ToString() => Name;

        public string Id { get; }
        public PythonProjectNode Project { get; }

        public string Name {
            get { return (string)SafeGetValue(NameProperty); }
            private set { SafeSetValue(NamePropertyKey, value); }
        }

        public bool IsSelected {
            get { return (bool)SafeGetValue(IsSelectedProperty); }
            set { SetValue(IsSelectedProperty, value); }
        }


        private object SafeGetValue(DependencyProperty property) {
            if (Dispatcher.CheckAccess()) {
                return GetValue(property);
            } else {
                return Dispatcher.Invoke((Func<object>)(() => GetValue(property)));
            }
        }

        private void SafeSetValue(DependencyPropertyKey property, object value) {
            if (Dispatcher.CheckAccess()) {
                SetValue(property, value);
            } else {
                Dispatcher.BeginInvoke((Action)(() => SetValue(property, value)));
            }
        }

        private static readonly DependencyPropertyKey NamePropertyKey = DependencyProperty.RegisterReadOnly("Name", typeof(string), typeof(InterpreterView), new PropertyMetadata());
        public static readonly DependencyProperty NameProperty = NamePropertyKey.DependencyProperty;

        public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register("IsSelected", typeof(bool), typeof(InterpreterView), new PropertyMetadata(false));

        private sealed class InterpreterViewComparer : IEqualityComparer<InterpreterView>, IComparer<InterpreterView> {
            public bool Equals(InterpreterView x, InterpreterView y) {
                return x?.Id == y?.Id;
            }

            public int GetHashCode(InterpreterView obj) {
                return obj.Id.GetHashCode();
            }

            public int Compare(InterpreterView x, InterpreterView y) {
                return StringComparer.CurrentCultureIgnoreCase.Compare(
                    x?.Name ?? "",
                    y?.Name ?? ""
                );
            }
        }
    }
}
