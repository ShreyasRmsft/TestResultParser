// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.Log.TestResultParser.Parser
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using Agent.Plugins.Log.TestResultParser.Contracts;

    public class JestTestResultParser : AbstractTestResultParser
    {
        // TODO: Need a hook for end of logs.
        // Needed for multiple reasons. Scenarios where i am expecting things and have not yet published the run
        // Needed where I have encoutered test results but got no summary
        // It is true that it can be inferred due to the absense of the summary event, but I would like there to
        // be one telemetry event per parser run

        // TODO: Decide on a reset if no match found withing x lines logic after a previous match.
        // This can be fine tuned depending on the previous match
        // Infra already in place for this

        private JestParserStates _currentState;
        private readonly JestParserStateContext _stateContext;

        private ITestResultParserState _testRunStart;
        private ITestResultParserState _expectingTestResults;
        private ITestResultParserState _expectingStackTraces;
        private ITestResultParserState _expectingTestRunSummary;

        public override string Name => nameof(JestTestResultParser);

        public override string Version => "1.0";

        /// <summary>
        /// Detailed constructor where specified logger and telemetry data collector are initialized along with test run manager
        /// </summary>
        /// <param name="testRunPublisher"></param>
        /// <param name="diagnosticDataCollector"></param>
        /// <param name="telemetryDataCollector"></param>
        public JestTestResultParser(ITestRunManager testRunManager, ITraceLogger logger, ITelemetryDataCollector telemetryDataCollector)
        : base(testRunManager, logger, telemetryDataCollector)
        {
            logger.Info("JestTestResultParser : Starting jest test result parser.");
            telemetryDataCollector.AddOrUpdate(JestTelemetryConstants.Initialize, true, JestTelemetryConstants.EventArea);

            // Initialize the starting state of the parser
            var testRun = new TestRun($"{Name}/{Version}", "Jest", 1);
            _stateContext = new JestParserStateContext(testRun);
            _currentState = JestParserStates.ExpectingTestRunStart;
        }

        /// <inheritdoc/>
        public override void Parse(LogData logData)
        {
            if (logData == null || logData.Line == null)
            {
                Logger.Error("JestTestResultParser : Parse : Input line was null.");
                return;
            }

            // TODO: Fix an appropriate threshold based on performance on hosted machine with load
            using (var timer = new SimpleTimer("JestParserParseOperation", JestTelemetryConstants.EventArea,
                JestTelemetryConstants.JestParserTotalTime, logData.LineNumber, Logger, Telemetry, ParseOperationPermissibleThreshold))
            {
                try
                {
                    _stateContext.CurrentLineNumber = logData.LineNumber;
                    Telemetry.AddOrUpdate(JestTelemetryConstants.TotalLinesParsed, logData.LineNumber, JestTelemetryConstants.EventArea);

                    // State model for the jest parser that defines the Regexs to match against in each state
                    // Each state re-orders the Regexs based on the frequency of expected matches
                    switch (_currentState)
                    {
                        // This state primarily looks for test run start indicator and
                        // transitions to the next one after encountering one
                        case JestParserStates.ExpectingTestRunStart:

                            if (AttemptMatch(TestRunStart, logData))
                                return;
                            break;

                        // This state primarily looks for test results and transitions
                        // to the next one after a stack trace or summary is encountered
                        case JestParserStates.ExpectingTestResults:

                            if (AttemptMatch(ExpectingTestResults, logData))
                                return;
                            break;

                        // This state primarily looks for stack traces/failed test cases
                        // and transitions on encountering summary
                        case JestParserStates.ExpectingStackTraces:

                            if (AttemptMatch(ExpectingStackTraces, logData))
                                return;
                            break;

                        // This state primarily looks for test run summary 
                        // and transitions back to testresults state on encountering
                        // another test run start marker indicating tests being run from
                        // more than one file
                        case JestParserStates.ExpectingTestRunSummary:

                            if (AttemptMatch(ExpectingTestRunSummary, logData))
                                return;
                            break;
                    }
                }
                catch (Exception e)
                {
                    Logger.Error($"JestTestResultParser : Parse : Failed with exception {e}.");

                    // This might start taking a lot of space if each and every parse operation starts throwing
                    // But if that happens then there's a lot more stuff broken.
                    Telemetry.AddAndAggregate(JestTelemetryConstants.Exceptions, new List<string> { e.Message }, JestTelemetryConstants.EventArea);

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
        /// <returns>true if a match occurs</returns>
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

                        _currentState = (JestParserStates)regexActionPair.MatchAction(match, _stateContext);
                        return true;
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    Logger.Warning($"JestTestResultParser : AttemptMatch : failed due to timeout while trying to match" +
                        $" { regexActionPair.Regex.ToString() } at line {logData.LineNumber}");
                    Telemetry.AddAndAggregate("RegexTimeout", 
                        new List<string> { regexActionPair.Regex.ToString() }, JestTelemetryConstants.EventArea);
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
            Logger.Info($"JestTestResultParser : Resetting the parser and attempting to publish the test run at line {_stateContext.CurrentLineNumber}.");
            var testRunToPublish = _stateContext.TestRun;

            // We have encountered passed test cases but no passed summary was encountered
            if (testRunToPublish.PassedTests.Count != 0 && testRunToPublish.TestRunSummary.TotalPassed == 0)
            {
                Logger.Error("JestTestResultParser : Passed tests were encountered but no passed summary was encountered.");
                Telemetry.AddAndAggregate(JestTelemetryConstants.PassedTestCasesFoundButNoPassedSummary, 
                    new List<int> { _stateContext.TestRun.TestRunId }, JestTelemetryConstants.EventArea);
            }
            else if (_stateContext.VerboseOptionEnabled && testRunToPublish.TestRunSummary.TotalPassed != testRunToPublish.PassedTests.Count)
            {
                // If encountered failed tests does not match summary fire telemetry
                Logger.Error($"JestTestResultParser : Passed tests count does not match passed summary" +
                    $" at line {_stateContext.CurrentLineNumber}");
                Telemetry.AddAndAggregate(JestTelemetryConstants.PassedSummaryMismatch,
                    new List<int> { testRunToPublish.TestRunId }, JestTelemetryConstants.EventArea);
            }

            // We have encountered failed test cases but no failed summary was encountered
            if (testRunToPublish.FailedTests.Count != 0 && testRunToPublish.TestRunSummary.TotalFailed == 0)
            {
                Logger.Error("JestTestResultParser : Failed tests were encountered but no failed summary was encountered.");
                Telemetry.AddAndAggregate(JestTelemetryConstants.FailedTestCasesFoundButNoFailedSummary,
                    new List<int> { _stateContext.TestRun.TestRunId }, JestTelemetryConstants.EventArea);
            }
            else if (testRunToPublish.TestRunSummary.TotalFailed != testRunToPublish.FailedTests.Count)
            {
                // If encountered failed tests does not match summary fire telemtry
                Logger.Error($"JestTestResultParser : Failed tests count does not match failed summary" +
                    $" at line {_stateContext.CurrentLineNumber}");
                Telemetry.AddAndAggregate(JestTelemetryConstants.FailedSummaryMismatch,
                    new List<int> { testRunToPublish.TestRunId }, JestTelemetryConstants.EventArea);
            }

            // Ensure some summary data was detected before attempting a publish, ie. check if the state is not test results state
            switch (_currentState)
            {
                case JestParserStates.ExpectingTestRunStart:

                    Logger.Error("JestTestResultParser : Skipping publish as no test cases or summary has been encountered.");

                    break;

                case JestParserStates.ExpectingTestResults:

                case JestParserStates.ExpectingStackTraces:

                    if (testRunToPublish.PassedTests.Count != 0
                        || testRunToPublish.FailedTests.Count != 0
                        || testRunToPublish.SkippedTests.Count != 0)
                    {
                        Logger.Error("JestTestResultParser : Skipping publish as testcases were encountered but no summary was encountered.");
                        Telemetry.AddAndAggregate(JestTelemetryConstants.TestCasesFoundButNoSummary,
                            new List<int> { _stateContext.TestRun.TestRunId }, JestTelemetryConstants.EventArea);
                    }

                    break;

                case JestParserStates.ExpectingTestRunSummary:

                    if (testRunToPublish.TestRunSummary.TotalTests == 0)
                    {
                        Logger.Error("JestTestResultParser : Skipping publish as total tests was 0.");
                        Telemetry.AddAndAggregate(JestTelemetryConstants.TotalTestsZero,
                            new List<int> { _stateContext.TestRun.TestRunId }, JestTelemetryConstants.EventArea);
                        break;
                    }

                    if (testRunToPublish.TestRunSummary.TotalExecutionTime.TotalMilliseconds == 0)
                    {
                        Logger.Error("JestTestResultParser : Total test run time was 0 or not encountered.");
                        Telemetry.AddAndAggregate(JestTelemetryConstants.TotalTestRunTimeZero,
                            new List<int> { _stateContext.TestRun.TestRunId }, JestTelemetryConstants.EventArea);
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
            var newTestRun = new TestRun($"{Name}/{Version}", "Jest", _stateContext.TestRun.TestRunId + 1);

            // Set state to ExpectingTestResults
            _currentState = JestParserStates.ExpectingTestRunStart;

            // Refresh the context
            _stateContext.Initialize(newTestRun);

            Logger.Info("JestTestResultParser : Successfully reset the parser.");
        }

        private ITestResultParserState TestRunStart => _testRunStart ??
            (_testRunStart = new JestParserStateExpectingTestRunStart(AttemptPublishAndResetParser, Logger, Telemetry, nameof(JestTestResultParser)));

        private ITestResultParserState ExpectingTestResults => _expectingTestResults ??
            (_expectingTestResults = new JestParserStateExpectingTestResults(AttemptPublishAndResetParser, Logger, Telemetry, nameof(JestTestResultParser)));

        private ITestResultParserState ExpectingStackTraces => _expectingStackTraces ??
            (_expectingStackTraces = new JestParserStateExpectingStackTraces(AttemptPublishAndResetParser, Logger, Telemetry, nameof(JestTestResultParser)));

        private ITestResultParserState ExpectingTestRunSummary => _expectingTestRunSummary ??
            (_expectingTestRunSummary = new JestParserStateExpectingTestRunSummary(AttemptPublishAndResetParser, Logger, Telemetry, nameof(JestTestResultParser)));
    }
}