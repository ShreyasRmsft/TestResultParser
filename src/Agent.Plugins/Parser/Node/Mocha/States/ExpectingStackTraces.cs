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
        private ParserResetAndAttempPublish attemptPublishAndResetParser;

        public List<RegexActionPair> RegexesToMatch { get; }

        public ExpectingStackTraces(ParserResetAndAttempPublish parserResetAndAttempPublish) : this(parserResetAndAttempPublish, TraceLogger.Instance, TelemetryDataCollector.Instance)
        {

        }

        public ExpectingStackTraces(ParserResetAndAttempPublish parserResetAndAttempPublish, ITraceLogger logger, ITelemetryDataCollector telemetryDataCollector)
        {
            RegexesToMatch = new List<RegexActionPair>
            {
                new RegexActionPair(MochaTestResultParserRegexes.FailedTestCase, FailedTestCaseMatched),
                new RegexActionPair(MochaTestResultParserRegexes.PassedTestCase, PassedTestCaseMatched),
                new RegexActionPair(MochaTestResultParserRegexes.PendingTestCase, PendingTestCaseMatched),
                new RegexActionPair(MochaTestResultParserRegexes.PassedTestsSummary, PassedTestsSummaryMatched)
            };

            this.logger = logger;
            this.telemetryDataCollector = telemetryDataCollector;
            this.attemptPublishAndResetParser = parserResetAndAttempPublish;
        }

        private Enum PassedTestCaseMatched(Match match, TestResultParserStateContext stateContext)
        {
            var testResult = new TestResult();
            var mochaStateContext = stateContext as MochaTestResultParserStateContext;

            testResult.Outcome = TestOutcome.Passed;
            testResult.Name = match.Groups[RegexCaptureGroups.TestCaseName].Value;

            // If a passed test case is encountered while in the stack traces state it indicates corruption
            // or incomplete stack trace data
            // This check is safety check for when we try to parse stack trace contents, as of now it will always evaluate to true
            if (mochaStateContext.StackTracesToSkipParsingPostSummary != 0)
            {
                this.logger.Error($"MochaTestResultParser : ExpectingStackTraces :  Expecting stack traces but found passed test case instead at line {mochaStateContext.CurrentLineNumber}.");
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.ExpectingStackTracesButFoundPassedTest,
                    new List<int> { mochaStateContext.TestRun.TestRunId }, true);
            }

            this.attemptPublishAndResetParser();

            return MochaTestResultParserState.ExpectingTestResults;
        }

        private Enum FailedTestCaseMatched(Match match, TestResultParserStateContext stateContext)
        {
            var testResult = new TestResult();
            var mochaStateContext = stateContext as MochaTestResultParserStateContext;

            // Handling parse errors is unnecessary
            int.TryParse(match.Groups[RegexCaptureGroups.FailedTestCaseNumber].Value, out int testCaseNumber);

            // In the event the failed test case number does not match the expected test case number log an error
            if (testCaseNumber != mochaStateContext.LastFailedTestCaseNumber + 1)
            {
                this.logger.Error($"MochaTestResultParser : ExpectingStackTraces : Expecting stack trace with" +
                    $" number {mochaStateContext.LastFailedTestCaseNumber + 1} but found {testCaseNumber} instead");
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.UnexpectedFailedStackTraceNumber,
                    new List<int> { mochaStateContext.TestRun.TestRunId }, true);

                // If it was not 1 there's a good chance we read some random line as a failed test case hence consider it a
                // as a match but do not consider it a valid stack trace
                if (testCaseNumber != 1)
                {
                    return MochaTestResultParserState.ExpectingStackTraces;
                }

                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.AttemptPublishAndResetParser,
                    new List<string> { $"Expecting stack trace with number {mochaStateContext.LastFailedTestCaseNumber} but found {testCaseNumber} instead" });

                // If the number was 1 then there's a good chance this is the beginning of the next test run, hence reset and start over
                this.attemptPublishAndResetParser();

                mochaStateContext.LastFailedTestCaseNumber++;

                testResult.Outcome = TestOutcome.Failed;
                testResult.Name = match.Groups[RegexCaptureGroups.TestCaseName].Value;

                mochaStateContext.TestRun.FailedTests.Add(testResult);

                return MochaTestResultParserState.ExpectingTestResults;
            }

            mochaStateContext.LastFailedTestCaseNumber++;

            // As of now we are ignoring stack traces
            // Otherwise parsing stacktrace code will go here

            mochaStateContext.StackTracesToSkipParsingPostSummary--;

            if (mochaStateContext.StackTracesToSkipParsingPostSummary == 0)
            {
                // We can also choose to ignore extra failures post summary if the number is not 1
                this.attemptPublishAndResetParser();
                return MochaTestResultParserState.ExpectingTestResults;
            }

            return MochaTestResultParserState.ExpectingStackTraces;
        }

        private Enum PendingTestCaseMatched(Match match, TestResultParserStateContext stateContext)
        {
            var testResult = new TestResult();
            var mochaStateContext = stateContext as MochaTestResultParserStateContext;

            testResult.Outcome = TestOutcome.Skipped;
            testResult.Name = match.Groups[RegexCaptureGroups.TestCaseName].Value;

            // If a pending test case is encountered while in the stack traces state it indicates corruption
            // or incomplete stack trace data

            // This check is safety check for when we try to parse stack trace contents
            if (mochaStateContext.StackTracesToSkipParsingPostSummary != 0)
            {
                this.logger.Error("MochaTestResultParser : ExpectingStackTraces : Expecting stack traces but found pending test case instead.");
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.ExpectingStackTracesButFoundPendingTest,
                    new List<int> { mochaStateContext.TestRun.TestRunId }, true);
            }

            this.attemptPublishAndResetParser();

            mochaStateContext.TestRun.SkippedTests.Add(testResult);
            return MochaTestResultParserState.ExpectingTestResults;
        }

        private Enum PassedTestsSummaryMatched(Match match, TestResultParserStateContext stateContext)
        {
            var mochaStateContext = stateContext as MochaTestResultParserStateContext;
            this.logger.Info($"MochaTestResultParser : ExpectingStackTraces : Passed test summary encountered at line {mochaStateContext.CurrentLineNumber}.");

            // If we were expecting more stack traces but got summary instead
            if (mochaStateContext.StackTracesToSkipParsingPostSummary != 0)
            {
                this.logger.Error("MochaTestResultParser : ExpectingStackTraces : Expecting stack traces but found passed summary instead.");
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.SummaryWithNoTestCases,
                    new List<int> { mochaStateContext.TestRun.TestRunId }, true);
            }

            this.attemptPublishAndResetParser();

            mochaStateContext.LinesWithinWhichMatchIsExpected = 1;
            mochaStateContext.ExpectedMatch = "failed/pending tests summary";

            // Handling parse errors is unnecessary
            int.TryParse(match.Groups[RegexCaptureGroups.PassedTests].Value, out int totalPassed);

            mochaStateContext.TestRun.TestRunSummary.TotalPassed = totalPassed;

            // Fire telemetry if summary does not agree with parsed tests count
            if (mochaStateContext.TestRun.TestRunSummary.TotalPassed != mochaStateContext.TestRun.PassedTests.Count)
            {
                this.logger.Error($"MochaTestResultParser : Passed tests count does not match passed summary" +
                    $" at line {mochaStateContext.CurrentLineNumber}");
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea,
                    TelemetryConstants.PassedSummaryMismatch, new List<int> { mochaStateContext.TestRun.TestRunId }, true);
            }

            // Handling parse errors is unnecessary
            long.TryParse(match.Groups[RegexCaptureGroups.TestRunTime].Value, out long timeTaken);

            // Store time taken based on the unit used
            switch (match.Groups[RegexCaptureGroups.TestRunTimeUnit].Value)
            {
                case "ms":
                    mochaStateContext.TestRun.TestRunSummary.TotalExecutionTime = TimeSpan.FromMilliseconds(timeTaken);
                    break;

                case "s":
                    mochaStateContext.TestRun.TestRunSummary.TotalExecutionTime = TimeSpan.FromMilliseconds(timeTaken * 1000);
                    break;

                case "m":
                    mochaStateContext.TestRun.TestRunSummary.TotalExecutionTime = TimeSpan.FromMilliseconds(timeTaken * 60 * 1000);
                    break;

                case "h":
                    mochaStateContext.TestRun.TestRunSummary.TotalExecutionTime = TimeSpan.FromMilliseconds(timeTaken * 60 * 60 * 1000);
                    break;
            }

            this.logger.Info("MochaTestResultParser : ExpectingStackTraces : Transitioned to state ExpectingTestRunSummary.");
            return MochaTestResultParserState.ExpectingTestRunSummary;
        }
    }
}
