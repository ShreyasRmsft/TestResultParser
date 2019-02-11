// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Agent.Plugins.Log.TestResultParser.Contracts;

namespace Agent.Plugins.Log.TestResultParser.Parser
{
    public class MochaTestResultParser : AbstractTestResultParser
    {
        private MochaParserStates _currentState;
        private readonly MochaParserStateContext _stateContext;

        private ITestResultParserState _expectingTestResults;
        private ITestResultParserState _expectingTestRunSummary;
        private ITestResultParserState _expectingStackTraces;

        public override string Name => nameof(MochaTestResultParser);

        public override string Version => "1.0";

        /// <summary>
        /// Detailed constructor where specified logger and telemetry data collector are initialized along with test run manager
        /// </summary>
        /// <param name="testRunPublisher"></param>
        /// <param name="diagnosticDataCollector"></param>
        /// <param name="telemetryDataCollector"></param>
        public MochaTestResultParser(ITestRunManager testRunManager, ITraceLogger logger, ITelemetryDataCollector telemetryDataCollector) : base(testRunManager, logger, telemetryDataCollector)
        {
            logger.Info("MochaTestResultParser : Starting mocha test result parser.");
            telemetryDataCollector.AddToCumulativeTelemetry(MochaTelemetryConstants.EventArea, MochaTelemetryConstants.Initialize, true);

            // Initialize the starting state of the parser
            var testRun = new TestRun($"{Name}/{Version}", "Mocha", 1);
            _stateContext = new MochaParserStateContext(testRun);
            _currentState = MochaParserStates.ExpectingTestResults;
        }

