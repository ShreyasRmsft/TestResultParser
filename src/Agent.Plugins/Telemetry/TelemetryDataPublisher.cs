// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.TestResultParser.Telemetry
{
    using System;
    using System.Collections.Generic;
    using Agent.Plugins.TestResultParser.Telemetry.Interfaces;

    class TelemetryDataPublisher : ITelemetryDataPublisher
    {
        /// <inheritdoc />
        public void PublishTelemetry(IDictionary<string, object> properties)
        {
            throw new NotImplementedException();
        }
    }
}
