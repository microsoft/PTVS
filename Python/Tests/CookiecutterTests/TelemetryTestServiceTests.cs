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
	[TestClass]
	public class TelemetryTestServiceTests
	{
		[TestMethod]
		public void TelemetryTestService_DefaultPrefixConstructorTest()
		{
			TelemetryTestService telemetryService = new TelemetryTestService();
			Assert.AreEqual(TelemetryTestService.EventNamePrefixString, telemetryService.EventNamePrefix);
			Assert.AreEqual(TelemetryTestService.PropertyNamePrefixString, telemetryService.PropertyNamePrefix);
		}

		[TestMethod]
		public void TelemetryTestService_CustomPrefixConstructorTest()
		{
			var eventPrefix = "Event/Prefix/";
			var propertyPrefix = "Property.Prefix.";

			TelemetryTestService telemetryService = new TelemetryTestService(eventPrefix, propertyPrefix);
			Assert.AreEqual(eventPrefix, telemetryService.EventNamePrefix);
			Assert.AreEqual(propertyPrefix, telemetryService.PropertyNamePrefix);
		}

		[TestMethod]
		public void TelemetryTestService_SimpleEventTest()
		{
			var area = "Options";
			var eventName = "event";

			TelemetryTestService telemetryService = new TelemetryTestService();
			telemetryService.ReportEvent(area, eventName);
			string log = telemetryService.SessionLog;
			Assert.AreEqual(TelemetryTestService.EventNamePrefixString + area.ToString() + "/" + eventName + "\r\n", log);
		}

		[TestMethod]
		public void TelemetryTestService_EventWithParametersTest()
		{
			var area = "Options";
			var eventName = "event";

			TelemetryTestService telemetryService = new TelemetryTestService();
			telemetryService.ReportEvent(area, eventName, new { parameter = "value" });
			string log = telemetryService.SessionLog;
			Assert.AreEqual(TelemetryTestService.EventNamePrefixString + area.ToString() + "/" + eventName +
							"\r\n\t" + TelemetryTestService.PropertyNamePrefixString + area.ToString() + ".parameter : value\r\n", log);
		}
	}
}
