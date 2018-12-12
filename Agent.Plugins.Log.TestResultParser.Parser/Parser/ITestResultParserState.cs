using System.Collections.Generic;

namespace Agent.Plugins.Log.TestResultParser.Parser
{
    public delegate void ParserResetAndAttemptPublish();

    public interface ITestResultParserState
    {
        IEnumerable<RegexActionPair> RegexsToMatch { get; }
    }
}
