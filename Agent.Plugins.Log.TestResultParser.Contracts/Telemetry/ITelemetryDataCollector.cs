// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Agent.Plugins.Log.TestResultParser.Contracts
{
    public interface ITelemetryDataCollector
    {
        void AddToCumulativeTelemetry(string eventArea, string eventName, object value, bool aggregate = false);

        Task PublishTelemetryAsync(string eventArea, string eventName, Dictionary<string, object> properties);
    }
}
