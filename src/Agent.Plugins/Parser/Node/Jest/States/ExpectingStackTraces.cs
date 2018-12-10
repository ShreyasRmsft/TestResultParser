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
    using Agent.Plugins.TestResultParser.TestResult.Models;

    public class ExpectingStackTraces : JestParserStateBase
    {
        public override IEnumerable<RegexActionPair> RegexesToMatch { get; }

        public ExpectingStackTraces(ParserResetAndAttemptPublish parserResetAndAttempPublish)
            : this(parserResetAndAttempPublish, TraceLogger.Instance, TelemetryDataCollector.Instance)
        {

        }

        /// <inheritdoc />
        public ExpectingStackTraces(ParserResetAndAttemptPublish parserResetAndAttemptPublish, ITraceLogger logger, ITelemetryDataCollector telemetryDataCollector)
            : base(parserResetAndAttemptPublish, logger, telemetryDataCollector)
        {
            RegexesToMatch = new List<RegexActionPair>
            {
                new RegexActionPair(Regexes.StackTraceStart, StackTraceStartMatched),
                new RegexActionPair(Regexes.SummaryStart, SummaryStartMatched),
                new RegexActionPair(Regexes.TestRunStart, TestRunStartMatched),
                new RegexActionPair(Regexes.FailedTestsSummaryIndicator, FailedTestsSummaryIndicatorMatched)
            };
        }

        private Enum StackTraceStartMatched(Match match, TestResultParserStateContext stateContext)
        {
            var jestStateContext = stateContext as JestParserStateContext;

            if (jestStateContext.FailedTestsSummaryIndicatorEncountered)
            {
                logger.Verbose($"JestTestResultParser : ExpectingStackTraces: Ignoring StackTrace/Failed test case at line " +
                    $"{stateContext.CurrentLineNumber} as it is part of the summarized failures.");
                return JestParserStates.ExpectingStackTraces;
            }
            
            // In non verbose mode console out appears as a failed test case
            // Only difference being it's not colored red
            if (match.Groups[RegexCaptureGroups.TestCaseName].Value == "Console")
            {
                logger.Verbose($"JestTestResultParser : ExpectingStackTraces: Ignoring apparent StackTrace/Failed test case at line " +
                    $"{stateContext.CurrentLineNumber} as Jest prints console out in this format in non verbose mode.");
                return JestParserStates.ExpectingStackTraces;
            }

            var testResult = PrepareTestResult(TestOutcome.Failed, match);
            jestStateContext.TestRun.FailedTests.Add(testResult);

            return JestParserStates.ExpectingStackTraces;
        }

        private Enum SummaryStartMatched(Match match, TestResultParserStateContext stateContext)
        {
            var jestStateContext = stateContext as JestParserStateContext;

            jestStateContext.LinesWithinWhichMatchIsExpected = 1;
            jestStateContext.NextExpectedMatch = "tests summary";

            logger.Info($"JestTestResultParser : ExpectingStackTraces : Transitioned to state ExpectingTestRunSummary.");

            return JestParserStates.ExpectingTestRunSummary;
        }

        private Enum TestRunStartMatched(Match match, TestResultParserStateContext stateContext)
        {
            var jestStateContext = stateContext as JestParserStateContext;

            // If a test run start indicator is encountered after failedTestsSummaryInidicator has
            // been encountered it must be ignored
            if (jestStateContext.FailedTestsSummaryIndicatorEncountered)
            {
                return JestParserStates.ExpectingStackTraces;
            }

            // Do we want to use PASS/FAIL information here?
            logger.Info($"JestTestResultParser : ExpectingStackTraces : Transitioned to state ExpectingTestResults.");

            return JestParserStates.ExpectingTestResults;
        }

        private Enum FailedTestsSummaryIndicatorMatched(Match match, TestResultParserStateContext stateContext)
        {
            var jestStateContext = stateContext as JestParserStateContext;

            jestStateContext.FailedTestsSummaryIndicatorEncountered = true;
            logger.Info($"JestTestResultParser : ExpectingStackTraces : ");

            return JestParserStates.ExpectingStackTraces;
        } 
    }
}
