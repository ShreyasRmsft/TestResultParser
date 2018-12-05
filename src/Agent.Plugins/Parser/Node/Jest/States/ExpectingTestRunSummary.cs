namespace Agent.Plugins.TestResultParser.Parser.Node.Jest.States
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using Agent.Plugins.TestResultParser.Loggers;
    using Agent.Plugins.TestResultParser.Loggers.Interfaces;
    using Agent.Plugins.TestResultParser.Parser.Interfaces;
    using Agent.Plugins.TestResultParser.Telemetry;
    using Agent.Plugins.TestResultParser.Telemetry.Interfaces;

    public class ExpectingTestRunSummary : JestParserStateBase
    {
        public override IEnumerable<RegexActionPair> RegexesToMatch { get; }

        public ExpectingTestRunSummary(ParserResetAndAttemptPublish parserResetAndAttempPublish)
            : this(parserResetAndAttempPublish, TraceLogger.Instance, TelemetryDataCollector.Instance)
        {

        }

        /// <inheritdoc />
        public ExpectingTestRunSummary(ParserResetAndAttemptPublish parserResetAndAttemptPublish, ITraceLogger logger, ITelemetryDataCollector telemetryDataCollector)
            : base(parserResetAndAttemptPublish, logger, telemetryDataCollector)
        {
            RegexesToMatch = new List<RegexActionPair>
            {
                new RegexActionPair(Regexes.TestsSummaryMatcher, TestsSummaryMatched),
                new RegexActionPair(Regexes.TestRunStart, TestRunStartMatched),
                new RegexActionPair(Regexes.TestRunTimeMatcher, TestRunTimeMatched)
            };
        }

        private Enum TestsSummaryMatched(Match match, TestResultParserStateContext stateContext)
        {
            var mochaStateContext = stateContext as JestParserStateContext;

            // Do we want to use PASS/FAIL information here?

            return JestParserStates.ExpectingTestRunSummary;
        }

        private Enum TestRunTimeMatched(Match match, TestResultParserStateContext stateContext)
        {
            var mochaStateContext = stateContext as JestParserStateContext;

            // Do we want to use PASS/FAIL information here?

            return JestParserStates.ExpectingTestRunStart;
        }

        private Enum TestRunStartMatched(Match match, TestResultParserStateContext stateContext)
        {
            var mochaStateContext = stateContext as JestParserStateContext;

            // Do we want to use PASS/FAIL information here?

            return JestParserStates.ExpectingTestResults;
        }
    }
}
