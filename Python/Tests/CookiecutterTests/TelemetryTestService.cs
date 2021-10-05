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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

namespace CookiecutterTests
{
	internal sealed class TelemetryTestService : TelemetryServiceBase, ITelemetryTestSupport
	{
		public static readonly string EventNamePrefixString = "Test/Cookiecutter/";
		public static readonly string PropertyNamePrefixString = "Test.Cookiecutter.";

		public TelemetryTestService(string eventNamePrefix, string propertyNamePrefix) :
			base(eventNamePrefix, propertyNamePrefix, new TestTelemetryRecorder())
		{
		}

		public TelemetryTestService() :
			this(TelemetryTestService.EventNamePrefixString, TelemetryTestService.PropertyNamePrefixString)
		{
		}

		#region ITelemetryTestSupport
		public string SessionLog
		{
			get
			{
				ITelemetryTestSupport testSupport = this.TelemetryRecorder as ITelemetryTestSupport;
				return testSupport.SessionLog;
			}
		}

		public void Reset()
		{
			ITelemetryTestSupport testSupport = this.TelemetryRecorder as ITelemetryTestSupport;
			testSupport.Reset();
		}
		#endregion
	}
}
