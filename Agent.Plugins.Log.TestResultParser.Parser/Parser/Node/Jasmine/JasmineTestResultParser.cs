// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.Log.TestResultParser.Parser
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using Agent.Plugins.Log.TestResultParser.Contracts;

    public class JasmineTestResultParser : AbstractTestResultParser
    {
        private JasmineParserStates currentState;
        private readonly JasmineParserStateContext stateContext;

        private ITestResultParserState testRunStart;
        private ITestResultParserState expectingTestResults;
        private ITestResultParserState expectingTestRunSummary;

        public override string Name => nameof(JasmineTestResultParser);
        public override string Version => "1.0";

        /// <summary>
        /// Detailed constructor where specified logger and telemetry data collector are initialized along with test run manager
        /// </summary>
        /// <param name="testRunPublisher"></param>
        /// <param name="diagnosticDataCollector"></param>
        /// <param name="telemetryDataCollector"></param>
        public JasmineTestResultParser(ITestRunManager testRunManager, ITraceLogger logger, ITelemetryDataCollector telemetryDataCollector) :
            base(testRunManager, logger, telemetryDataCollector)
        {
            logger.Info("JasmineTestResultParser : Starting jasmine test result parser.");
            telemetryDataCollector.AddToCumulativeTelemetry(JasmineTelemetryConstants.EventArea, JasmineTelemetryConstants.Initialize, true);

            // Initialize the starting state of the parser
            var testRun = new TestRun($"{Name}/{Version}", 1);
            stateContext = new JasmineParserStateContext(testRun);
            currentState = JasmineParserStates.ExpectingTestRunStart;
        }

        public override void Parse(LogData logData)
        {
            if (logData == null || logData.Line == null)
            {
                _logger.Error("JasmineTestResultParser : Parse : Input line was null.");
                return;
            }

            // TODO: Fix an appropriate threshold based on performance on hosted machine with load
            using (var timer = new SimpleTimer("JasmineParserParseOperation", JasmineTelemetryConstants.EventArea,
                JasmineTelemetryConstants.JasmineParserTotalTime, logData.LineNumber, _logger, _telemetry, ParseOperationPermissibleThreshold))
            {
                try
                {
                    stateContext.CurrentLineNumber = logData.LineNumber;
                    _telemetry.AddToCumulativeTelemetry(JasmineTelemetryConstants.EventArea, JasmineTelemetryConstants.TotalLinesParsed, logData.LineNumber);

                    // State model for the jasmine parser that defines the Regexs to match against in each state
                    switch (currentState)
                    {
                        // This state primarily looks for test run start indicator and
                        // transitions to the next one after encountering one
                        case JasmineParserStates.ExpectingTestRunStart:

                            if (AttemptMatch(TestRunStart, logData))
                                return;
                            break;

                        // This state primarily looks for test results and transitions
                        // to the next one after a summary is encountered
                        case JasmineParserStates.ExpectingTestResults:

                            if (AttemptMatch(ExpectingTestResults, logData))
                                return;
                            break;

                        // This state primarily looks for test run summary 
                        // and transitions back to testrunstart state
                        case JasmineParserStates.ExpectingTestRunSummary:

                            if (AttemptMatch(ExpectingTestRunSummary, logData))
                                return;
                            break;
                    }
                }
                catch (Exception e)
                {
                    _logger.Error($"JasmineTestResultParser : Parse : Failed with exception {e}.");

                    // This might start taking a lot of space if each and every parse operation starts throwing
                    // But if that happens then there's a lot more stuff broken.
                    _telemetry.AddToCumulativeTelemetry(JasmineTelemetryConstants.EventArea, JasmineTelemetryConstants.Exceptions, new List<string> { e.Message });

                    // Rethrowing this so that the plugin is aware that the parser is erroring out
                    // Ideally this would never should happen
                    throw;
                }
            }
        }

        /// <summary>
        /// Attempts to match the line with each regex specified by the current state
        /// </summary>
        /// <param name="state">Current state</param>
        /// <param name="logData">Input line</param>
        /// <returns>True if a match occurs</returns>
        private bool AttemptMatch(ITestResultParserState state, LogData logData)
        {
            foreach (var regexActionPair in state.RegexesToMatch)
            {
                try
                {
                    var match = regexActionPair.Regex.Match(logData.Line);
                    if (match.Success)
                    {
                        // Reset this value on a match
                        stateContext.LinesWithinWhichMatchIsExpected = -1;

                        currentState = (JasmineParserStates)regexActionPair.MatchAction(match, stateContext);
                        return true;
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    _logger.Warning($"JasmineTestResultParser : AttemptMatch : failed due to timeout while trying to match { regexActionPair.Regex.ToString() } at line {logData.LineNumber}");
                    _telemetry.AddToCumulativeTelemetry(JasmineTelemetryConstants.EventArea, "RegexTimeout", new List<string> { regexActionPair.Regex.ToString() }, true);
                }
            }

            state.PeformNoPatternMatchedAction(logData.Line, stateContext);

            return false;
        }

        /// <summary>
        /// Publishes the run and resets the parser by resetting the state context and current state
        /// </summary>
        private void AttemptPublishAndResetParser()
        {
            _logger.Info($"JasmineTestResultParser : Resetting the parser and attempting to publish the test run at line {stateContext.CurrentLineNumber}.");
            var testRunToPublish = stateContext.TestRun;

            // We have encountered failed test cases but no failed summary was encountered
            if (testRunToPublish.FailedTests.Count != 0 && testRunToPublish.TestRunSummary.TotalFailed == 0)
            {
                _logger.Error("JasmineTestResultParser : Failed tests were encountered but no failed summary was encountered.");
                _telemetry.AddToCumulativeTelemetry(JasmineTelemetryConstants.EventArea,
                    JasmineTelemetryConstants.FailedTestCasesFoundButNoFailedSummary, new List<int> { stateContext.TestRun.TestRunId }, true);
            }
            else if (testRunToPublish.TestRunSummary.TotalFailed != testRunToPublish.FailedTests.Count)
            {
                // If encountered failed tests does not match summary fire telemetry
                _logger.Error($"JasmineTestResultParser : Failed tests count does not match failed summary" +
                    $" at line {stateContext.CurrentLineNumber}");
                _telemetry.AddToCumulativeTelemetry(JasmineTelemetryConstants.EventArea,
                    JasmineTelemetryConstants.FailedSummaryMismatch, new List<int> { testRunToPublish.TestRunId }, true);
            }

            if (testRunToPublish.SkippedTests.Count != 0 && testRunToPublish.TestRunSummary.TotalSkipped == 0)
            {
                _logger.Error("JasmineTestResultParser : Skipped tests were encountered but no skipped summary was encountered.");
                _telemetry.AddToCumulativeTelemetry(JasmineTelemetryConstants.EventArea,
                    JasmineTelemetryConstants.SkippedTestCasesFoundButNoSkippedSummary, new List<int> { stateContext.TestRun.TestRunId }, true);
            }
            else if (testRunToPublish.TestRunSummary.TotalSkipped != testRunToPublish.SkippedTests.Count)
            {
                // If encountered skipped tests does not match summary fire telemetry
                _logger.Error($"JasmineTestResultParser : Pending tests count does not match pending summary" +
                    $" at line {stateContext.CurrentLineNumber}");
                _telemetry.AddToCumulativeTelemetry(JasmineTelemetryConstants.EventArea,
                    JasmineTelemetryConstants.SkippedSummaryMismatch, new List<int> { testRunToPublish.TestRunId }, true);
            }

            // Ensure some summary data was detected before attempting a publish, ie. check if the state is not test results state
            switch (currentState)
            {
                case JasmineParserStates.ExpectingTestRunStart:

                    _logger.Error("JasmineTestResultParser : Skipping publish as no test cases or summary has been encountered.");
                    _telemetry.AddToCumulativeTelemetry(JasmineTelemetryConstants.EventArea,
                            JasmineTelemetryConstants.NoSummaryEncounteredBeforePublish, new List<int> { stateContext.TestRun.TestRunId }, true);

                    break;

                case JasmineParserStates.ExpectingTestResults:
                    if (testRunToPublish.PassedTests.Count != 0
                        || testRunToPublish.FailedTests.Count != 0
                        || testRunToPublish.SkippedTests.Count != 0)
                    {
                        _logger.Error("JasmineTestResultParser : Skipping publish as testcases were encountered but no summary was encountered.");
                        _telemetry.AddToCumulativeTelemetry(JasmineTelemetryConstants.EventArea,
                            JasmineTelemetryConstants.PassedTestCasesFoundButNoPassedSummary, new List<int> { stateContext.TestRun.TestRunId }, true);
                    }
                    break;

                case JasmineParserStates.ExpectingTestRunSummary:

                    if (testRunToPublish.TestRunSummary.TotalTests == 0)
                    {
                        _logger.Error("JasmineTestResultParser : Skipping publish as total tests was 0.");
                        _telemetry.AddToCumulativeTelemetry(JasmineTelemetryConstants.EventArea,
                            JasmineTelemetryConstants.TotalTestsZero, new List<int> { stateContext.TestRun.TestRunId }, true);
                        break;
                    }

                    if (stateContext.IsTimeParsed == false)
                    {
                        _logger.Error("JasmineTestResultParser : Total test run time was not parsed.");
                        _telemetry.AddToCumulativeTelemetry(JasmineTelemetryConstants.EventArea,
                            JasmineTelemetryConstants.TotalTestRunTimeNotParsed, new List<int> { stateContext.TestRun.TestRunId }, true);
                    }

                    if (stateContext.SuiteErrors > 0)
                    {
                        // Adding telemetry for suite errors
                        _logger.Info($"JasmineTestResultParser : {stateContext.SuiteErrors} suite errors found in the test run.");
                        _telemetry.AddToCumulativeTelemetry(JasmineTelemetryConstants.EventArea, JasmineTelemetryConstants.SuiteErrors,
                            new List<int> { stateContext.TestRun.TestRunId }, true);
                    }

                    // Trim the stack traces of extra newlines etc.
                    foreach (var failedTest in testRunToPublish.FailedTests)
                    {
                        if (failedTest.StackTrace != null)
                        {
                            failedTest.StackTrace = failedTest.StackTrace.TrimEnd();
                        }
                    }

                    // Only publish if total tests was not zero
                    _testRunManager.PublishAsync(testRunToPublish);

                    break;
            }

            ResetParser();
        }

        /// <summary>
        /// Used to reset the parser including the test run and context
        /// </summary>
        private void ResetParser()
        {
            // Start a new TestRun
            var newTestRun = new TestRun($"{Name}/{Version}", stateContext.TestRun.TestRunId + 1);

            // Set state to ExpectingTestResults
            currentState = JasmineParserStates.ExpectingTestRunStart;

            // Refresh the context
            stateContext.Initialize(newTestRun);

            _logger.Info("JasmineTestResultParser : Successfully reset the parser.");
        }

        private ITestResultParserState TestRunStart => testRunStart ??
            (testRunStart = new JasmineParserStateExpectingTestRunStart(AttemptPublishAndResetParser, _logger, _telemetry, nameof(JasmineTestResultParser)));

        private ITestResultParserState ExpectingTestResults => expectingTestResults ??
            (expectingTestResults = new JasmineParserStateExpectingTestResults(AttemptPublishAndResetParser, _logger, _telemetry, nameof(JasmineTestResultParser)));

        private ITestResultParserState ExpectingTestRunSummary => expectingTestRunSummary ??
            (expectingTestRunSummary = new JasmineParserStateExpectingTestRunSummary(AttemptPublishAndResetParser, _logger, _telemetry, nameof(JasmineTestResultParser)));
    }
}