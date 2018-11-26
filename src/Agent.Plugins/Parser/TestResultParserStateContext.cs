namespace Agent.Plugins.TestResultParser.Parser
{
    /// <summary>
    /// Base class for all state context objects
    /// </summary>
    public abstract class TestResultParserStateContext
    {
        public abstract void Reset();
        // Extract out common properties here when enough parsers have been authored
    }
}
