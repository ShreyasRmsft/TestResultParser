// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.Log.TestResultParser.Parser
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
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

        private bool captureStackTrace = false;
        private int stackTraceLinesAllowedToParse = -1;

        public override string Name => "Python";

        public override string Version => "1.0";

        /// <summary>
        /// Default constructor accepting only test run manager instance, rest of the requirements assume default values
        /// </summary>
        public PythonTestResultParser(ITestRunManager testRunManager, ITraceLogger logger, ITelemetryDataCollector telemetry) : base(testRunManager, logger, telemetry)
        {
            base._logger.Info("PythonTestResultParser : Starting python test result parser.");
            base._telemetry.AddToCumulativeTelemetry(PythonTelemetryConstants.EventArea, PythonTelemetryConstants.Initialize, true);

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
            if (!IsValidInput(logData.Line))
            {
                return;
            }

            // TODO: Fix an appropriate threshold based on performance on hosted machine with load
            using (var timer = new SimpleTimer("PythonParserParseOperation", PythonTelemetryConstants.EventArea,
                PythonTelemetryConstants.PythonParserTotalTime, _logger, _telemetry, TimeSpan.FromMilliseconds(1)))
            {
                try
                {
                    _telemetry.AddToCumulativeTelemetry(PythonTelemetryConstants.EventArea, PythonTelemetryConstants.TotalLinesParsed, logData.LineNumber);

                    switch (this.state)
                    {
                        case ParserState.ExpectingSummary:

                            if (string.IsNullOrWhiteSpace(logData.Line))
                            {
                                return;
                            }

                            // Summary Test count and total time should have already been parsed
                            // Try to parse test outcome, number of tests for each outcome
                            if (TryParseSummaryOutcome(logData))
                            {
                                PublishAndReset(logData);
                                return;
                            }

                            // Summary was not parsed, reset the parser and try parse again.
                            Reset(logData);
                            Parse(logData);
                            break;

                        case ParserState.ExpectingFailedResults:

                            // Try to parse for failed results and summary
                            // If summary is parsed, change the state
                            if (TryParseForFailedResult(logData))
                            {
                                stackTraceLinesAllowedToParse = 50;
                                return;
                            }

                            if (TryParseSummaryTestAndTime(logData))
                            {
                                this.state = ParserState.ExpectingSummary;
                                this._logger.Info($"PythonTestResultParser : ExpectingFailedResults: transitioned to state ExpectingSummary at line {logData.LineNumber}");
                                return;
                            }

                            // Not expected, as Summary has not been encountered yet
                            // If a new TestResult is found, reset the parser and Parse again
                            if (TryParseTestResult(logData))
                            {
                                this._logger.Error($"PythonTestResultParser : Parse : Expecting failed result or summary but found new test result at line {logData.LineNumber}.");
                                this._telemetry.AddToCumulativeTelemetry(PythonTelemetryConstants.EventArea, PythonTelemetryConstants.SummaryOrFailedTestsNotFound, new List<int> { this.currentTestRunId }, true);
                                Reset(logData);
                                Parse(logData);
                            }

                            TryParseStackTrace(logData);

                            break;

                        case ParserState.ExpectingTestResults:

                        default:

                            if (TryParseTestResult(logData)) return;

                            // Change the state and clear the partial result if failed result or summary is found
                            if (TryParseForFailedResult(logData))
                            {
                                this.partialTestResult = null;
                                this.state = ParserState.ExpectingFailedResults;
                                stackTraceLinesAllowedToParse = 50;
                                this._logger.Info($"PythonTestResultParser : ExpectingTestResults: transitioned to state ExpectingFailedResults at line {logData.LineNumber}");

                                return;
                            }

                            if (TryParseSummaryTestAndTime(logData))
                            {
                                this.partialTestResult = null;
                                this.state = ParserState.ExpectingSummary;
                                this._logger.Info($"PythonTestResultParser : ExpectingTestResults: transitioned to state ExpectingSummary at line {logData.LineNumber}");
                                return;
                            }

                            break;
                    }
                }
                catch (RegexMatchTimeoutException regexMatchTimeoutException)
                {
                    this._logger.Warning($"JasmineTestResultParser : AttemptMatch : failed due to timeout while with exception { regexMatchTimeoutException } at line {logData.LineNumber}");
                    this._telemetry.AddToCumulativeTelemetry(PythonTelemetryConstants.EventArea, "RegexTimeout", new List<string> { "UnknownRegex" }, true);
                }
                catch (Exception ex)
                {
                    this._logger.Error($"PythonTestResultParser : Parse : Unable to parse the log line {logData.Line} with exception {ex.ToString()} at line {logData.LineNumber}");
                    this._telemetry.AddToCumulativeTelemetry(PythonTelemetryConstants.EventArea, PythonTelemetryConstants.ParseException, ex.Message);

                    Reset(logData);

                    // Rethrow the exception so that the invoker of Parser is notified of a failure
                    throw;
                }
            }
        }

        /// <summary>
        /// Reset the parser to original state
        /// </summary>
        private void Reset(LogData logData)
        {
            this._logger.Info($"PythonTestResultParser : Reset at line {logData.LineNumber}");
            this.partialTestResult = null;
            this.currentTestRun = new TestRun($"{Name}/{Version}", ++this.currentTestRunId);
            this.state = ParserState.ExpectingTestResults;
            this.stackTraceLinesAllowedToParse = -1;
            this.captureStackTrace = false;
        }

        /// <summary>
        /// Publish the current test run and reset the parser
        /// </summary>
        private void PublishAndReset(LogData logData)
        {
            this._logger.Info($"PythonTestResultParser : PublishAndReset : Publishing TestRun {this.currentTestRunId} at line {logData.LineNumber}.");

            foreach (var failedTest in this.currentTestRun.FailedTests)
            {
                if (failedTest.StackTrace != null)
                {
                    failedTest.StackTrace = failedTest.StackTrace.TrimEnd();
                }
            }

            this._testRunManager.PublishAsync(this.currentTestRun);
            Reset(logData);
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

            if (testCaseName == null)
            {
                return false;
            }

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

            if (!failedResultMatch.Success)
            {
                return false;
            }

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
                this._logger.Verbose($"Test result name is null or whitespace in logData: {logData.Line} at line {logData.LineNumber}");
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

                this._logger.Info($"PythonTestResultParser : TryParseSummaryTestAndTime : TestRunSummary with total time and tests created at line {logData.LineNumber}");

                return true;
            }

            return false;
        }

        private bool TryParseSummaryOutcome(LogData logData)
        {
            if (this.currentTestRun.TestRunSummary == null)
            {
                // This is safe check, if must be true always because parsers will try to parse for Outcome if Test and Time Summary already parsed.
                this._logger.Error($"PythonTestResultParser : TryParseSummaryOutcome : TestRunSummary is null at line {logData.LineNumber}");
                this._telemetry.AddToCumulativeTelemetry(PythonTelemetryConstants.EventArea, PythonTelemetryConstants.TestRunSummaryCorrupted, new List<int> { this.currentTestRunId }, true);
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

            this._logger.Error($"PythonTestResultParser : TryParseSummaryOutcome : Expected match for SummaryTestOutcome was not found at line {logData.LineNumber}");
            this._telemetry.AddToCumulativeTelemetry(PythonTelemetryConstants.EventArea, PythonTelemetryConstants.TestOutcomeSummaryNotFound, new List<int> { this.currentTestRunId }, true);
            return false;
        }

        /// <summary>
        /// Tries to capture stack trace lines
        /// Also identifies beginnin and end of a stack trace
        /// </summary>
        /// <param name="logData"></param>
        private void TryParseStackTrace(LogData logData)
        {
            if (PythonRegexes.StackTraceBorder.IsMatch(logData.Line))
            {
                captureStackTrace = !captureStackTrace;
                return;
            }

            if (PythonRegexes.StackTraceEnd.IsMatch(logData.Line))
            {
                captureStackTrace = false;
                return;
            }

            if (captureStackTrace == false)
            {
                return;
            }

            // Add to the stack trace
            if (this.currentTestRun.FailedTests[this.currentTestRun.FailedTests.Count - 1].StackTrace == null)
            {
                this.currentTestRun.FailedTests[this.currentTestRun.FailedTests.Count - 1].StackTrace = logData.Line;
            }
            else
            {
                this.currentTestRun.FailedTests[this.currentTestRun.FailedTests.Count - 1].StackTrace += Environment.NewLine + logData.Line;
            }

            if (stackTraceLinesAllowedToParse > -1)
            {
                stackTraceLinesAllowedToParse--;
                if (stackTraceLinesAllowedToParse == 0)
                {
                    // Reset the parser if the stack trace does not terminate within 50 lines
                    this.currentTestRun.FailedTests[this.currentTestRun.FailedTests.Count - 1].StackTrace = null;
                    captureStackTrace = false;
                }
            }

            return;
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
                this._logger.Error("PythonTestResultParser : IsValidInput : Received null data");
                this._telemetry.AddToCumulativeTelemetry(PythonTelemetryConstants.EventArea, PythonTelemetryConstants.InvalidInput, new List<int> { this.currentTestRunId }, true);
            }

            return data != null;
        }
    }
}
