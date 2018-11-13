namespace Agent.Plugins.TestResultParser.Parser.Python
{
    internal enum ParserState
    {
        ExpectingTestResults,
        ExpectingFailedResults,
        ExpectingSummary
    }
}