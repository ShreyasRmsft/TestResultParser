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

    public class ExpectingTestResults : ITestResultParserState
    {
        private ITraceLogger logger;
        private ITelemetryDataCollector telemetryDataCollector;
        private ParserResetAndAttempPublish resetParserAndAttempPublish;

        public delegate MochaTestResultParserState MatchAction(Match match, MochaTestResultParserStateContext stateContext);
        public delegate void ParserResetAndAttempPublish(string reason = null);

        public List<Tuple<Regex, MatchAction>> RegexesToMatch { get; }

        public ExpectingTestResults(ParserResetAndAttempPublish parserResetAndAttempPublish)
            : this(parserResetAndAttempPublish, TraceLogger.Instance, TelemetryDataCollector.Instance)
        {

        }

        public ExpectingTestResults(ParserResetAndAttempPublish parserResetAndAttempPublish, ITraceLogger logger,
            ITelemetryDataCollector telemetryDataCollector)
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

            stateContext.TestRun.PassedTests.Add(testResult);
            return MochaTestResultParserState.ExpectingTestResults;
        }

        private MochaTestResultParserState FailedTestCaseMatched(Match match, MochaTestResultParserStateContext stateContext)
        {
            var testResult = new TestResult();

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
                // as a match but do not add it to our list of test cases
                if (testCaseNumber != 1)
                {
                    return MochaTestResultParserState.ExpectingTestResults;
                }

                // If the number was 1 then there's a good chance this is the beginning of the next test run, hence reset and start over
                // This is something we might choose to change if we realize there is a chance we can get such false detections often in the middle of a run
                this.resetParserAndAttempPublish($"was expecting failed test case or stack trace with number {stateContext.LastFailedTestCaseNumber} but found" +
                    $" {testCaseNumber} instead");
            }

            // Increment either ways whether it was expected or context was reset and the encountered number was 1
            stateContext.LastFailedTestCaseNumber++;

            testResult.Outcome = TestOutcome.Failed;
            testResult.Name = match.Groups[RegexCaptureGroups.TestCaseName].Value;

            stateContext.TestRun.FailedTests.Add(testResult);

            return MochaTestResultParserState.ExpectingTestResults;
        }
    }
}
