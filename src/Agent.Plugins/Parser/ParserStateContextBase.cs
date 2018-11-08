namespace Agent.Plugins.TestResultParser.Parser
{
    public abstract class ParserStateContextBase
    {
        public bool PassedTestsSummaryEncountered { get; set; }

        public bool FailedTestsSummaryEncountered { get; set; }
    }
}
