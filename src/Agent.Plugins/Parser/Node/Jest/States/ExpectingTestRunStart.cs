using System.Collections.Generic;

namespace Agent.Plugins.TestResultParser.Parser.Node.Jest.States
{
    public class ExpectingTestRunStart : JestParserStateBase
    {
        public override IEnumerable<RegexActionPair> RegexesToMatch { get; }

        public ExpectingTestRunStart(ParserResetAndAttemptPublish parserResetAndAttempPublish)
            : this(parserResetAndAttempPublish, TraceLogger.Instance, TelemetryDataCollector.Instance)
        {

        }

        /// <inheritdoc />
        public ExpectingTestRunStart(ParserResetAndAttemptPublish parserResetAndAttemptPublish, ITraceLogger logger, ITelemetryDataCollector telemetryDataCollector)
            : base(parserResetAndAttemptPublish, logger, telemetryDataCollector)
        {
            RegexesToMatch = new List<RegexActionPair>
            {
                new RegexActionPair(Regexes.PassedTestCase, PassedTestCaseMatched),
                new RegexActionPair(Regexes.FailedTestCase, FailedTestCaseMatched),
                new RegexActionPair(Regexes.PendingTestCase, PendingTestCaseMatched),
                new RegexActionPair(Regexes.PassedTestsSummary, PassedTestsSummaryMatched)
            };
        }
    }
}
