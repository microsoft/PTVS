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
	public class StringTelemetryRecorderTests
	{
		[TestMethod]
		public void StringTelemetryRecorder_SimpleEventTest()
		{
			var eventName = "event";

			TestTelemetryRecorder telemetryRecorder = new TestTelemetryRecorder();
			telemetryRecorder.RecordEvent(eventName);

			string log = telemetryRecorder.SessionLog;
			Assert.AreEqual(eventName + "\r\n", log);
		}

		[TestMethod]
		public void StringTelemetryRecorder_EventWithDictionaryTest()
		{
			var eventName = "event";
			var parameter1 = "parameter1";
			var value1 = "value1";
			var parameter2 = "parameter2";
			var value2 = "value2";

			TestTelemetryRecorder telemetryRecorder = new TestTelemetryRecorder();
			telemetryRecorder.RecordEvent(eventName, new Dictionary<string, object>() { { parameter1, value1 }, { parameter2, value2 } });

			string log = telemetryRecorder.SessionLog;
			Assert.AreEqual(eventName + "\r\n\t" + parameter1 + " : " + value1 + "\r\n\t" + parameter2 + " : " + value2 + "\r\n", log);
		}

		[TestMethod]
		public void StringTelemetryRecorder_EventWithAnonymousCollectionTest()
		{
			var eventName = "event";

			TestTelemetryRecorder telemetryRecorder = new TestTelemetryRecorder();
			telemetryRecorder.RecordEvent(eventName, new { parameter1 = "value1", parameter2 = "value2" });

			string log = telemetryRecorder.SessionLog;
			Assert.AreEqual(eventName + "\r\n\tparameter1 : value1\r\n\tparameter2 : value2\r\n", log);
		}
	}
}
