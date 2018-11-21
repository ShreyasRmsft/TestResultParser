// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.TestResultParser.Telemetry.Interfaces
{
    public interface ITelemetryDataCollector
    {
        void AddToCumulativeTelemtery(string EventArea, string EventName, object value, bool aggregate = false);
    }
}
