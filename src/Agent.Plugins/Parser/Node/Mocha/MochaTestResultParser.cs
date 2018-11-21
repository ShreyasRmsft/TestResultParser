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

        public string Name => nameof(MochaTestResultParser);

        public string Version => "1.0";

        /// <summary>
        /// Default constructor accepting only test run manager instance, rest of the requirements assume default values
        /// </summary>
        /// <param name="testRunManager"></param>
        public MochaTestResultParser(ITestRunManager testRunManager) : this(testRunManager, TraceLogger.Instance, TelemetryDataCollector.Instance)
        {

        }

        /// <summary>
        /// Detailed constructor where specified logger and telemetry data collector are initialized along with test run manager
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
            this.testRun = new TestRun($"{Name}/{Version}", this.currentTestRunId);
            this.stateContext = new MochaTestResultParserStateContext();
            this.state = MochaTestResultParserState.ExpectingTestResults;
        }

        /// <inheritdoc/>
        public void Parse(LogData testResultsLine)
        {
            // State model for the mocha parser that defines the regexes to match against in each state
            // Each state re-orders the regexes based on the frequency of expected matches
            switch (this.state)
            {
                // This state primarily looks for test results 
                // and transitions to the next one after a line of summary is encountered
                case MochaTestResultParserState.ExpectingTestResults:

                    if (MatchPassedTestCase(testResultsLine))
                    {
                        return;
                    }
                    if (MatchFailedTestCase(testResultsLine))
                    {
                        return;
                    }
                    if (MatchPendingTestCase(testResultsLine))
                    {
                        return;
                    }
                    if (MatchPassedSummary(testResultsLine))
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
                    if (MatchFailedSummary(testResultsLine))
                    {
                        return;
                    }
                    if (MatchPassedTestCase(testResultsLine))
                    {
                        return;
                    }
                    if (MatchFailedTestCase(testResultsLine))
                    {
                        return;
                    }
                    if (MatchPendingTestCase(testResultsLine))
                    {
                        return;
                    }
                    if (MatchPassedSummary(testResultsLine))
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
                    if (MatchPassedTestCase(testResultsLine))
                    {
                        return;
                    }
                    if (MatchPendingTestCase(testResultsLine))
                    {
                        return;
                    }
                    if (MatchPassedSummary(testResultsLine))
                    {
                        return;
                    }

                    break;
            }

            // This is a mechanism to enforce matches that have to occur within 
            // a specific number of lines after encountering the previous match
            // one obvious usage is for successive summary lines containing passed,
            // pending and failed test summary
            if (this.stateContext.LinesWithinWhichMatchIsExpected == 1)
            {
                AttemptPublishAndResetParser($"was expecting {this.stateContext.ExpectedMatch} before line {testResultsLine.LineNumber} but no matches occurred");
                return;
            }

            if (this.stateContext.LinesWithinWhichMatchIsExpected > 1)
            {
                this.stateContext.LinesWithinWhichMatchIsExpected--;
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
                this.logger.Info($"MochaTestResultParser : Resetting the parser and attempting to publishing the test run : {reason}.");
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea,
                    TelemetryConstants.AttemptPublishAndResetParser, new List<string> { reason }, true);
            }

            PublishTestRun();

            ResetParser();
        }

        private void PublishTestRun()
        {
            // We have encountered failed test cases but no failed summary was encountered
            if (this.testRun.FailedTests.Count != 0 && this.testRun.TestRunSummary.TotalFailed == 0)
            {
                this.logger.Error("MochaTestResultParser : Failed tests were encountered but no failed summary was encountered.");
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea,
                    TelemetryConstants.FailedTestCasesFoundButNoFailedSummary, new List<int> { this.currentTestRunId }, true);
            }

            // We have encountered pending test cases but no pending summary was encountered
            if (this.testRun.SkippedTests.Count != 0 && this.testRun.TestRunSummary.TotalSkipped == 0)
            {
                this.logger.Error("MochaTestResultParser : Skipped tests were encountered but no skipped summary was encountered.");
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea,
                    TelemetryConstants.PendingTestCasesFoundButNoFailedSummary, new List<int> { this.currentTestRunId }, true);
            }

            // Ensure some summary data was detected before attempting a publish, ie. check if the state is not test results state
            switch (this.state)
            {
                case MochaTestResultParserState.ExpectingTestResults:
                    if (this.testRun.PassedTests.Count != 0)
                    {
                        this.logger.Error("MochaTestResultParser : Passed tests were encountered but no passed summary was encountered.");
                        this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea,
                            TelemetryConstants.PassedTestCasesFoundButNoPassedSummary, new List<int> { this.currentTestRunId }, true);
                    }
                    break;

                default:
                    // Publish the test run if reset and publish was called from any state other than the test results state
                    this.testRunManager.Publish(this.testRun);
                    this.currentTestRunId++;
                    break;
            }
        }

        private void ResetParser()
        {
            // Refresh the context
            this.stateContext = new MochaTestResultParserStateContext();

            // Start a new TestRun
            this.testRun = new TestRun($"{Name}/{Version}", this.currentTestRunId);
            this.state = MochaTestResultParserState.ExpectingTestResults;

            this.logger.Info("MochaTestResultParser : Successfully reset the parser.");
        }

        /// <summary>
        /// Matches a line of input with the passed test case regex and performs appropriate actions 
        /// on a successful match
        /// </summary>
        /// <param name="testResultsLine"></param>
        /// <returns></returns>
        private bool MatchPassedTestCase(LogData testResultsLine)
        {
            var match = MochaTestResultParserRegexes.PassedTestCase.Match(testResultsLine.Line);

            if (!match.Success)
            {
                return false;
            }

            var testResult = new TestResult();

            testResult.Outcome = TestOutcome.Passed;
            testResult.Name = match.Groups[RegexCaptureGroups.TestCaseName].Value;

            // Also since this is an action performed in context of a state should there be a separate function?
            // Should this intelligence come from the caller?

            switch (this.state)
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
                    if (this.stateContext.StackTracesToSkipParsingPostSummary != 0)
                    {
                        this.logger.Error($"MochaTestResultParser : Expecting stack traces but found passed test case instead at line {testResultsLine.LineNumber}.");
                        this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.ExpectingStackTracesButFoundPassedTest,
                            new List<int> { this.currentTestRunId }, true);
                    }

                    AttemptPublishAndResetParser();
                    break;
            }

            this.testRun.PassedTests.Add(testResult);
            return true;
        }

        private bool MatchFailedTestCase(LogData testResultsLine)
        {
            var match = MochaTestResultParserRegexes.FailedTestCase.Match(testResultsLine.Line);

            if (!match.Success)
            {
                return false;
            }

            var testResult = new TestResult();

            // Handling parse errors is unnecessary
            int.TryParse(match.Groups[RegexCaptureGroups.FailedTestCaseNumber].Value, out int testCaseNumber);

            // In the event the failed test case number does not match the expected test case number log an error and move on
            if (testCaseNumber != this.stateContext.LastFailedTestCaseNumber + 1)
            {
                this.logger.Error($"MochaTestResultParser : Expecting failed test case or stack trace with" +
                    $" number {this.stateContext.LastFailedTestCaseNumber + 1} but found {testCaseNumber} instead");
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.UnexpectedFailedTestCaseNumber,
                    new List<int> { this.currentTestRunId }, true);

                // If it was not 1 there's a good chance we read some random line as a failed test case hence consider it a
                // no match since the number did not match what we were expecting anyway
                if (testCaseNumber != 1)
                {
                    return false;
                }

                // If the number was 1 then there's a good chance this is the beginning of the next test run, hence reset and start over
                AttemptPublishAndResetParser($"was expecting failed test case or stack trace with number {this.stateContext.LastFailedTestCaseNumber} but found" +
                    $" {testCaseNumber} instead");
            }

            // Increment either ways whether it was expected or context was reset and the encountered number was 1
            this.stateContext.LastFailedTestCaseNumber++;

            // As of now we are ignoring stack traces
            if (this.stateContext.StackTracesToSkipParsingPostSummary > 0)
            {
                this.stateContext.StackTracesToSkipParsingPostSummary--;
                if (this.stateContext.StackTracesToSkipParsingPostSummary == 0)
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
            if (this.state == MochaTestResultParserState.ExpectingTestRunSummary)
            {
                AttemptPublishAndResetParser();
            }

            testResult.Outcome = TestOutcome.Failed;
            testResult.Name = match.Groups[RegexCaptureGroups.TestCaseName].Value;

            this.testRun.FailedTests.Add(testResult);

            return true;
        }

        private bool MatchPendingTestCase(LogData testResultsLine)
        {
            var match = MochaTestResultParserRegexes.PendingTestCase.Match(testResultsLine.Line);

            if (!match.Success)
            {
                return false;
            }

            var testResult = new TestResult();

            testResult.Outcome = TestOutcome.Skipped;
            testResult.Name = match.Groups[RegexCaptureGroups.TestCaseName].Value;

            // Also since this is an action performed in context of a state should there be a separate function?
            // Should this intelligence come from the caller?

            switch (this.state)
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
                    if (this.stateContext.StackTracesToSkipParsingPostSummary != 0)
                    {
                        this.logger.Error("MochaTestResultParser : Expecting stack traces but found pending test case instead.");
                        this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.ExpectingStackTracesButFoundPendingTest,
                            new List<int> { this.currentTestRunId }, true);
                    }

                    AttemptPublishAndResetParser();
                    break;
            }

            this.testRun.SkippedTests.Add(testResult);
            return true;
        }

        private bool MatchPassedSummary(LogData testResultsLine)
        {
            var match = MochaTestResultParserRegexes.PassedTestsSummary.Match(testResultsLine.Line);

            if (!match.Success)
            {
                return false;
            }

            this.logger.Info($"MochaTestResultParser : Passed test summary encountered at line {testResultsLine.LineNumber}.");

            // Unexpected matches for Passed summary
            // We expect summary ideally only when we are in the first state.
            switch (this.state)
            {
                case MochaTestResultParserState.ExpectingTestRunSummary:

                    this.logger.Error($"MochaTestResultParser : Was expecting atleast one test case before encountering" +
                        $" summary again at line {testResultsLine.LineNumber}");
                    this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.SummaryWithNoTestCases,
                        new List<int> { this.currentTestRunId }, true);

                    AttemptPublishAndResetParser();
                    break;

                case MochaTestResultParserState.ExpectingStackTraces:

                    // If we were expecting more stack traces but got summary instead
                    if (this.stateContext.StackTracesToSkipParsingPostSummary != 0)
                    {
                        this.logger.Error("MochaTestResultParser : Expecting stack traces but found passed summary instead.");
                        this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.SummaryWithNoTestCases,
                            new List<int> { this.currentTestRunId }, true);
                    }

                    AttemptPublishAndResetParser();
                    break;
            }

            this.stateContext.LinesWithinWhichMatchIsExpected = 1;
            this.stateContext.ExpectedMatch = "failed/pending tests summary";
            this.state = MochaTestResultParserState.ExpectingTestRunSummary;
            this.stateContext.LastFailedTestCaseNumber = 0;

            this.logger.Info("MochaTestResultParser : Transitioned to state ExpectingTestRunSummary.");

            // Handling parse errors is unnecessary
            int.TryParse(match.Groups[RegexCaptureGroups.PassedTests].Value, out int totalPassed);

            this.testRun.TestRunSummary.TotalPassed = totalPassed;

            // Fire telemetry if summary does not agree with parsed tests count
            if (this.testRun.TestRunSummary.TotalPassed != this.testRun.PassedTests.Count)
            {
                this.logger.Error($"MochaTestResultParser : Passed tests count does not match passed summary" +
                    $" at line {testResultsLine.LineNumber}");
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea,
                    TelemetryConstants.PassedSummaryMismatch, new List<int> { this.currentTestRunId }, true);
            }

            // Handling parse errors is unnecessary
            long.TryParse(match.Groups[RegexCaptureGroups.TestRunTime].Value, out long timeTaken);

            // Store time taken based on the unit used
            switch (match.Groups[RegexCaptureGroups.TestRunTimeUnit].Value)
            {
                case "ms":
                    this.testRun.TestRunSummary.TotalExecutionTime = TimeSpan.FromMilliseconds(timeTaken);
                    break;

                case "s":
                    this.testRun.TestRunSummary.TotalExecutionTime = TimeSpan.FromMilliseconds(timeTaken * 1000);
                    break;

                case "m":
                    this.testRun.TestRunSummary.TotalExecutionTime = TimeSpan.FromMilliseconds(timeTaken * 60 * 1000);
                    break;

                case "h":
                    this.testRun.TestRunSummary.TotalExecutionTime = TimeSpan.FromMilliseconds(timeTaken * 60 * 60 * 1000);
                    break;
            }

            return true;
        }

        private bool MatchFailedSummary(LogData testResultsLine)
        {
            var match = MochaTestResultParserRegexes.FailedTestsSummary.Match(testResultsLine.Line);

            if (!match.Success)
            {
                return false;
            }

            this.logger.Info($"MochaTestResultParser : Failed tests summary encountered at line {testResultsLine.LineNumber}.");

            this.stateContext.LinesWithinWhichMatchIsExpected = 0;

            // Handling parse errors is unnecessary
            int.TryParse(match.Groups[RegexCaptureGroups.FailedTests].Value, out int totalFailed);

            this.testRun.TestRunSummary.TotalFailed = totalFailed;
            this.stateContext.StackTracesToSkipParsingPostSummary = totalFailed;

            
            this.logger.Info("MochaTestResultParser : Transitioned to state ExpectingStackTraces.");
            this.state = MochaTestResultParserState.ExpectingStackTraces;

            // If encountered failed tests does not match summary fire telemtry
            if (this.testRun.TestRunSummary.TotalFailed != this.testRun.FailedTests.Count)
            {
                this.logger.Error($"MochaTestResultParser : Failed tests count does not match failed summary" +
                    $" at line {testResultsLine.LineNumber}");
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea,
                    TelemetryConstants.PassedSummaryMismatch, new List<int> { this.currentTestRunId }, true);
            }

            return true;
        }

        private bool MatchPendingSummary(LogData testResultsLine)
        {
            var match = MochaTestResultParserRegexes.PendingTestsSummary.Match(testResultsLine.Line);

            if (!match.Success)
            {
                return false;
            }

            this.logger.Info($"MochaTestResultParser : Pending tests summary encountered at line {testResultsLine.LineNumber}.");

            this.stateContext.LinesWithinWhichMatchIsExpected = 1;

            // Handling parse errors is unnecessary
            int.TryParse(match.Groups[RegexCaptureGroups.PendingTests].Value, out int totalPending);

            this.testRun.TestRunSummary.TotalSkipped = totalPending;

            // If encountered skipped tests does not match summary fire telemtry
            if (this.testRun.TestRunSummary.TotalSkipped != this.testRun.SkippedTests.Count)
            {
                this.logger.Error($"MochaTestResultParser : Pending tests count does not match pending summary" +
                    $" at line {testResultsLine.LineNumber}");
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea,
                    TelemetryConstants.PendingSummaryMismatch, new List<int> { this.currentTestRunId }, true);
            }

            return true;
        }
    }
}