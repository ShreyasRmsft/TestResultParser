namespace Agent.Plugins.TestResultParser.Telemetry.Interfaces
{
    public interface ITelemetryDataCollector
    {
        void AddToCumulativeTelemtery(string EventArea, string EventName, object value, bool aggregate = false);
    }
}
