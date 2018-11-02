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

        }
    }
}
