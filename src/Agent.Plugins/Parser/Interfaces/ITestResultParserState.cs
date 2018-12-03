namespace Agent.Plugins.TestResultParser.Parser.Interfaces
{
    using System.Collections.Generic;
    using Agent.Plugins.TestResultParser.Parser;

    public delegate void ParserResetAndAttemptPublish();

    public interface ITestResultParserState
    {
        IEnumerable<RegexActionPair> RegexesToMatch { get; }
    }
}
