namespace Agent.Plugins.TestResultParser.Telemetry.Interfaces
{
    public interface ITelemetryDataCollector
    {
        /// <summary>
        /// Adds key value pair as telemetry data.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <param name="value">Value.</param>
        /// <param name="aggregate"></param>
        void AddProperty(string key, object value, bool aggregate = false);
        void AddProperty(object unexpectedError, string v, bool aggregate);
    }
}
