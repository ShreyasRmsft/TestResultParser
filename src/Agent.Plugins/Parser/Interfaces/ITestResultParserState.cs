namespace Agent.Plugins.TestResultParser.Parser.Interfaces
{
    using System.Collections.Generic;
    using Agent.Plugins.TestResultParser.Parser;

    public delegate void ParserResetAndAttempPublish();

    public interface ITestResultParserState
    {
        List<RegexActionPair> RegexesToMatch { get; }
    }
}
