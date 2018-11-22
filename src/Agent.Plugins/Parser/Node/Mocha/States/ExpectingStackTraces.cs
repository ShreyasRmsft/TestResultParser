namespace Agent.Plugins.TestResultParser.Parser.Node.Mocha.States
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

    public class ExpectingStackTraces : ITestResultParserState
    {
        private ITraceLogger logger;
        private ITelemetryDataCollector telemetryDataCollector;
        private ParserResetAndAttempPublish resetParserAndAttempPublish;

        public delegate MochaTestResultParserState MatchAction(Match match, MochaTestResultParserStateContext stateContext);
        public delegate void ParserResetAndAttempPublish(string reason = null);

        public List<Tuple<Regex, MatchAction>> RegexesToMatch { get; }

        public ExpectingStackTraces(ParserResetAndAttempPublish parserResetAndAttempPublish) : this(parserResetAndAttempPublish, TraceLogger.Instance, TelemetryDataCollector.Instance)
        {

        }

        public ExpectingStackTraces(ParserResetAndAttempPublish parserResetAndAttempPublish, ITraceLogger logger, ITelemetryDataCollector telemetryDataCollector)
        {
            RegexesToMatch = new List<Tuple<Regex, MatchAction>>
            {
                new Tuple<Regex, MatchAction>(MochaTestResultParserRegexes.PassedTestCase, PassedTestCaseMatched),
                new Tuple<Regex, MatchAction>(MochaTestResultParserRegexes.FailedTestCase, FailedTestCaseMatched),
                new Tuple<Regex, MatchAction>(MochaTestResultParserRegexes.PendingTestCase, null),
                new Tuple<Regex, MatchAction>(MochaTestResultParserRegexes.PassedTestsSummary, null)
            };

            this.logger = logger;
            this.telemetryDataCollector = telemetryDataCollector;
            this.resetParserAndAttempPublish = parserResetAndAttempPublish;
        }

        private MochaTestResultParserState PassedTestCaseMatched(Match match, MochaTestResultParserStateContext stateContext)
        {
            var testResult = new TestResult();

            testResult.Outcome = TestOutcome.Passed;
            testResult.Name = match.Groups[RegexCaptureGroups.TestCaseName].Value;

            // If a passed test case is encountered while in the stack traces state it indicates corruption
            // or incomplete stack trace data
            // This check is safety check for when we try to parse stack trace contents
            if (stateContext.StackTracesToSkipParsingPostSummary != 0)
            {
                this.logger.Error($"MochaTestResultParser : Expecting stack traces but found passed test case instead at line {stateContext.CurrentLineNumber}.");
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.ExpectingStackTracesButFoundPassedTest,
                    new List<int> { stateContext.TestRun.TestRunId }, true);
            }

            this.resetParserAndAttempPublish();

            return MochaTestResultParserState.ExpectingTestResults;
        }


        private MochaTestResultParserState FailedTestCaseMatched(Match match, MochaTestResultParserStateContext stateContext)
        {
            var testResult = new TestResult();
            var nextState = MochaTestResultParserState.ExpectingStackTraces;

            // Handling parse errors is unnecessary
            int.TryParse(match.Groups[RegexCaptureGroups.FailedTestCaseNumber].Value, out int testCaseNumber);

            // In the event the failed test case number does not match the expected test case number log an error and move on
            if (testCaseNumber != stateContext.LastFailedTestCaseNumber + 1)
            {
                this.logger.Error($"MochaTestResultParser : Expecting failed test case or stack trace with" +
                    $" number {stateContext.LastFailedTestCaseNumber + 1} but found {testCaseNumber} instead");
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.UnexpectedFailedTestCaseNumber,
                    new List<int> { stateContext.TestRun.TestRunId }, true);

                // If it was not 1 there's a good chance we read some random line as a failed test case hence consider it a
                // as a match but do not add it to our list of test cases or consider it a valid stack trace
                if (testCaseNumber != 1)
                {
                    return nextState;
                }

                // If the number was 1 then there's a good chance this is the beginning of the next test run, hence reset and start over
                this.resetParserAndAttempPublish($"was expecting failed test case or stack trace with number {stateContext.LastFailedTestCaseNumber} but found" +
                    $" {testCaseNumber} instead");
                nextState = MochaTestResultParserState.ExpectingTestResults;
            }

            // Increment either ways whether it was expected or context was reset and the encountered number was 1
            stateContext.LastFailedTestCaseNumber++;

            // As of now we are ignoring stack traces
            // Otherwise parsing stack trace code will go here

            stateContext.StackTracesToSkipParsingPostSummary--;

            if (stateContext.StackTracesToSkipParsingPostSummary == 0)
            {
                // we can also choose to ignore extra failures post summary if the number is not 1
                this.resetParserAndAttempPublish();
            }

            return MochaTestResultParserState.ExpectingStackTraces;
        }

        private MochaTestResultParserState MatchPendingTestCase(Match match, MochaTestResultParserStateContext stateContext)
        {

        }
    }
}
