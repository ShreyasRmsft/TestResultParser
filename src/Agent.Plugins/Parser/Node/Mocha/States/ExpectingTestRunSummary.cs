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

    public class ExpectingTestRunSummary : ITestResultParserState
    {
        private ITraceLogger logger;
        private ITelemetryDataCollector telemetryDataCollector;
        private ParserResetAndAttempPublish resetParserAndAttempPublish;

        public delegate MochaTestResultParserState MatchAction(Match match, MochaTestResultParserStateContext stateContext);
        public delegate void ParserResetAndAttempPublish(string reason = null);

        public List<Tuple<Regex, MatchAction>> RegexesToMatch { get; }

        public ExpectingTestRunSummary(ParserResetAndAttempPublish parserResetAndAttempPublish) : this(parserResetAndAttempPublish, TraceLogger.Instance, TelemetryDataCollector.Instance)
        {

        }

        public ExpectingTestRunSummary(ParserResetAndAttempPublish parserResetAndAttempPublish, ITraceLogger logger, ITelemetryDataCollector telemetryDataCollector)
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
            // If a passed test case is encountered while in the summary state it indicates either completion
            // or corruption of summary. Since Summary is Gospel to us, we will ignore the latter and publish
            // the run regardless. 
            this.resetParserAndAttempPublish();

            var testResult = new TestResult();

            testResult.Outcome = TestOutcome.Passed;
            testResult.Name = match.Groups[RegexCaptureGroups.TestCaseName].Value;

            stateContext.TestRun.PassedTests.Add(testResult);
            return MochaTestResultParserState.ExpectingTestResults;
        }

        private MochaTestResultParserState FailedTestCaseMatched(Match match, MochaTestResultParserStateContext stateContext)
        {
            // If a failed test case is encountered while in the summary state it indicates either completion
            // or corruption of summary. Since Summary is Gospel to us, we will ignore the latter and publish
            // the run regardless. 
            this.resetParserAndAttempPublish();

            var testResult = new TestResult();

            // Handling parse errors is unnecessary
            int.TryParse(match.Groups[RegexCaptureGroups.FailedTestCaseNumber].Value, out int testCaseNumber);

            // If it was not 1 there's a good chance we read some random line as a failed test case hence consider it a
            // as a match but do not add it to our list of test cases or consider it a valid stack trace
            if (testCaseNumber != 1)
            {
                this.logger.Error($"MochaTestResultParser : Expecting failed test case or stack trace with" +
                    $" number {stateContext.LastFailedTestCaseNumber + 1} but found {testCaseNumber} instead");
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.UnexpectedFailedTestCaseNumber,
                    new List<int> { stateContext.TestRun.TestRunId }, true);

                return MochaTestResultParserState.ExpectingTestResults;
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
