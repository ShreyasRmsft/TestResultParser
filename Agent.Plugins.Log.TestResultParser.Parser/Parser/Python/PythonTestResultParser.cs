// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.Log.TestResultParser.Parser
{
    using System;
    using System.Collections.Generic;
    using Agent.Plugins.Log.TestResultParser.Contracts;

    /// <summary>
    /// Python test result parser.
    /// </summary>
    public class PythonTestResultParser : AbstractTestResultParser
    {
        private ParserState state;
        private TestResult partialTestResult;
        private TestRun currentTestRun;
        private int currentTestRunId = 1;

        public override string Name => "Python";
        public override string Version => "1.0";

        /// <summary>
        /// Default constructor accepting only test run manager instance, rest of the requirements assume default values
        /// </summary>
        public PythonTestResultParser(ITestRunManager testRunManager, ITraceLogger logger, ITelemetryDataCollector telemetry) : base(testRunManager, logger, telemetry)
        {
            base.logger.Info("PythonTestResultParser : Starting python test result parser.");
            base.telemetry.AddToCumulativeTelemetry(PythonTelemetryConstants.EventArea, PythonTelemetryConstants.Initialize, true);

            this.state = ParserState.ExpectingTestResults;
            this.currentTestRun = new TestRun($"{Name}/{Version}", this.currentTestRunId);
        }

        /// <summary>
        /// Parses input data to detect python test result.
        /// </summary>
        /// <param name="logData">Data to be parsed.</param>
        public override void Parse(LogData logData)
        {
            // Validate data input
            if (!IsValidInput(logData.Line) || string.IsNullOrWhiteSpace(logData.Line)) return;

            try
            {
                switch (this.state)
                {
                    case ParserState.ExpectingSummary:

                        // Summary Test count and total time should have already been parsed
                        // Try to parse test outcome, number of tests for each outcome
                        if (TryParseSummaryOutcome(logData))
                        {
                            PublishAndReset();
                            return;
                        }

                        // Summary was not parsed, reset the parser and try parse again.
                        Reset();
                        Parse(logData);
                        break;

                    case ParserState.ExpectingFailedResults:

                        // Try to parse for failed results and summary
                        // If summary is parsed, change the state
                        if (TryParseForFailedResult(logData)) return;
                        if (TryParseSummaryTestAndTime(logData))
                        {
                            this.state = ParserState.ExpectingSummary;
                            return;
                        }

                        // Not expected, as Summary has not been encountered yet
                        // If a new TestResult is found, reset the parser and Parse again
                        if (TryParseTestResult(logData))
                        {
                            this.logger.Error("PythonTestResultParser:Parse Expecting failed result or summary but found new test result.");
                            this.telemetry.AddToCumulativeTelemetry(PythonTelemetryConstants.EventArea, PythonTelemetryConstants.SummaryOrFailedTestsNotFound, new List<int> { this.currentTestRunId }, true);
                            Reset();
                            Parse(logData);
                        }
                        break;

                    case ParserState.ExpectingTestResults:
                    default:
                        if (TryParseTestResult(logData)) return;

                        // Change the state and clear the partial result if failed result or summary is found
                        if (TryParseForFailedResult(logData))
                        {
                            this.partialTestResult = null;
                            this.state = ParserState.ExpectingFailedResults;
                            return;
                        }
                        if (TryParseSummaryTestAndTime(logData))
                        {
                            this.partialTestResult = null;
                            this.state = ParserState.ExpectingSummary;
                            return;
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                this.logger.Error($"PythonTestResultParser.Parse : Unable to parse the log line {logData.Line} with exception {ex.ToString()}");
                this.telemetry.AddToCumulativeTelemetry(PythonTelemetryConstants.EventArea, PythonTelemetryConstants.ParseException, ex.Message);

                Reset();
            }
        }

        /// <summary>
        /// Reset the parser to original state
        /// </summary>
        private void Reset()
        {
            this.logger.Info("PythonTestResultParser.Reset");
            this.partialTestResult = null;
            this.currentTestRun = new TestRun($"{Name}/{Version}", ++this.currentTestRunId);
            this.state = ParserState.ExpectingTestResults;
        }

        /// <summary>
        /// Publish the current test run and reset the parser
        /// </summary>
        private void PublishAndReset()
        {
            this.logger.Info($"PythonTestResultParser:PublishAndReset : Publishing TestRun {this.currentTestRunId}");
            this.testRunManager.PublishAsync(this.currentTestRun);
            Reset();
        }

        private bool TryParseTestResult(LogData logData)
        {
            var resultMatch = PythonRegexes.TestResult.Match(logData.Line);

            if (!resultMatch.Success)
            {
                return this.partialTestResult == null ? false : TryParseForPartialResult(logData);
            }

            this.partialTestResult = null;

            var testCaseNameIdentifier = resultMatch.Groups[RegexCaptureGroups.TestCaseName].Value.Trim();
            string testCaseName = GetResultName(logData, testCaseNameIdentifier);

            if (testCaseName == null) return false;

            var result = new TestResult() { Name = testCaseName };

            // Determine the outcome of the Test result
            var testOutcomeIdentifier = resultMatch.Groups[RegexCaptureGroups.TestOutcome].Value.Trim();
            var passedResultMatch = PythonRegexes.PassedOutcome.Match(testOutcomeIdentifier);
            if (passedResultMatch.Success)
            {
                result.Outcome = TestOutcome.Passed;
                this.currentTestRun.PassedTests.Add(result);
                return true;
            }

            var skippedResultMatch = PythonRegexes.SkippedOutcome.Match(testOutcomeIdentifier);
            if (skippedResultMatch.Success)
            {
                result.Outcome = TestOutcome.NotExecuted;
                this.currentTestRun.SkippedTests.Add(result);
                return true;
            }

            // The outcome for this result could not be determined, adding to partial result
            this.partialTestResult = result;
            return true;
        }

        private bool TryParseForPartialResult(LogData logData)
        {
            var partialResultMatch = PythonRegexes.PassedOutcome.Match(logData.Line);
            if (partialResultMatch.Success)
            {
                this.partialTestResult.Outcome = TestOutcome.Passed;
                this.currentTestRun.PassedTests.Add(this.partialTestResult);
                return true;
            }
            return false;
        }

        private bool TryParseForFailedResult(LogData logData)
        {
            // Parse
            var failedResultMatch = PythonRegexes.FailedResult.Match(logData.Line);
            if (!failedResultMatch.Success) { return false; }

            // Set result name.
            string resultNameIdentifier = failedResultMatch.Groups[RegexCaptureGroups.TestCaseName].Value.Trim();

            var result = new TestResult();
            result.Name = GetResultName(logData, resultNameIdentifier);
            result.Outcome = TestOutcome.Failed;

            this.currentTestRun.FailedTests.Add(result);
            return true;
        }

        private string GetResultName(LogData logData, string testResultNameIdentifier)
        {
            if (string.IsNullOrWhiteSpace(testResultNameIdentifier))
            {
                this.logger.Verbose($"Test result name is null or whitespace in logData: {logData.Line}");
                return null;
            }

            return testResultNameIdentifier;
        }

        private bool TryParseSummaryTestAndTime(LogData logData)
        {
            var countAndTimeSummaryMatch = PythonRegexes.TestCountAndTimeSummary.Match(logData.Line);
            if (countAndTimeSummaryMatch.Success)
            {
                var testcount = int.Parse(countAndTimeSummaryMatch.Groups[RegexCaptureGroups.TotalTests].Value);
                var secTime = int.Parse(countAndTimeSummaryMatch.Groups[RegexCaptureGroups.TestRunTime].Value);
                var msTime = int.Parse(countAndTimeSummaryMatch.Groups[RegexCaptureGroups.TestRunTimeMs].Value);

                this.currentTestRun.TestRunSummary = new TestRunSummary
                {
                    TotalExecutionTime = new TimeSpan(0, 0, 0, secTime, msTime),
                    TotalTests = testcount
                };
                this.logger.Info("PythonTestResultParser:TryParseSummaryTestAndTime : TestRunSummary with total time and tests created.");
                return true;
            }

            return false;
        }

        private bool TryParseSummaryOutcome(LogData logData)
        {
            if (this.currentTestRun.TestRunSummary == null)
            {
                // This is safe check, if must be true always because parsers will try to parse for Outcome if Test and Time Summary already parsed.
                this.logger.Error("PythonTestResultParser:TryParseSummaryOutcome : TestRunSummary is null");
                this.telemetry.AddToCumulativeTelemetry(PythonTelemetryConstants.EventArea, PythonTelemetryConstants.TestRunSummaryCorrupted, new List<int> { this.currentTestRunId }, true);
                return false;
            }

            var resultSummaryMatch = PythonRegexes.TestOutcomeSummary.Match(logData.Line);
            if (resultSummaryMatch.Success)
            {
                var resultIdentifer = resultSummaryMatch.Groups[RegexCaptureGroups.TestOutcome].Value;

                var failureCountPatternMatch = PythonRegexes.SummaryFailure.Match(resultIdentifer);
                if (failureCountPatternMatch.Success)
                {
                    this.currentTestRun.TestRunSummary.TotalFailed = int.Parse(failureCountPatternMatch.Groups[RegexCaptureGroups.FailedTests].Value);
                }

                // TODO: We should have a separate bucket for errors
                var errorCountPatternMatch = PythonRegexes.SummaryErrors.Match(resultIdentifer);
                if (errorCountPatternMatch.Success)
                {
                    this.currentTestRun.TestRunSummary.TotalFailed += int.Parse(errorCountPatternMatch.Groups[RegexCaptureGroups.Errors].Value);
                }

                var skippedCountPatternMatch = PythonRegexes.SummarySkipped.Match(resultIdentifer);
                if (skippedCountPatternMatch.Success)
                {
                    this.currentTestRun.TestRunSummary.TotalSkipped = int.Parse(skippedCountPatternMatch.Groups[RegexCaptureGroups.SkippedTests].Value);
                }

                // Since total passed count is not available, calculate the count based on available statistics.
                this.currentTestRun.TestRunSummary.TotalPassed = this.currentTestRun.TestRunSummary.TotalTests - (this.currentTestRun.TestRunSummary.TotalFailed + this.currentTestRun.TestRunSummary.TotalSkipped);
                return true;
            }

            this.logger.Error("PythonTestResultParser:TryParseSummaryOutcome : Expected match for SummaryTestOutcome was not found");
            this.telemetry.AddToCumulativeTelemetry(PythonTelemetryConstants.EventArea, PythonTelemetryConstants.TestOutcomeSummaryNotFound, new List<int> { this.currentTestRunId }, true);
            return false;
        }

        /// <summary>
        /// Validate the input data
        /// </summary>
        /// <param name="data">Log line that was passed to the parser</param>
        /// <returns>True if valid</returns>
        private bool IsValidInput(string data)
        {
            if (data == null)
            {
                this.logger.Error("PythonTestResultParser.IsValidInput : Received null data");
                this.telemetry.AddToCumulativeTelemetry(PythonTelemetryConstants.EventArea, PythonTelemetryConstants.InvalidInput, new List<int> { this.currentTestRunId }, true);
            }

            return data != null;
        }
    }
}
