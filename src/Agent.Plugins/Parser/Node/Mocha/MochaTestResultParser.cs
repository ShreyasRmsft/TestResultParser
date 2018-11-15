// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.TestResultParser.Parser.Node.Mocha
{
    using System;
    using System.Collections.Generic;
    using Agent.Plugins.TestResultParser.Loggers;
    using Agent.Plugins.TestResultParser.Loggers.Interfaces;
    using Agent.Plugins.TestResultParser.Parser.Interfaces;
    using Agent.Plugins.TestResultParser.Parser.Models;
    using Agent.Plugins.TestResultParser.Telemetry;
    using Agent.Plugins.TestResultParser.Telemetry.Interfaces;
    using Agent.Plugins.TestResultParser.TestResult.Models;
    using Agent.Plugins.TestResultParser.TestRunManger;
    using TestResult = TestResult.Models.TestResult;

    public class MochaTestResultParser : ITestResultParser
    {
        // TODO: Need a hook for end of logs.
        // Needed for multiple reasons. Scenarios where i am expecting things and have not yet published the run
        // Needed where I have encoutered test results but got no summary
        // It is true that it can be inferred due to the absense of the summary event, but I would like there to
        // be one telemetry event per parser run

        // TODO: Decide on a reset if no match found withing x lines logic after a previous match.
        // This can be fine tuned depending on the previous match
        // Infra already in place for this

        private TestRun testRun;
        private MochaTestResultParserStateContext stateContext;
        private int currentTestRunId = 1;

        private MochaTestResultParserState state;
        private ITraceLogger logger;
        private ITelemetryDataCollector telemetryDataCollector;
        private ITestRunManager testRunManager;

        public string ParserName => nameof(MochaTestResultParser);

        public string ParserVersion => "1.0";

        /// <summary>
        /// TODO
        /// </summary>
        /// <param name="testRunManager"></param>
        public MochaTestResultParser(ITestRunManager testRunManager) : this(testRunManager, TraceLogger.Instance, TelemetryDataCollector.Instance)
        {

        }

        /// <summary>
        /// TODO
        /// </summary>
        /// <param name="testRunPublisher"></param>
        /// <param name="diagnosticDataCollector"></param>
        /// <param name="telemetryDataCollector"></param>
        public MochaTestResultParser(ITestRunManager testRunManager, ITraceLogger logger, ITelemetryDataCollector telemetryDataCollector)
        {
            logger.Info("MochaTestResultParser.MochaTestResultParser : Starting mocha test result parser.");
            telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.Initialize, true);

            this.testRunManager = testRunManager;
            this.logger = logger;
            this.telemetryDataCollector = telemetryDataCollector;

            // Initialize the starting state of the parser
            testRun = new TestRun { TestRunId = currentTestRunId, FailedTests = new List<TestResult>(), PassedTests = new List<TestResult>(), SkippedTests = new List<TestResult>(), TestRunSummary = new TestRunSummary() };
            stateContext = new MochaTestResultParserStateContext();
            state = MochaTestResultParserState.ExpectingTestResults;

            testRun.ParserUri = $"{ParserName}/{ParserVersion}";
        }

        /// <inheritdoc/>
        public void Parse(LogLineData testResultsLine)
        {
            // State model for the mocha parser that defines the regexes to match against in each state
            // Each state re-orders the regexes based on the frequency of expected matches
            switch (state)
            {
                // This state primarily looks for test results 
                // and transitions to the next one after a line of summary is encountered
                case MochaTestResultParserState.ExpectingTestResults:

                    if (MatchPassedTestCase(testResultsLine))
                    {
                        return;
                    }
                    else if (MatchFailedTestCase(testResultsLine))
                    {
                        return;
                    }
                    else if (MatchPendingTestCase(testResultsLine))
                    {
                        return;
                    }
                    else if (MatchPassedSummary(testResultsLine))
                    {
                        return;
                    }

                    break;

                // This state primarily looks for test run summary 
                // If failed tests were found to be present transitions to the next one to look for stack traces
                // else goes back to the first state after publishing the run
                case MochaTestResultParserState.ExpectingTestRunSummary:

                    if (MatchPendingSummary(testResultsLine))
                    {
                        return;
                    }
                    else if (MatchFailedSummary(testResultsLine))
                    {
                        return;
                    }
                    else if (MatchPassedTestCase(testResultsLine))
                    {
                        return;
                    }
                    else if (MatchFailedTestCase(testResultsLine))
                    {
                        return;
                    }
                    else if (MatchPendingTestCase(testResultsLine))
                    {
                        return;
                    }
                    else if (MatchPassedSummary(testResultsLine))
                    {
                        return;
                    }

                    break;

                // This state primarily looks for stack traces
                // If any other match occurs before all the expected stack traces are found it 
                // fires telemetry for unexpected behavior but moves on to the next test run
                case MochaTestResultParserState.ExpectingStackTraces:

                    if (MatchFailedTestCase(testResultsLine))
                    {
                        return;
                    }
                    else if (MatchPassedTestCase(testResultsLine))
                    {
                        return;
                    }
                    else if (MatchPendingTestCase(testResultsLine))
                    {
                        return;
                    }
                    else if (MatchPassedSummary(testResultsLine))
                    {
                        return;
                    }

                    break;
            }

            // This is a mechanism to enforce matches that have to occur within 
            // a specific number of lines after encountering the previous match
            // one obvious usage is for successive summary lines containing passed,
            // pending and failed test summary
            if (stateContext.LinesWithinWhichMatchIsExpected == 1)
            {
                AttemptPublishAndResetParser($"was expecting {stateContext.ExpectedMatch} before line {testResultsLine.LineNumber} but no matches occurred");
                return;
            }
            else if (stateContext.LinesWithinWhichMatchIsExpected > 1)
            {
                stateContext.LinesWithinWhichMatchIsExpected--;
                return;
            }
        }

        /// <summary>
        /// Publishes the run and resets the parser by resetting the state context and current state
        /// </summary>
        private void AttemptPublishAndResetParser(string reason = null)
        {

            if (!string.IsNullOrEmpty(reason))
            {
                logger.Info($"MochaTestResultParser : Resetting the parser and attempting to publishing the test run : {reason}.");
                telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea,
                    TelemetryConstants.AttemptPublishAndResetParser, new List<string> { reason }, true);
            }

            // We have encountered failed test cases but no failed summary was encountered
            if (testRun.FailedTests.Count != 0 && testRun.TestRunSummary.TotalFailed == 0)
            {
                logger.Error("MochaTestResultParser : Failed tests were encountered but no failed summary was encountered.");
                telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea,
                    TelemetryConstants.FailedTestCasesFoundButNoFailedSummary, new List<int> { currentTestRunId }, true);
            }

            // We have encountered pending test cases but no pending summary was encountered
            if (testRun.SkippedTests.Count != 0 && testRun.TestRunSummary.TotalSkipped == 0)
            {
                logger.Error("MochaTestResultParser : Skipped tests were encountered but no skipped summary was encountered.");
                telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea,
                    TelemetryConstants.PendingTestCasesFoundButNoFailedSummary, new List<int> { currentTestRunId }, true);
            }

            // Ensure some summary data was detected before attempting a publish, ie. check if the state is not test results state
            switch (state)
            {
                case MochaTestResultParserState.ExpectingTestResults:
                    if (testRun.PassedTests.Count != 0)
                    {
                        logger.Error("MochaTestResultParser : Passed tests were encountered but no passed summary was encountered.");
                        telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea,
                            TelemetryConstants.PassedTestCasesFoundButNoPassedSummary, new List<int> { currentTestRunId }, true);
                    }
                    break;

                default:
                    // Publish the test run if reset and publish was called from any state other than the test results state
                    testRunManager.Publish(testRun);
                    currentTestRunId++;
                    break;
            }

            // Refresh the context
            stateContext = new MochaTestResultParserStateContext();

            // Start a new TestRun
            testRun = new TestRun() { TestRunId = currentTestRunId, FailedTests = new List<TestResult>(), PassedTests = new List<TestResult>(), SkippedTests = new List<TestResult>(), TestRunSummary = new TestRunSummary() };
            testRun.ParserUri = $"{ParserName}/{ParserVersion}";
            state = MochaTestResultParserState.ExpectingTestResults;

            logger.Info("MochaTestResultParser : Successfully reset the parser.");
        }

        /// <summary>
        /// Matches a line of input with the passed test case regex and performs appropriate actions 
        /// on a successful match
        /// </summary>
        /// <param name="testResultsLine"></param>
        /// <returns></returns>
        private bool MatchPassedTestCase(LogLineData testResultsLine)
        {
            var match = MochaTestResultParserRegularExpressions.PassedTestCaseMatcher.Match(testResultsLine.Line);

            if (match.Success)
            {
                var testResult = new TestResult();

                testResult.Outcome = TestOutcome.Passed;
                testResult.Name = match.Groups[RegexCaptureGroups.TestCaseName].Value;

                // Also since this is an action performed in context of a state should there be a separate function?
                // Should this intelligence come from the caller?

                switch (state)
                {
                    // If a passed test case is encountered while in the summary state it indicates either completion
                    // or corruption of summary. Since Summary is Gospel to us, we will ignore the latter and publish
                    // the run regardless. 
                    case MochaTestResultParserState.ExpectingTestRunSummary:

                        AttemptPublishAndResetParser();
                        break;

                    // If a passed test case is encountered while in the stack traces state it indicates corruption
                    // or incomplete stack trace data
                    case MochaTestResultParserState.ExpectingStackTraces:

                        // This check is safety check for when we try to parse stack trace contents
                        if (stateContext.StackTracesToSkipParsingPostSummary != 0)
                        {
                            logger.Error($"MochaTestResultParser : Expecting stack traces but found passed test case instead at line {testResultsLine.LineNumber}.");
                            telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.ExpectingStackTracesButFoundPassedTest,
                                new List<int> { currentTestRunId }, true);
                        }

                        AttemptPublishAndResetParser();
                        break;
                }

                testRun.PassedTests.Add(testResult);
                return true;
            }

            return false;
        }

        private bool MatchFailedTestCase(LogLineData testResultsLine)
        {
            var match = MochaTestResultParserRegularExpressions.FailedTestCaseMatcher.Match(testResultsLine.Line);

            if (match.Success)
            {
                var testResult = new TestResult();

                // Handle parse errors
                if (!int.TryParse(match.Groups[RegexCaptureGroups.FailedTestCaseNumber].Value, out int testCaseNumber))
                {
                    logger.Error($"MochaTestResultParser : MatchFailedTestCase : failed to parse failed test case number" +
                        $" {match.Groups[RegexCaptureGroups.FailedTestCaseNumber].Value}. at line {testResultsLine.LineNumber}");
                }

                // In the event the failed test case number does not match the expected test case number log an error and move on
                if (testCaseNumber != stateContext.LastFailedTestCaseNumber + 1)
                {
                    logger.Error($"MochaTestResultParser : Expecting failed test case or stack trace with" +
                        $" number {stateContext.LastFailedTestCaseNumber + 1} but found {testCaseNumber} instead");
                    telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.UnexpectedFailedTestCaseNumber,
                        new List<int> { currentTestRunId }, true);

                    // If it was not 1 there's a good chance we read some random line as a failed test case hence consider it a
                    // no match since the number did not match what we were expecting anyway
                    if (testCaseNumber != 1)
                    {
                        return false;
                    }

                    // If the number was 1 then there's a good chance this is the beginning of the next test run, hence reset and start over
                    AttemptPublishAndResetParser($"was expecting failed test case or stack trace with number {stateContext.LastFailedTestCaseNumber} but found" +
                        $" {testCaseNumber} instead");
                }

                // Increment either ways whether it was expected or context was reset and the encountered number was 1
                stateContext.LastFailedTestCaseNumber++;

                // As of now we are ignoring stack traces
                if (stateContext.StackTracesToSkipParsingPostSummary > 0)
                {
                    stateContext.StackTracesToSkipParsingPostSummary--;
                    if (stateContext.StackTracesToSkipParsingPostSummary == 0)
                    {
                        // we can also choose to ignore extra failures post summary if the number is not 1
                        AttemptPublishAndResetParser();
                    }

                    return true;
                }

                // Also since this is an action performed in context of a state should there be a separate function?
                // Should this intelligence come from the caller?

                // If a failed test case is encountered while in the summary state it indicates either completion
                // or corruption of summary. Since Summary is Gospel to us, we will ignore the latter and publish
                // the run regardless. 
                if (state == MochaTestResultParserState.ExpectingTestRunSummary)
                {
                    AttemptPublishAndResetParser();
                }

                testResult.Outcome = TestOutcome.Failed;
                testResult.Name = match.Groups[RegexCaptureGroups.TestCaseName].Value;

                testRun.FailedTests.Add(testResult);

                return true;
            }

            return false;
        }

        private bool MatchPendingTestCase(LogLineData testResultsLine)
        {
            var match = MochaTestResultParserRegularExpressions.PendingTestCaseMatcher.Match(testResultsLine.Line);

            if (match.Success)
            {
                var testResult = new TestResult();

                testResult.Outcome = TestOutcome.Skipped;
                testResult.Name = match.Groups[RegexCaptureGroups.TestCaseName].Value;

                // Also since this is an action performed in context of a state should there be a separate function?
                // Should this intelligence come from the caller?

                switch (state)
                {
                    // If a pending test case is encountered while in the summary state it indicates either completion
                    // or corruption of summary. Since Summary is Gospel to us, we will ignore the latter and publish
                    // the run regardless. 
                    case MochaTestResultParserState.ExpectingTestRunSummary:

                        AttemptPublishAndResetParser();
                        break;

                    // If a pending test case is encountered while in the stack traces state it indicates corruption
                    // or incomplete stack trace data
                    case MochaTestResultParserState.ExpectingStackTraces:

                        // This check is safety check for when we try to parse stack trace contents
                        if (stateContext.StackTracesToSkipParsingPostSummary != 0)
                        {
                            logger.Error("MochaTestResultParser : Expecting stack traces but found pending test case instead.");
                            telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.ExpectingStackTracesButFoundPendingTest,
                                new List<int> { currentTestRunId }, true);
                        }

                        AttemptPublishAndResetParser();
                        break;
                }

                testRun.SkippedTests.Add(testResult);
                return true;
            }

            return false;
        }

        private bool MatchPassedSummary(LogLineData testResultsLine)
        {
            var match = MochaTestResultParserRegularExpressions.PassedTestsSummaryMatcher.Match(testResultsLine.Line);

            if (match.Success)
            {
                logger.Info($"MochaTestResultParser : Passed test summary encountered at line {testResultsLine.Line}.");

                stateContext.LinesWithinWhichMatchIsExpected = 1;
                stateContext.ExpectedMatch = "failed/pending tests summary";

                // Unexpected matches for Passed summary
                // We expect summary ideally only when we are in the first state.
                switch (state)
                {
                    case MochaTestResultParserState.ExpectingTestRunSummary:

                        logger.Error($"MochaTestResultParser : Was expecting atleast one test case before encountering" +
                            $" summary again at line {testResultsLine.LineNumber}");
                        telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.SummaryWithNoTestCases,
                            new List<int> { currentTestRunId }, true);

                        AttemptPublishAndResetParser();
                        break;

                    case MochaTestResultParserState.ExpectingStackTraces:

                        // If we were expecting more stack traces but got summary instead
                        if (stateContext.StackTracesToSkipParsingPostSummary != 0)
                        {
                            logger.Error("MochaTestResultParser : Expecting stack traces but found passed summary instead.");
                            telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.SummaryWithNoTestCases,
                                new List<int> { currentTestRunId }, true);
                        }

                        AttemptPublishAndResetParser();
                        break;
                }

                logger.Info("MochaTestResultParser : Transitioned to state ExpectingTestRunSummary.");

                state = MochaTestResultParserState.ExpectingTestRunSummary;
                stateContext.LastFailedTestCaseNumber = 0;

                if (!int.TryParse(match.Groups[RegexCaptureGroups.PassedTests].Value, out int totalPassed))
                {
                    logger.Error($"MochaTestResultParser : MatchPassedSummary : failed to parse total passed tests number" +
                        $" {match.Groups[RegexCaptureGroups.PassedTests].Value}. at line {testResultsLine.LineNumber}");
                }

                testRun.TestRunSummary.TotalPassed = totalPassed;

                // Fire telemetry if summary does not agree with parsed tests count
                if (testRun.TestRunSummary.TotalPassed != testRun.PassedTests.Count)
                {
                    logger.Error($"MochaTestResultParser : Passed tests count does not match passed summary" +
                        $" at line {testResultsLine.LineNumber}");
                    telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea,
                        TelemetryConstants.PassedSummaryMismatch, new List<int> { currentTestRunId }, true);
                }

                // Handle parse errors
                if (!long.TryParse(match.Groups[RegexCaptureGroups.TestRunTime].Value, out long timeTaken))
                {
                    logger.Error($"MochaTestResultParser : MatchPassedSummary : failed to parse test run time" +
                        $" {match.Groups[RegexCaptureGroups.TestRunTime].Value}. at line {testResultsLine.LineNumber}");
                }

                // Store time taken based on the unit used
                switch (match.Groups[RegexCaptureGroups.TestRunTimeUnit].Value)
                {
                    case "ms":
                        testRun.TestRunSummary.TotalExecutionTime = TimeSpan.FromMilliseconds(timeTaken);
                        break;

                    case "s":
                        testRun.TestRunSummary.TotalExecutionTime = TimeSpan.FromMilliseconds(timeTaken * 1000);
                        break;

                    case "m":
                        testRun.TestRunSummary.TotalExecutionTime = TimeSpan.FromMilliseconds(timeTaken * 60 * 1000);
                        break;

                    case "h":
                        testRun.TestRunSummary.TotalExecutionTime = TimeSpan.FromMilliseconds(timeTaken * 60 * 60 * 1000);
                        break;
                }

                return true;
            }

            return false;
        }

        private bool MatchFailedSummary(LogLineData testResultsLine)
        {
            var match = MochaTestResultParserRegularExpressions.FailedTestsSummaryMatcher.Match(testResultsLine.Line);

            if (match.Success)
            {
                logger.Info($"MochaTestResultParser : Failed tests summary encountered at line {testResultsLine.Line}.");

                stateContext.LinesWithinWhichMatchIsExpected = 0;

                // Handle parse errors
                if (!int.TryParse(match.Groups[RegexCaptureGroups.FailedTests].Value, out int totalFailed))
                {
                    logger.Error($"MochaTestResultParser : MatchFailedSummary : failed to parse total failed tests number" +
                        $" {match.Groups[RegexCaptureGroups.FailedTests].Value}. at line {testResultsLine.LineNumber}");
                }

                testRun.TestRunSummary.TotalFailed = totalFailed;
                stateContext.StackTracesToSkipParsingPostSummary = totalFailed;

                // If no failed tests found then skip the stack traces parsing state
                if (testRun.TestRunSummary.TotalFailed == 0)
                {
                    logger.Info("MochaTestResultParser : Transitioned to state ExpectingTestResults.");
                    state = MochaTestResultParserState.ExpectingTestResults;
                    AttemptPublishAndResetParser();
                }
                else
                {
                    logger.Info("MochaTestResultParser : Transitioned to state ExpectingStackTraces.");
                    state = MochaTestResultParserState.ExpectingStackTraces;
                }

                // If encountered failed tests does not match summary fire telemtry
                if (testRun.TestRunSummary.TotalFailed != testRun.FailedTests.Count)
                {
                    logger.Error($"MochaTestResultParser : Failed tests count does not match failed summary" +
                        $" at line {testResultsLine.LineNumber}");
                    telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea,
                        TelemetryConstants.PassedSummaryMismatch, new List<int> { currentTestRunId }, true);
                }

                return true;
            }

            return false;
        }

        private bool MatchPendingSummary(LogLineData testResultsLine)
        {
            var match = MochaTestResultParserRegularExpressions.PendingTestsSummaryMatcher.Match(testResultsLine.Line);

            if (match.Success)
            {
                logger.Info($"MochaTestResultParser : Pending tests summary encountered at line {testResultsLine.Line}.");

                stateContext.LinesWithinWhichMatchIsExpected = 1;

                // Handle parse error
                if (!int.TryParse(match.Groups[RegexCaptureGroups.PendingTests].Value, out int totalPending))
                {
                    logger.Error($"MochaTestResultParser : MatchPendingSummary : failed to parse total pending tests number" +
                        $" {match.Groups[RegexCaptureGroups.PendingTests].Value}. at line {testResultsLine.LineNumber}");
                }

                testRun.TestRunSummary.TotalSkipped = totalPending;

                // If encountered skipped tests does not match summary fire telemtry
                if (testRun.TestRunSummary.TotalSkipped != testRun.SkippedTests.Count)
                {
                    logger.Error($"MochaTestResultParser : Pending tests count does not match pending summary" +
                        $" at line {testResultsLine.LineNumber}");
                    telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea,
                        TelemetryConstants.PendingSummaryMismatch, new List<int> { currentTestRunId }, true);
                }

                return true;
            }

            return false;
        }
    }
}