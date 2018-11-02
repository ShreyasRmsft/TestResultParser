namespace Agent.Plugins.TestResultParser.Telemetry
{
    using System;
    using Agent.Plugins.TestResultParser.Telemetry.Interfaces;

    public class TelemetryDataCollector : ITelemetryDataCollector
    {
        private static ITelemetryDataCollector instance;

        /// <summary>
        /// Gets the singleton instance of telemetry data collector.
        /// </summary>
        public static ITelemetryDataCollector Instance {
            get => instance ?? (instance = new TelemetryDataCollector());
            internal set => instance = value;
        }

        /// <inheritdoc />
        public void AddProperty(string key, object value, bool aggregate = false)
        {

        }
    }
}
