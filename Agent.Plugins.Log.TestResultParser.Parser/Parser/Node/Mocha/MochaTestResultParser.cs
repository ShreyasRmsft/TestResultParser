// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Agent.Plugins.Log.TestResultParser.Contracts;

namespace Agent.Plugins.Log.TestResultParser.Parser
{
    public class MochaTestResultParser : AbstractTestResultParser
    {
        // TODO: Need a hook for end of logs.
        // Needed for multiple reasons. Scenarios where i am expecting things and have not yet published the run
        // Needed where I have encoutered test results but got no summary
        // It is true that it can be inferred due to the absense of the summary event, but I would like there to
        // be one telemetry event per parser run

        // TODO: Decide on a reset if no match found withing x lines logic after a previous match.
        // This can be fine tuned depending on the previous match
        // Infra already in place for this

        private MochaParserStates currentState;
        private MochaParserStateContext stateContext;

        private ITestResultParserState expectingTestResults;
        private ITestResultParserState expectingTestRunSummary;
        private ITestResultParserState expectingStackTraces;

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
            var testRun = new TestRun($"{Name}/{Version}", 1);
            this.stateContext = new MochaParserStateContext(testRun);
            this.currentState = MochaParserStates.ExpectingTestResults;

            this.expectingTestResults = new MochaParserStateExpectingTestResults(AttemptPublishAndResetParser, logger, telemetryDataCollector);
            this.expectingTestRunSummary = new MochaParserStateExpectingTestRunSummary(AttemptPublishAndResetParser, logger, telemetryDataCollector);
            this.expectingStackTraces = new MochaParserStateExpectingStackTraces(AttemptPublishAndResetParser, logger, telemetryDataCollector);
        }

