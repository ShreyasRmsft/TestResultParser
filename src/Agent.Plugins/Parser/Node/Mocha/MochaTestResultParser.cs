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
            logger.Verbose("MochaTestResultParser.MochaTestResultParser : Starting mocha test result parser.");
            telemetryDataCollector.AddProperty("TODO", "TODO");

            this.testRunManager = testRunManager;
            this.logger = logger;
            this.telemetryDataCollector = telemetryDataCollector;

            // Initialize the starting state of the parser
            testRun = new TestRun { FailedTests = new List<TestResult>(), PassedTests = new List<TestResult>(), SkippedTests = new List<TestResult>(), TestRunSummary = new TestRunSummary() };
            stateContext = new MochaTestResultParserStateContext();
            state = MochaTestResultParserState.ExpectingTestResults;

            testRun.ParserUri = $"{ParserName}/{ParserVersion}";
        }

        /// <inheritdoc/>
        public void Parse(LogLineData testResultsLine)
        {
            if(testResultsLine.Line.Contains("Do not sign or zipalign if nothing is selected"))
            {

            }

            // State model for the mocha parser that defines the regexes to match against in each state
            // Each state re-orders the regexes based on the frequency of expected matches
            switch (state)
            {
                // This state primarily looks for test results 
                // and transitions to the next one after a line of summary is encountered
                case MochaTestResultParserState.ExpectingTestResults:

                    if (MatchPassedTestCase(testResultsLine.Line))
                    {
                        return;
                    }
                    else if (MatchFailedTestCase(testResultsLine.Line))
                    {
                        return;
                    }
                    else if (MatchPendingTestCase(testResultsLine.Line))
                    {
                        return;
                    }
                    else if (MatchPassedSummary(testResultsLine.Line))
                    {
                        return;
                    }

                    break;

                // This state primarily looks for test run summary 
                // If failed tests were found to be present transitions to the next one to look for stack traces
                // else goes back to the first state after publishing the run
                case MochaTestResultParserState.ExpectingTestRunSummary:

                    if (MatchPendingSummary(testResultsLine.Line))
                    {
                        return;
                    }
                    else if (MatchFailedSummary(testResultsLine.Line))
                    {
                        return;
                    }
                    else if (MatchPassedTestCase(testResultsLine.Line))
                    {
                        return;
                    }
                    else if (MatchFailedTestCase(testResultsLine.Line))
                    {
                        return;
                    }
                    else if (MatchPendingTestCase(testResultsLine.Line))
                    {
                        return;
                    }
                    else if (MatchPassedSummary(testResultsLine.Line))
                    {
                        return;
                    }

                    break;

                // This state primarily looks for stack traces
                // If any other match occurs before all the expected stack traces are found it 
                // fires telemetry for unexpected behavior but moves on to the next test run
                case MochaTestResultParserState.ExpectingStackTraces:

                    if (MatchFailedTestCase(testResultsLine.Line))
                    {
                        return;
                    }
                    else if (MatchPassedTestCase(testResultsLine.Line))
                    {
                        return;
                    }
                    else if (MatchPendingTestCase(testResultsLine.Line))
                    {
                        return;
                    }
                    else if (MatchPassedSummary(testResultsLine.Line))
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
                logger.Verbose("TODO" + stateContext.ExpectedMatch);
                PublishRunAndResetParser();
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
        private void PublishRunAndResetParser()
        {
            // TODO: If a reset is implemented
            // this can still blindly pump incomplete test runs, validator needs to ensure it verifies and fires telemtry

            // Refresh the context
            stateContext = new MochaTestResultParserStateContext();

            // Publish the test run
            testRunManager.Publish(testRun);

            // Start a new TestRun
            testRun = new TestRun() { FailedTests = new List<TestResult>(), PassedTests = new List<TestResult>(), SkippedTests = new List<TestResult>(), TestRunSummary = new TestRunSummary() };
            testRun.ParserUri = $"{ParserName}/{ParserVersion}";
            state = MochaTestResultParserState.ExpectingTestResults;

            logger.Verbose("TODO");
            telemetryDataCollector.AddProperty("TODO", "TODO");
        }

        /// <summary>
        /// Matches a line of input with the passed test case regex and performs appropriate actions 
        /// on a successful match
        /// </summary>
        /// <param name="testResultsLine"></param>
        /// <returns></returns>
        private bool MatchPassedTestCase(string testResultsLine)
        {
            var match = MochaTestResultParserRegularExpressions.PassedTestCaseMatcher.Match(testResultsLine);

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

                        PublishRunAndResetParser();
                        break;

                    // If a passed test case is encountered while in the stack traces state it indicates corruption
                    // or incomplete stack trace data
                    case MochaTestResultParserState.ExpectingStackTraces:

                        // This check is safety check for when we try to parse stack trace contents
                        if (stateContext.StackTracesToSkipParsingPostSummary != 0)
                        {
                            logger.Error("TODO");
                            telemetryDataCollector.AddProperty("TODO", "TODO");
                        }

                        PublishRunAndResetParser();
                        break;
                }

                testRun.PassedTests.Add(testResult);
                return true;
            }

            return false;
        }

        private bool MatchFailedTestCase(string testResultsLine)
        {
            var match = MochaTestResultParserRegularExpressions.FailedTestCaseMatcher.Match(testResultsLine);

            if (match.Success)
            {
                var testResult = new TestResult();

                // Handle parse errors
                if (!int.TryParse(match.Groups[RegexCaptureGroups.FailedTestCaseNumber].Value, out int testCaseNumber))
                {
                    logger.Error("TODO");
                    telemetryDataCollector.AddProperty("TODO", "TODO");
                }

                // In the event the failed test case numebr does not match the expected test case number log an error and move on
                if (testCaseNumber != stateContext.LastFailedTestCaseNumber + 1)
                {
                    // TODO: Start a new test run here as this could potentially be the beginning of the next run??
                    logger.Error("TODO");
                    telemetryDataCollector.AddProperty("TODO", "TODO");
                }
                else
                {
                    // TODO: This else can go away and the line always executed depending on the action taken above
                    stateContext.LastFailedTestCaseNumber++;
                }

                // As of now we are ignoring stack traces
                if (stateContext.StackTracesToSkipParsingPostSummary > 0)
                {
                    stateContext.StackTracesToSkipParsingPostSummary--;
                    if (stateContext.StackTracesToSkipParsingPostSummary == 0)
                    {
                        // we can also choose to ignore extra failures post summary if the number is not 1
                        PublishRunAndResetParser();
                    }

                    return true;
                }

                // Also since this is an action performed in context of a state should there be a separate function?
                // Should this intelligence come from the caller?

                // If a passed test case is encountered while in the summary state it indicates either completion
                // or corruption of summary. Since Summary is Gospel to us, we will ignore the latter and publish
                // the run regardless. 
                if (state == MochaTestResultParserState.ExpectingTestRunSummary)
                {
                    PublishRunAndResetParser();
                }

                testResult.Outcome = TestOutcome.Failed;
                testResult.Name = match.Groups[RegexCaptureGroups.TestCaseName].Value;

                testRun.FailedTests.Add(testResult);

                return true;
            }

            return false;
        }

        private bool MatchPendingTestCase(string testResultsLine)
        {
            var match = MochaTestResultParserRegularExpressions.PendingTestCaseMatcher.Match(testResultsLine);

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

                        PublishRunAndResetParser();
                        break;

                    // If a pending test case is encountered while in the stack traces state it indicates corruption
                    // or incomplete stack trace data
                    case MochaTestResultParserState.ExpectingStackTraces:

                        // This check is safety check for when we try to parse stack trace contents
                        if (stateContext.StackTracesToSkipParsingPostSummary != 0)
                        {
                            logger.Error("TODO");
                            telemetryDataCollector.AddProperty("TODO", "TODO");
                        }

                        PublishRunAndResetParser();
                        break;
                }

                testRun.SkippedTests.Add(testResult);
                return true;
            }

            return false;
        }

        private bool MatchPassedSummary(string testResultsLine)
        {
            var match = MochaTestResultParserRegularExpressions.PassedTestsSummaryMatcher.Match(testResultsLine);

            if (match.Success)
            {
                logger.Verbose("TODO");
                telemetryDataCollector.AddProperty("TODO", "TODO");

                stateContext.LinesWithinWhichMatchIsExpected = 1;
                stateContext.ExpectedMatch = "failed/pending tests summary";

                // Unexpected matches for Passed summary
                // We expect summary ideally only when we are in the first state.
                switch (state)
                {
                    case MochaTestResultParserState.ExpectingTestRunSummary:

                        // We have encountered failed test cases but no failed summary was encountered
                        if (testRun.FailedTests.Count != 0)
                        {
                            logger.Error("TODO");
                            telemetryDataCollector.AddProperty("TODO", "TODO");
                        }

                        PublishRunAndResetParser();
                        break;

                    case MochaTestResultParserState.ExpectingStackTraces:

                        // If we were expecting more stack traces but got summary instead
                        if (stateContext.StackTracesToSkipParsingPostSummary != 0)
                        {
                            logger.Error("TODO");
                            telemetryDataCollector.AddProperty("TODO", "TODO");
                        }

                        PublishRunAndResetParser();
                        break;
                }

                logger.Verbose("TODO");

                state = MochaTestResultParserState.ExpectingTestRunSummary;
                stateContext.LastFailedTestCaseNumber = 0;

                if (!int.TryParse(match.Groups[RegexCaptureGroups.PassedTests].Value, out int totalPassed))
                {
                    logger.Error("TODO");
                    telemetryDataCollector.AddProperty("TODO", "TODO");
                }

                testRun.TestRunSummary.TotalPassed = totalPassed;

                // Fire telemetry if summary does not agree with parsed tests count
                if (testRun.TestRunSummary.TotalPassed != testRun.PassedTests.Count)
                {
                    logger.Error("TODO");
                    telemetryDataCollector.AddProperty("TODO", "TODO");
                }

                // Handle parse errors
                if (!long.TryParse(match.Groups[RegexCaptureGroups.TestRunTime].Value, out long timeTaken))
                {
                    logger.Error("TODO");
                    telemetryDataCollector.AddProperty("TODO", "TODO");
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

        private bool MatchFailedSummary(string testResultsLine)
        {
            var match = MochaTestResultParserRegularExpressions.FailedTestsSummaryMatcher.Match(testResultsLine);

            if (match.Success)
            {
                logger.Verbose("TODO");
                telemetryDataCollector.AddProperty("TODO", "TODO");

                stateContext.LinesWithinWhichMatchIsExpected = 0;

                // Handle parse errors
                if (!int.TryParse(match.Groups[RegexCaptureGroups.FailedTests].Value, out int totalFailed))
                {
                    logger.Error("TODO");
                    telemetryDataCollector.AddProperty("TODO", "TODO");
                }

                testRun.TestRunSummary.TotalFailed = totalFailed;
                stateContext.StackTracesToSkipParsingPostSummary = totalFailed;

                // If no failed tests found then skip the stack traces parsing state
                if (testRun.TestRunSummary.TotalFailed == 0)
                {
                    logger.Verbose("TODO");
                    state = MochaTestResultParserState.ExpectingTestResults;
                    PublishRunAndResetParser();
                }
                else
                {
                    logger.Verbose("TODO");
                    state = MochaTestResultParserState.ExpectingStackTraces;
                }

                // If encountered failed tests does not match summary fire telemtry
                if (testRun.TestRunSummary.TotalFailed != testRun.FailedTests.Count)
                {
                    logger.Error("TODO");
                    telemetryDataCollector.AddProperty("TODO", "TODO");
                }

                return true;
            }

            return false;
        }

        private bool MatchPendingSummary(string testResultsLine)
        {
            var match = MochaTestResultParserRegularExpressions.PendingTestsSummaryMatcher.Match(testResultsLine);

            if (match.Success)
            {
                logger.Verbose("TODO");
                telemetryDataCollector.AddProperty("TODO", "TODO");

                stateContext.LinesWithinWhichMatchIsExpected = 1;

                // Handle parse error
                if (!int.TryParse(match.Groups[RegexCaptureGroups.PendingTests].Value, out int totalPending))
                {
                    logger.Error("TODO");
                    telemetryDataCollector.AddProperty("TODO", "TODO");
                }

                testRun.TestRunSummary.TotalSkipped = totalPending;

                // If encountered skipped tests does not match summary fire telemtry
                if (testRun.TestRunSummary.TotalSkipped != testRun.SkippedTests.Count)
                {
                    logger.Error("TODO");
                    telemetryDataCollector.AddProperty("TODO", "TODO");
                }

                return true;
            }

            return false;
        }
    }
}