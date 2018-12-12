// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.Log.TestResultParser.Parser
{
    using System;
    using System.Collections.Generic;
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

        private JestParserStates currentState;
        private JestParserStateContext stateContext;

        private ITestResultParserState testRunStart;
        private ITestResultParserState expectingTestResults;
        private ITestResultParserState expectingStackTraces;
        private ITestResultParserState expectingTestRunSummary;

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
            telemetryDataCollector.AddToCumulativeTelemetry(JestTelemetryConstants.EventArea, JestTelemetryConstants.Initialize, true);

            // Initialize the starting state of the parser
            var testRun = new TestRun($"{Name}/{Version}", 1);
            this.stateContext = new JestParserStateContext(testRun);
            this.currentState = JestParserStates.ExpectingTestRunStart;
        }

        /// <inheritdoc/>
        public override void Parse(LogData logData)
        {
            if (logData == null || logData.Line == null)
            {
                this.logger.Error("JestTestResultParser : Parse : Input line was null.");
                return;
            }

            try
            {
                this.stateContext.CurrentLineNumber = logData.LineNumber;

                // State model for the jest parser that defines the Regexs to match against in each state
                // Each state re-orders the Regexs based on the frequency of expected matches
                switch (this.currentState)
                {
                    // This state primarily looks for test run start indicator and
                    // transitions to the next one after encountering one
                    case JestParserStates.ExpectingTestRunStart:

                        if (AttemptMatch(this.TestRunStart, logData))
                            return;
                        break;

                    // This state primarily looks for test results and transitions
                    // to the next one after a stack trace or summary is encountered
                    case JestParserStates.ExpectingTestResults:

                        if (AttemptMatch(this.ExpectingTestResults, logData))
                            return;
                        break;

                    // This state primarily looks for stack traces/failed test cases
                    // and transitions on encountering summary
                    case JestParserStates.ExpectingStackTraces:

                        if (AttemptMatch(this.ExpectingStackTraces, logData))
                            return;
                        break;

                    // This state primarily looks for test run summary 
                    // and transitions back to testresults state on encountering
                    // another test run start marker indicating tests being run from
                    // more than one file
                    case JestParserStates.ExpectingTestRunSummary:

                        if (AttemptMatch(this.ExpectingTestRunSummary, logData))
                            return;
                        break;
                }

                // This is a mechanism to enforce matches that have to occur within 
                // a specific number of lines after encountering the previous match
                // one obvious usage is for successive summary lines which
                // come one after the other
                if (this.stateContext.LinesWithinWhichMatchIsExpected == 1)
                {
                    this.logger.Info($"JestTestResultParser : Parse : Was expecting {this.stateContext.NextExpectedMatch} before line {logData.LineNumber}, but no matches occurred.");
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
                this.logger.Error($"JestTestResultParser : Parse : Failed with exception {e}.");

                // This might start taking a lot of space if each and every parse operation starts throwing
                // But if that happens then there's a lot more stuff broken.
                this.telemetry.AddToCumulativeTelemetry(JestTelemetryConstants.EventArea, "Exceptions", new List<string> { e.Message });

                // Rethrowing this so that the plugin is aware that the parser is erroring out
                // Ideally this would never should happen
                throw;
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
            foreach (var regexActionPair in state.RegexsToMatch)
            {
                var match = regexActionPair.Regex.Match(logData.Line);
                if (match.Success)
                {
                    this.currentState = (JestParserStates)regexActionPair.MatchAction(match, this.stateContext);
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
            this.logger.Info($"JestTestResultParser : Resetting the parser and attempting to publish the test run at line {this.stateContext.CurrentLineNumber}.");
            var testRunToPublish = this.stateContext.TestRun;

            // We have encountered passed test cases but no passed summary was encountered
            if (testRunToPublish.PassedTests.Count != 0 && testRunToPublish.TestRunSummary.TotalPassed == 0)
            {
                this.logger.Error("JestTestResultParser : Passed tests were encountered but no passed summary was encountered.");
                this.telemetry.AddToCumulativeTelemetry(JestTelemetryConstants.EventArea,
                    JestTelemetryConstants.PassedTestCasesFoundButNoPassedSummary, new List<int> { this.stateContext.TestRun.TestRunId }, true);
            }
            else if (this.stateContext.VerboseOptionEnabled && testRunToPublish.TestRunSummary.TotalPassed != testRunToPublish.PassedTests.Count)
            {
                // If encountered failed tests does not match summary fire telemtry
                this.logger.Error($"JestTestResultParser : Passed tests count does not match passed summary" +
                    $" at line {this.stateContext.CurrentLineNumber}");
                this.telemetry.AddToCumulativeTelemetry(JestTelemetryConstants.EventArea,
                    JestTelemetryConstants.PassedSummaryMismatch, new List<int> { testRunToPublish.TestRunId }, true);
            }

            // We have encountered failed test cases but no failed summary was encountered
            if (testRunToPublish.FailedTests.Count != 0 && testRunToPublish.TestRunSummary.TotalFailed == 0)
            {
                this.logger.Error("JestTestResultParser : Failed tests were encountered but no failed summary was encountered.");
                this.telemetry.AddToCumulativeTelemetry(JestTelemetryConstants.EventArea,
                    JestTelemetryConstants.FailedTestCasesFoundButNoFailedSummary, new List<int> { this.stateContext.TestRun.TestRunId }, true);
            }
            else if (testRunToPublish.TestRunSummary.TotalFailed != testRunToPublish.FailedTests.Count)
            {
                // If encountered failed tests does not match summary fire telemtry
                this.logger.Error($"JestTestResultParser : Failed tests count does not match failed summary" +
                    $" at line {this.stateContext.CurrentLineNumber}");
                this.telemetry.AddToCumulativeTelemetry(JestTelemetryConstants.EventArea,
                    JestTelemetryConstants.FailedSummaryMismatch, new List<int> { testRunToPublish.TestRunId }, true);
            }

            // Ensure some summary data was detected before attempting a publish, ie. check if the state is not test results state
            switch (this.currentState)
            {
                case JestParserStates.ExpectingTestRunStart:

                    this.logger.Error("JestTestResultParser : Skipping publish as no test cases or summary has been encountered.");

                    break;

                case JestParserStates.ExpectingTestResults:

                case JestParserStates.ExpectingStackTraces:

                    if (testRunToPublish.PassedTests.Count != 0
                        || testRunToPublish.FailedTests.Count != 0
                        || testRunToPublish.SkippedTests.Count != 0)
                    {
                        this.logger.Error("JestTestResultParser : Skipping publish as testcases were encountered but no summary was encountered.");
                        this.telemetry.AddToCumulativeTelemetry(JestTelemetryConstants.EventArea,
                            JestTelemetryConstants.TestCasesFoundButNoSummary, new List<int> { this.stateContext.TestRun.TestRunId }, true);
                    }

                    break;

                case JestParserStates.ExpectingTestRunSummary:

                    if (testRunToPublish.TestRunSummary.TotalTests == 0)
                    {
                        this.logger.Error("JestTestResultParser : Skipping publish as total tests was 0.");
                        this.telemetry.AddToCumulativeTelemetry(JestTelemetryConstants.EventArea,
                            JestTelemetryConstants.TotalTestsZero, new List<int> { this.stateContext.TestRun.TestRunId }, true);
                        break;
                    }

                    if (testRunToPublish.TestRunSummary.TotalExecutionTime.TotalMilliseconds == 0)
                    {
                        this.logger.Error("JestTestResultParser : Total test run time was 0 or not encountered.");
                        this.telemetry.AddToCumulativeTelemetry(JestTelemetryConstants.EventArea,
                            JestTelemetryConstants.TotalTestRunTimeZero, new List<int> { this.stateContext.TestRun.TestRunId }, true);
                    }

                    // Only publish if total tests was not zero
                    this.testRunManager.PublishAsync(testRunToPublish);

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
            var newTestRun = new TestRun($"{Name}/{Version}", this.stateContext.TestRun.TestRunId + 1);

            // Set state to ExpectingTestResults
            this.currentState = JestParserStates.ExpectingTestResults;

            // Refresh the context
            this.stateContext.Initialize(newTestRun);

            this.logger.Info("JestTestResultParser : Successfully reset the parser.");
        }

        private ITestResultParserState TestRunStart => this.testRunStart ??
            (this.testRunStart = new JestParserStateExpectingTestRunStart(AttemptPublishAndResetParser, this.logger, this.telemetry));

        private ITestResultParserState ExpectingTestResults => this.expectingTestResults ??
            (this.expectingTestResults = new JestParserStateExpectingTestResults(AttemptPublishAndResetParser, this.logger, this.telemetry));

        private ITestResultParserState ExpectingStackTraces => this.expectingStackTraces ??
            (this.expectingStackTraces = new JestParserStateExpectingStackTraces(AttemptPublishAndResetParser, this.logger, this.telemetry));

        private ITestResultParserState ExpectingTestRunSummary => this.expectingTestRunSummary ??
            (this.expectingTestRunSummary = new JestParserStateExpectingTestRunSummary(AttemptPublishAndResetParser, this.logger, this.telemetry));
    }
}