        /// <inheritdoc/>
        public override void Parse(LogData logData)
        {
            if (logData == null || logData.Line == null)
            {
                this.logger.Error("MochaTestResultParser : Parse : Input line was null.");
                return;
            }

            try
            {
                this.stateContext.CurrentLineNumber = logData.LineNumber;

                // State model for the mocha parser that defines the Regexs to match against in each state
                // Each state re-orders the Regexs based on the frequency of expected matches
                switch (this.currentState)
                {
                    // This state primarily looks for test results 
                    // and transitions to the next one after a line of summary is encountered
                    case MochaParserStates.ExpectingTestResults:

                        if (AttemptMatch(this.ExpectingTestResults, logData))
                            return;
                        break;

                    // This state primarily looks for test run summary 
                    // If failed tests were found to be present transitions to the next one to look for stack traces
                    // else goes back to the first state after publishing the run
                    case MochaParserStates.ExpectingTestRunSummary:

                        if (AttemptMatch(this.ExpectingTestRunSummary, logData))
                            return;
                        break;

                    // This state primarily looks for stack traces
                    // If any other match occurs before all the expected stack traces are found it 
                    // fires telemetry for unexpected behavior but moves on to the next test run
                    case MochaParserStates.ExpectingStackTraces:

                        if (AttemptMatch(this.ExpectingStackTraces, logData))
                            return;
                        break;
                }

                // This is a mechanism to enforce matches that have to occur within 
                // a specific number of lines after encountering the previous match
                // one obvious usage is for successive summary lines containing passed,
                // pending and failed test summary
                if (this.stateContext.LinesWithinWhichMatchIsExpected == 1)
                {
                    this.logger.Info($"MochaTestResultParser : Parse : Was expecting {this.stateContext.NextExpectedMatch} before line {logData.LineNumber}, but no matches occurred.");
                    AttemptPublishAndResetParser();
                    return;
                }

                // If no match occurred and a match was expected in a positive number of lines, decrement the counter
                // A value of zero or lesser indicates not expecting a match
                if (this.stateContext.LinesWithinWhichMatchIsExpected > 1)
                {
                    this.stateContext.LinesWithinWhichMatchIsExpected--;
                    return;
                }
            }
            catch (Exception e)
            {
                this.logger.Error($"MochaTestResultParser : Parse : Failed with exception {e}.");

                // This might start taking a lot of space if each and every parse operation starts throwing
                // But if that happens then there's a lot more stuff broken.
                this.telemetry.AddToCumulativeTelemetry(MochaTelemetryConstants.EventArea, "Exceptions", new List<string> { e.Message });

                // Rethrowing this so that the plugin is aware that the parser is erroring out
                // Ideally this never should happen
                throw;
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
            foreach (var regexActionPair in state.RegexsToMatch)
            {
                var match = regexActionPair.Regex.Match(logData.Line);
                if (match.Success)
                {
                    this.currentState = (MochaParserStates)regexActionPair.MatchAction(match, this.stateContext);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Publishes the run and resets the parser by resetting the state context and current state
        /// </summary>
        private void AttemptPublishAndResetParser()
        {
            this.logger.Info($"MochaTestResultParser : Resetting the parser and attempting to publish the test run at line {this.stateContext.CurrentLineNumber}.");
            var testRunToPublish = this.stateContext.TestRun;

            // We have encountered failed test cases but no failed summary was encountered
            if (testRunToPublish.FailedTests.Count != 0 && testRunToPublish.TestRunSummary.TotalFailed == 0)
            {
                this.logger.Error("MochaTestResultParser : Failed tests were encountered but no failed summary was encountered.");
                this.telemetry.AddToCumulativeTelemetry(MochaTelemetryConstants.EventArea,
                    MochaTelemetryConstants.FailedTestCasesFoundButNoFailedSummary, new List<int> { this.stateContext.TestRun.TestRunId }, true);
            }
            else if (testRunToPublish.TestRunSummary.TotalFailed != testRunToPublish.FailedTests.Count)
            {
                // If encountered failed tests does not match summary fire telemtry
                this.logger.Error($"MochaTestResultParser : Failed tests count does not match failed summary" +
                    $" at line {this.stateContext.CurrentLineNumber}");
                this.telemetry.AddToCumulativeTelemetry(MochaTelemetryConstants.EventArea,
                    MochaTelemetryConstants.FailedSummaryMismatch, new List<int> { testRunToPublish.TestRunId }, true);
            }

            // We have encountered pending test cases but no pending summary was encountered
            if (testRunToPublish.SkippedTests.Count != 0 && testRunToPublish.TestRunSummary.TotalSkipped == 0)
            {
                this.logger.Error("MochaTestResultParser : Skipped tests were encountered but no skipped summary was encountered.");
                this.telemetry.AddToCumulativeTelemetry(MochaTelemetryConstants.EventArea,
                    MochaTelemetryConstants.PendingTestCasesFoundButNoFailedSummary, new List<int> { this.stateContext.TestRun.TestRunId }, true);
            }
            else if (testRunToPublish.TestRunSummary.TotalSkipped != testRunToPublish.SkippedTests.Count)
            {
                // If encountered skipped tests does not match summary fire telemetry
                this.logger.Error($"MochaTestResultParser : Pending tests count does not match pending summary" +
                    $" at line {this.stateContext.CurrentLineNumber}");
                this.telemetry.AddToCumulativeTelemetry(MochaTelemetryConstants.EventArea,
                    MochaTelemetryConstants.PendingSummaryMismatch, new List<int> { testRunToPublish.TestRunId }, true);
            }

            // Ensure some summary data was detected before attempting a publish, ie. check if the state is not test results state
            switch (this.currentState)
            {
                case MochaParserStates.ExpectingTestResults:
                    if (testRunToPublish.PassedTests.Count != 0
                        || testRunToPublish.FailedTests.Count != 0
                        || testRunToPublish.SkippedTests.Count != 0)
                    {
                        this.logger.Error("MochaTestResultParser : Skipping publish as testcases were encountered but no summary was encountered.");
                        this.telemetry.AddToCumulativeTelemetry(MochaTelemetryConstants.EventArea,
                            MochaTelemetryConstants.PassedTestCasesFoundButNoPassedSummary, new List<int> { this.stateContext.TestRun.TestRunId }, true);
                    }
                    break;

                default:
                    // Publish the test run if reset and publish was called from any state other than the test results state

                    // Calculate total tests
                    testRunToPublish.TestRunSummary.TotalTests =
                        testRunToPublish.TestRunSummary.TotalPassed +
                        testRunToPublish.TestRunSummary.TotalFailed +
                        testRunToPublish.TestRunSummary.TotalSkipped;

                    this.testRunManager.PublishAsync(testRunToPublish);
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
            var newTestRun = new TestRun($"{Name}/{Version}", this.stateContext.TestRun.TestRunId + 1);

            // Set state to ExpectingTestResults
            this.currentState = MochaParserStates.ExpectingTestResults;

            // Refresh the context
            this.stateContext.Initialize(newTestRun);

            this.logger.Info("MochaTestResultParser : Successfully reset the parser.");
        }


        private ITestResultParserState ExpectingTestResults => this.expectingTestResults ??
            (this.expectingTestResults = new MochaParserStateExpectingTestResults(AttemptPublishAndResetParser, this.logger, this.telemetry));

        private ITestResultParserState ExpectingStackTraces => this.expectingStackTraces ??
            (this.expectingStackTraces = new MochaParserStateExpectingStackTraces(AttemptPublishAndResetParser, this.logger, this.telemetry));

        private ITestResultParserState ExpectingTestRunSummary => this.expectingTestRunSummary ??
            (this.expectingTestRunSummary = new MochaParserStateExpectingTestRunSummary(AttemptPublishAndResetParser, this.logger, this.telemetry));
    }
}