        /// <inheritdoc/>
        public override void Parse(LogData logData)
        {
            if (logData == null || logData.Line == null)
            {
                Logger.Error("MochaTestResultParser : Parse : Input line was null.");
                return;
            }

            // TODO: Fix an appropriate threshold based on performance on hosted machine with load
            using (var timer = new SimpleTimer("MochaParserParseOperation", MochaTelemetryConstants.EventArea,
                MochaTelemetryConstants.MochaParserTotalTime, logData.LineNumber, Logger, Telemetry, ParseOperationPermissibleThreshold))
            {
                try
                {
                    _stateContext.CurrentLineNumber = logData.LineNumber;
                    Telemetry.AddToCumulativeTelemetry(MochaTelemetryConstants.EventArea, MochaTelemetryConstants.TotalLinesParsed, logData.LineNumber);

                    // State model for the mocha parser that defines the Regexs to match against in each state
                    // Each state re-orders the Regexs based on the frequency of expected matches
                    switch (_currentState)
                    {
                        // This state primarily looks for test results 
                        // and transitions to the next one after a line of summary is encountered
                        case MochaParserStates.ExpectingTestResults:

                            if (AttemptMatch(ExpectingTestResults, logData))
                                return;
                            break;

                        // This state primarily looks for test run summary 
                        // If failed tests were found to be present transitions to the next one to look for stack traces
                        // else goes back to the first state after publishing the run
                        case MochaParserStates.ExpectingTestRunSummary:

                            if (AttemptMatch(ExpectingTestRunSummary, logData))
                                return;
                            break;

                        // This state primarily looks for stack traces
                        // If any other match occurs before all the expected stack traces are found,
                        // it fires telemetry for unexpected behavior but moves on to the next test run
                        case MochaParserStates.ExpectingStackTraces:

                            if (AttemptMatch(ExpectingStackTraces, logData))
                                return;
                            break;
                    }
                }
                catch (Exception e)
                {
                    Logger.Error($"MochaTestResultParser : Parse : Failed with exception {e}.");

                    // This might start taking a lot of space if each and every parse operation starts throwing
                    // But if that happens then there's a lot more stuff broken.
                    Telemetry.AddToCumulativeTelemetry(MochaTelemetryConstants.EventArea, "Exceptions", new List<string> { e.Message });

                    // Rethrowing this so that the plugin is aware that the parser is erroring out
                    // Ideally this never should happen
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

                        _currentState = (MochaParserStates)regexActionPair.MatchAction(match, _stateContext);
                        return true;
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    Logger.Warning($"MochaTestResultParser : AttemptMatch : failed due to timeout while trying to match { regexActionPair.Regex.ToString() } at line {logData.LineNumber}");
                    Telemetry.AddToCumulativeTelemetry(MochaTelemetryConstants.EventArea, "RegexTimeout", new List<string> { regexActionPair.Regex.ToString() }, true);
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
            Logger.Info($"MochaTestResultParser : Resetting the parser and attempting to publish the test run at line {_stateContext.CurrentLineNumber}.");
            var testRunToPublish = _stateContext.TestRun;

            // We have encountered failed test cases but no failed summary was encountered
            if (testRunToPublish.FailedTests.Count != 0 && testRunToPublish.TestRunSummary.TotalFailed == 0)
            {
                Logger.Error("MochaTestResultParser : Failed tests were encountered but no failed summary was encountered.");
                Telemetry.AddToCumulativeTelemetry(MochaTelemetryConstants.EventArea,
                    MochaTelemetryConstants.FailedTestCasesFoundButNoFailedSummary, new List<int> { _stateContext.TestRun.TestRunId }, true);
            }
            else if (testRunToPublish.TestRunSummary.TotalFailed != testRunToPublish.FailedTests.Count)
            {
                // If encountered failed tests does not match summary fire telemetry
                Logger.Error($"MochaTestResultParser : Failed tests count does not match failed summary" +
                    $" at line {_stateContext.CurrentLineNumber}");
                Telemetry.AddToCumulativeTelemetry(MochaTelemetryConstants.EventArea,
                    MochaTelemetryConstants.FailedSummaryMismatch, new List<int> { testRunToPublish.TestRunId }, true);
            }

            // We have encountered pending test cases but no pending summary was encountered
            if (testRunToPublish.SkippedTests.Count != 0 && testRunToPublish.TestRunSummary.TotalSkipped == 0)
            {
                Logger.Error("MochaTestResultParser : Skipped tests were encountered but no skipped summary was encountered.");
                Telemetry.AddToCumulativeTelemetry(MochaTelemetryConstants.EventArea,
                    MochaTelemetryConstants.PendingTestCasesFoundButNoFailedSummary, new List<int> { _stateContext.TestRun.TestRunId }, true);
            }
            else if (testRunToPublish.TestRunSummary.TotalSkipped != testRunToPublish.SkippedTests.Count)
            {
                // If encountered skipped tests does not match summary fire telemetry
                Logger.Error($"MochaTestResultParser : Pending tests count does not match pending summary" +
                    $" at line {_stateContext.CurrentLineNumber}");
                Telemetry.AddToCumulativeTelemetry(MochaTelemetryConstants.EventArea,
                    MochaTelemetryConstants.PendingSummaryMismatch, new List<int> { testRunToPublish.TestRunId }, true);
            }

            // Ensure some summary data was detected before attempting a publish, ie. check if the state is not test results state
            switch (_currentState)
            {
                case MochaParserStates.ExpectingTestResults:
                    if (testRunToPublish.PassedTests.Count != 0
                        || testRunToPublish.FailedTests.Count != 0
                        || testRunToPublish.SkippedTests.Count != 0)
                    {
                        Logger.Error("MochaTestResultParser : Skipping publish as testcases were encountered but no summary was encountered.");
                        Telemetry.AddToCumulativeTelemetry(MochaTelemetryConstants.EventArea,
                            MochaTelemetryConstants.PassedTestCasesFoundButNoPassedSummary, new List<int> { _stateContext.TestRun.TestRunId }, true);
                    }
                    break;

                default:
                    // Publish the test run if reset and publish was called from any state other than the test results state

                    // Calculate total tests
                    testRunToPublish.TestRunSummary.TotalTests =
                        testRunToPublish.TestRunSummary.TotalPassed +
                        testRunToPublish.TestRunSummary.TotalFailed +
                        testRunToPublish.TestRunSummary.TotalSkipped;

                    // Trim the stack traces of extra newlines etc.
                    foreach (var failedTest in testRunToPublish.FailedTests)
                    {
                        if (failedTest.StackTrace != null)
                        {
                            failedTest.StackTrace = failedTest.StackTrace.TrimEnd();
                        }
                    }

                    TestRunManager.PublishAsync(testRunToPublish);
                    break;
            }

            ResetParser();
        }

        /// <summary>
        /// Used to reset the parser inluding the test run and context
        /// </summary>
        private void ResetParser()
        {
            // Start a new TestRun
            var newTestRun = new TestRun($"{Name}/{Version}", "Mocha", _stateContext.TestRun.TestRunId + 1);

            // Set state to ExpectingTestResults
            _currentState = MochaParserStates.ExpectingTestResults;

            // Refresh the context
            _stateContext.Initialize(newTestRun);

            Logger.Info("MochaTestResultParser : Successfully reset the parser.");
        }

        private ITestResultParserState ExpectingTestResults => _expectingTestResults ??
            (_expectingTestResults = new MochaParserStateExpectingTestResults(AttemptPublishAndResetParser, Logger, Telemetry, nameof(MochaTestResultParser)));

        private ITestResultParserState ExpectingStackTraces => _expectingStackTraces ??
            (_expectingStackTraces = new MochaParserStateExpectingStackTraces(AttemptPublishAndResetParser, Logger, Telemetry, nameof(MochaTestResultParser)));

        private ITestResultParserState ExpectingTestRunSummary => _expectingTestRunSummary ??
            (_expectingTestRunSummary = new MochaParserStateExpectingTestRunSummary(AttemptPublishAndResetParser, Logger, Telemetry, nameof(MochaTestResultParser)));
    }
}
