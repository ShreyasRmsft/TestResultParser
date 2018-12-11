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
                new RegexActionPair(Regexes.TestRunStart, TestRunStartMatched),
            };
        }

        private Enum TestRunStartMatched(Match match, TestResultParserStateContext stateContext)
        {
            var jestStateContext = stateContext as JestParserStateContext;

            this.logger.Info($"JestTestResultParser : ExpectingTestRunStart : Transitioned to state ExpectingTestResults" +
                $" at line {jestStateContext.CurrentLineNumber}.");

            return JestParserStates.ExpectingTestResults;
        }
    }
}
