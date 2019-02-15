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
        private JasmineParserStates _currentState;
        private readonly JasmineParserStateContext _stateContext;

        private ITestResultParserState _testRunStart;
        private ITestResultParserState _expectingTestResults;
        private ITestResultParserState _expectingTestRunSummary;

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
            Logger.Info("JasmineTestResultParser : Starting jasmine test result parser.");
            telemetryDataCollector.AddOrUpdate(JasmineTelemetryConstants.Initialize, true, JasmineTelemetryConstants.EventArea);

            // Initialize the starting state of the parser
            var testRun = new TestRun($"{Name}/{Version}", "Jasmine", 1);

            _stateContext = new JasmineParserStateContext(testRun);
            _currentState = JasmineParserStates.ExpectingTestRunStart;
        }

        public override void Parse(LogData logData)
        {
            if (logData == null || logData.Line == null)
            {
                Logger.Error("JasmineTestResultParser : Parse : Input line was null.");
                return;
            }

            // TODO: Fix an appropriate threshold based on performance on hosted machine with load
            using (var timer = new SimpleTimer("JasmineParserParseOperation", JasmineTelemetryConstants.EventArea,
                JasmineTelemetryConstants.JasmineParserTotalTime, logData.LineNumber, Logger, Telemetry, ParseOperationPermissibleThreshold))
            {
                try
                {
                    _stateContext.CurrentLineNumber = logData.LineNumber;
                    Telemetry.AddOrUpdate(JasmineTelemetryConstants.TotalLinesParsed, logData.LineNumber, JasmineTelemetryConstants.EventArea);

                    // State model for the jasmine parser that defines the Regexs to match against in each state
                    switch (_currentState)
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
                    Logger.Error($"JasmineTestResultParser : Parse : Failed with exception {e}.");

                    // This might start taking a lot of space if each and every parse operation starts throwing
                    // But if that happens then there's a lot more stuff broken.
                    Telemetry.AddAndAggregate(JasmineTelemetryConstants.Exceptions, 
                        new List<string> { e.Message }, JasmineTelemetryConstants.EventArea);

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
                        _stateContext.LinesWithinWhichMatchIsExpected = -1;

                        _currentState = (JasmineParserStates)regexActionPair.MatchAction(match, _stateContext);
                        return true;
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    Logger.Warning($"JasmineTestResultParser : AttemptMatch : failed due to timeout while trying to match { regexActionPair.Regex.ToString() } at line {logData.LineNumber}");
                    Telemetry.AddAndAggregate("RegexTimeout", new List<string> { regexActionPair.Regex.ToString() }, JasmineTelemetryConstants.EventArea);
                }
            }

            state.PeformNoPatternMatchedAction(logData.Line, _stateContext);

            return false;
        }

        /// <summary>
        /// Publishes the run and resets the parser by resetting the state context and current state
        /// </summary>
        private void AttemptPublishAndResetParser()
        {
            Logger.Info($"JasmineTestResultParser : Resetting the parser and attempting to publish the test run at line {_stateContext.CurrentLineNumber}.");
            var testRunToPublish = _stateContext.TestRun;

            // We have encountered failed test cases but no failed summary was encountered
            if (testRunToPublish.FailedTests.Count != 0 && testRunToPublish.TestRunSummary.TotalFailed == 0)
            {
                Logger.Error("JasmineTestResultParser : Failed tests were encountered but no failed summary was encountered.");
                Telemetry.AddAndAggregate( JasmineTelemetryConstants.FailedTestCasesFoundButNoFailedSummary,
                    new List<int> { _stateContext.TestRun.TestRunId }, JasmineTelemetryConstants.EventArea);
            }
            else if (testRunToPublish.TestRunSummary.TotalFailed != testRunToPublish.FailedTests.Count)
            {
                // If encountered failed tests does not match summary fire telemetry
                Logger.Error($"JasmineTestResultParser : Failed tests count does not match failed summary" +
                    $" at line {_stateContext.CurrentLineNumber}");
                Telemetry.AddAndAggregate(JasmineTelemetryConstants.FailedSummaryMismatch,
                    new List<int> { testRunToPublish.TestRunId }, JasmineTelemetryConstants.EventArea);
            }

            if (testRunToPublish.SkippedTests.Count != 0 && testRunToPublish.TestRunSummary.TotalSkipped == 0)
            {
                Logger.Error("JasmineTestResultParser : Skipped tests were encountered but no skipped summary was encountered.");
                Telemetry.AddAndAggregate(JasmineTelemetryConstants.SkippedTestCasesFoundButNoSkippedSummary,
                    new List<int> { _stateContext.TestRun.TestRunId }, JasmineTelemetryConstants.EventArea);
            }
            else if (testRunToPublish.TestRunSummary.TotalSkipped != testRunToPublish.SkippedTests.Count)
            {
                // If encountered skipped tests does not match summary fire telemetry
                Logger.Error($"JasmineTestResultParser : Pending tests count does not match pending summary" +
                    $" at line {_stateContext.CurrentLineNumber}");
                Telemetry.AddAndAggregate(JasmineTelemetryConstants.SkippedSummaryMismatch, 
                    new List<int> { testRunToPublish.TestRunId }, JasmineTelemetryConstants.EventArea);
            }

            // Ensure some summary data was detected before attempting a publish, ie. check if the state is not test results state
            switch (_currentState)
            {
                case JasmineParserStates.ExpectingTestRunStart:

                    Logger.Error("JasmineTestResultParser : Skipping publish as no test cases or summary has been encountered.");
                    Telemetry.AddAndAggregate(JasmineTelemetryConstants.NoSummaryEncounteredBeforePublish,
                        new List<int> { _stateContext.TestRun.TestRunId }, JasmineTelemetryConstants.EventArea);

                    break;

                case JasmineParserStates.ExpectingTestResults:
                    if (testRunToPublish.PassedTests.Count != 0
                        || testRunToPublish.FailedTests.Count != 0
                        || testRunToPublish.SkippedTests.Count != 0)
                    {
                        Logger.Error("JasmineTestResultParser : Skipping publish as testcases were encountered but no summary was encountered.");
                        Telemetry.AddAndAggregate(JasmineTelemetryConstants.PassedTestCasesFoundButNoPassedSummary, 
                            new List<int> { _stateContext.TestRun.TestRunId }, JasmineTelemetryConstants.EventArea);
                    }
                    break;

                case JasmineParserStates.ExpectingTestRunSummary:

                    if (testRunToPublish.TestRunSummary.TotalTests == 0)
                    {
                        Logger.Error("JasmineTestResultParser : Skipping publish as total tests was 0.");
                        Telemetry.AddAndAggregate(JasmineTelemetryConstants.TotalTestsZero,
                            new List<int> { _stateContext.TestRun.TestRunId }, JasmineTelemetryConstants.EventArea);
                        break;
                    }

                    if (_stateContext.IsTimeParsed == false)
                    {
                        Logger.Error("JasmineTestResultParser : Total test run time was not parsed.");
                        Telemetry.AddAndAggregate(JasmineTelemetryConstants.TotalTestRunTimeNotParsed, 
                            new List<int> { _stateContext.TestRun.TestRunId }, JasmineTelemetryConstants.EventArea);
                    }

                    if (_stateContext.SuiteErrors > 0)
                    {
                        // Adding telemetry for suite errors
                        Logger.Info($"JasmineTestResultParser : {_stateContext.SuiteErrors} suite errors found in the test run.");
                        Telemetry.AddAndAggregate(JasmineTelemetryConstants.SuiteErrors,
                            new List<int> { _stateContext.TestRun.TestRunId }, JasmineTelemetryConstants.EventArea);
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
                    TestRunManager.PublishAsync(testRunToPublish);

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
            var newTestRun = new TestRun($"{Name}/{Version}", "Jasmine", _stateContext.TestRun.TestRunId + 1);

            // Set state to ExpectingTestResults
            _currentState = JasmineParserStates.ExpectingTestRunStart;

            // Refresh the context
            _stateContext.Initialize(newTestRun);

            Logger.Info("JasmineTestResultParser : Successfully reset the parser.");
        }

        private ITestResultParserState TestRunStart => _testRunStart ??
            (_testRunStart = new JasmineParserStateExpectingTestRunStart(AttemptPublishAndResetParser, Logger, Telemetry, nameof(JasmineTestResultParser)));

        private ITestResultParserState ExpectingTestResults => _expectingTestResults ??
            (_expectingTestResults = new JasmineParserStateExpectingTestResults(AttemptPublishAndResetParser, Logger, Telemetry, nameof(JasmineTestResultParser)));

        private ITestResultParserState ExpectingTestRunSummary => _expectingTestRunSummary ??
            (_expectingTestRunSummary = new JasmineParserStateExpectingTestRunSummary(AttemptPublishAndResetParser, Logger, Telemetry, nameof(JasmineTestResultParser)));
    }
}
