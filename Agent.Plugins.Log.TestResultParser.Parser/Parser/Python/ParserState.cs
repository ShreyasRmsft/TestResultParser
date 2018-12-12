namespace Agent.Plugins.Log.TestResultParser.Parser
{
    internal enum ParserState
    {
        ExpectingTestResults,
        ExpectingFailedResults,
        ExpectingSummary
    }
}