// Python Tools for Visual Studio
// Copyright(c) 2018 Intel Corporation.  All rights reserved.
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

namespace ExternalProfilerDriver
{
    public static class Utils
    {
        // from an idea in https://github.com/dotnet/corefx/issues/3093
        public static IEnumerable<T> Emit<T>(T element)
        {
          return Enumerable.Repeat(element, 1);
        }
    }

    /// <summary>
    /// A `Maybe` monad implementation based largely on the one(s) in the
    /// Azure IoT Edge project.
    /// </summary>
    public struct Option<T> : IEquatable< Option<T> >
    {
        public bool HasValue { get; }
               T    Value    { get; }
        
        /// <summary>
        /// This may seem a bit weird for an `Option` constructor, the
        /// implementation only recognizes the `Value` as valid if `HasValue` has
        /// been set to `true`.
        /// </summary>        
        internal Option(T _value, bool _hasValue)
        {
            this.Value = _value;
            this.HasValue = _hasValue;
        }
        
        public bool Equals(Option<T> other)
        {
            // there is only one `none`:
            if (!this.HasValue && !other.HasValue) { return true; }
            if (this.HasValue && other.HasValue) {
                return EqualityComparer<T>.Default.Equals( this.Value, other.Value);
            }
            return false;
        }

        public T GetOrElse(T otherwise)
        {
            return (this.HasValue? this.Value : otherwise);
        }
        
        // a specialization of the above
        public T GetOrDefault()
        {
            return (this.HasValue? this.Value : default(T));
        }
        
        // The main interface
        public TResult Match<TResult>(Func<T, TResult> some,
                                     Func<TResult> none)
        {
            return (this.HasValue? some(this.Value) : none() );
        }

        public override string ToString()
        {
            if (!HasValue) {
                return "This Option is None";
            }
            return $"Optional has value: {Value}";
        }
    }

    /// <summary>
    /// The `Option<T>` "components" -- in lieu of real Algebraic data types
    /// </summary>
    public static class Option
    {
        public static Option<T> Some<T>(T value) { return new Option<T>(value, true); }
        public static Option<T> None<T>() { return new Option<T>(default(T), false); }
    }
}