namespace Agent.Plugins.Log.TestResultParser.Parser
{
    using System;
    using System.Text.RegularExpressions;

    public delegate Enum MatchAction(Match match, TestResultParserStateContext stateContext);

    public class RegexActionPair
    {
        public RegexActionPair(Regex regex, MatchAction matchAction)
        {
            Regex = regex;
            MatchAction = matchAction;
        }

        public Regex Regex { get; }

        public MatchAction MatchAction { get; }
    }
}
