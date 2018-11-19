// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.TestResultParser.Telemetry.Interfaces
{
    using System.Collections.Generic;

    interface ITelemetryDataPublisher
    {
        /// <summary>
        /// Publish telemetry properties to pipeline telemetry service.
        /// </summary>
        /// <param name="properties">Properties to publish.</param>
        void PublishTelemetry(IDictionary<string, object> properties);
    }
}
