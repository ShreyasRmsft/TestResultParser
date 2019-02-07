﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Agent.Plugins.Log.TestResultParser.Contracts
{
    public interface ITelemetryDataCollector
    {
        /// <summary>
        /// Cumulates data throught the life cycle of the data collector until PublishCumulativeTelemetryAsync is called
        /// This is used when the same data point is hit multiple times in an execution flow and firing an event each time
        /// is a costly operations. This helps is agrregating the data that would otherwise have been done later and the 
        /// inidividual data points themselves are of no interest.
        /// </summary>
        /// <param name="eventArea">Sub area of the data point</param>
        /// <param name="eventName">Name of the data point</param>
        /// <param name="value">Data point value to be stored</param>
        /// <param name="aggregate">When to override or perform smart aggregation when the data point already exists</param>
        void AddToCumulativeTelemetry(string eventArea, string eventName, object value, bool aggregate = false);

        /// <summary>
        /// Publish the cumulative telemetry accrued over tinme and resets the collection
        /// </summary>
        Task PublishCumulativeTelemetryAsync();

        /// <summary>
        /// Publish a standalone event with custom properties
        /// <param name="feature">Feature name for the event</param>
        /// <param name="properties">Custom set of properties to publish</param>
        /// </summary>
        Task PublishTelemetryAsync(string feature, Dictionary<string, object> properties);
    }
}
