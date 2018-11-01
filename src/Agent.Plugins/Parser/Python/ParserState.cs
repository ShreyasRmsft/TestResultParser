namespace Agent.Plugins.TestResultParser.Parser.Python
{
    internal enum ParserState
    {
        WaitingForResultOrSummary,
        WaitingForSummary,
        WaitingForPartialResultOrSummary,
        WaitingForPartialSummary,
        Completed
    }
}