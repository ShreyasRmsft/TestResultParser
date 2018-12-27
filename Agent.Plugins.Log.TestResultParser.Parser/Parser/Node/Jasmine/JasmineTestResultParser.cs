// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.Log.TestResultParser.Parser
{
    using System;
    using System.Collections.Generic;
    using Agent.Plugins.Log.TestResultParser.Contracts;

    public class JasmineTestResultParser : AbstractTestResultParser
    {
        private JasmineParserStates currentState;
        private JasmineParserStateContext stateContext;

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
            this.stateContext = new JasmineParserStateContext(testRun);
            this.currentState = JasmineParserStates.ExpectingTestRunStart;
        }

        public override void Parse(LogData logData)
        {
            if (logData == null || logData.Line == null)
            {
                this.logger.Error("JasmineTestResultParser : Parse : Input line was null.");
                return;
            }
            try
            {
                this.stateContext.CurrentLineNumber = logData.LineNumber;

                // State model for the jasmine parser that defines the Regexs to match against in each state
                switch (this.currentState)
                {
                    // This state primarily looks for test run start indicator and
                    // transitions to the next one after encountering one
                    case JasmineParserStates.ExpectingTestRunStart:

                        if (AttemptMatch(this.TestRunStart, logData))
                            return;
                        break;

                    // This state primarily looks for test results and transitions
                    // to the next one after a summary is encountered
                    case JasmineParserStates.ExpectingTestResults:

                        if (AttemptMatch(this.ExpectingTestResults, logData))
                            return;
                        break;

                    // This state primarily looks for test run summary 
                    // and transitions back to testrunstart state
                    case JasmineParserStates.ExpectingTestRunSummary:

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
                    this.logger.Info($"JasmineTestResultParser : Parse : Was expecting {this.stateContext.NextExpectedMatch} before line {logData.LineNumber}, but no matches occurred.");
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
                this.logger.Error($"JasmineTestResultParser : Parse : Failed with exception {e}.");

                // This might start taking a lot of space if each and every parse operation starts throwing
                // But if that happens then there's a lot more stuff broken.
                this.telemetry.AddToCumulativeTelemetry(JasmineTelemetryConstants.EventArea, JasmineTelemetryConstants.Exceptions, new List<string> { e.Message });

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
        /// <returns>True if a match occurs</returns>
        private bool AttemptMatch(ITestResultParserState state, LogData logData)
        {
            foreach (var regexActionPair in state.RegexsToMatch)
            {
                var match = regexActionPair.Regex.Match(logData.Line);
                if (match.Success)
                {
                    this.currentState = (JasmineParserStates)regexActionPair.MatchAction(match, this.stateContext);
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
            this.logger.Info($"JasmineTestResultParser : Resetting the parser and attempting to publish the test run at line {this.stateContext.CurrentLineNumber}.");
            var testRunToPublish = this.stateContext.TestRun;

            // We have encountered failed test cases but no failed summary was encountered
            if (testRunToPublish.FailedTests.Count != 0 && testRunToPublish.TestRunSummary.TotalFailed == 0)
            {
                this.logger.Error("JasmineTestResultParser : Failed tests were encountered but no failed summary was encountered.");
                this.telemetry.AddToCumulativeTelemetry(JasmineTelemetryConstants.EventArea,
                    JasmineTelemetryConstants.FailedTestCasesFoundButNoFailedSummary, new List<int> { this.stateContext.TestRun.TestRunId }, true);
            }
            else if (testRunToPublish.TestRunSummary.TotalFailed != testRunToPublish.FailedTests.Count)
            {
                // If encountered failed tests does not match summary fire telemetry
                this.logger.Error($"JasmineTestResultParser : Failed tests count does not match failed summary" +
                    $" at line {this.stateContext.CurrentLineNumber}");
                this.telemetry.AddToCumulativeTelemetry(JasmineTelemetryConstants.EventArea,
                    JasmineTelemetryConstants.FailedSummaryMismatch, new List<int> { testRunToPublish.TestRunId }, true);
            }

            if (testRunToPublish.SkippedTests.Count != 0 && testRunToPublish.TestRunSummary.TotalSkipped == 0)
            {
                this.logger.Error("JasmineTestResultParser : Skipped tests were encountered but no skipped summary was encountered.");
                this.telemetry.AddToCumulativeTelemetry(JasmineTelemetryConstants.EventArea,
                    JasmineTelemetryConstants.SkippedTestCasesFoundButNoSkippedSummary, new List<int> { this.stateContext.TestRun.TestRunId }, true);
            }
            else if (testRunToPublish.TestRunSummary.TotalSkipped != testRunToPublish.SkippedTests.Count)
            {
                // If encountered skipped tests does not match summary fire telemetry
                this.logger.Error($"JasmineTestResultParser : Pending tests count does not match pending summary" +
                    $" at line {this.stateContext.CurrentLineNumber}");
                this.telemetry.AddToCumulativeTelemetry(JasmineTelemetryConstants.EventArea,
                    JasmineTelemetryConstants.SkippedSummaryMismatch, new List<int> { testRunToPublish.TestRunId }, true);
            }

            // Ensure some summary data was detected before attempting a publish, ie. check if the state is not test results state
            switch (this.currentState)
            {
                case JasmineParserStates.ExpectingTestRunStart:

                    this.logger.Error("JasmineTestResultParser : Skipping publish as no test cases or summary has been encountered.");
                    this.telemetry.AddToCumulativeTelemetry(JasmineTelemetryConstants.EventArea,
                            JasmineTelemetryConstants.NoSummaryEncounteredBeforePublish, new List<int> { this.stateContext.TestRun.TestRunId }, true);

                    break;

                case JasmineParserStates.ExpectingTestResults:
                    if (testRunToPublish.PassedTests.Count != 0
                        || testRunToPublish.FailedTests.Count != 0
                        || testRunToPublish.SkippedTests.Count != 0)
                    {
                        this.logger.Error("JasmineTestResultParser : Skipping publish as testcases were encountered but no summary was encountered.");
                        this.telemetry.AddToCumulativeTelemetry(JasmineTelemetryConstants.EventArea,
                            JasmineTelemetryConstants.PassedTestCasesFoundButNoPassedSummary, new List<int> { this.stateContext.TestRun.TestRunId }, true);
                    }
                    break;

                case JasmineParserStates.ExpectingTestRunSummary:

                    if (testRunToPublish.TestRunSummary.TotalTests == 0)
                    {
                        this.logger.Error("JasmineTestResultParser : Skipping publish as total tests was 0.");
                        this.telemetry.AddToCumulativeTelemetry(JasmineTelemetryConstants.EventArea,
                            JasmineTelemetryConstants.TotalTestsZero, new List<int> { this.stateContext.TestRun.TestRunId }, true);
                        break;
                    }

                    if (this.stateContext.IsTimeParsed == false)
                    {
                        this.logger.Error("JasmineTestResultParser : Total test run time was not parsed.");
                        this.telemetry.AddToCumulativeTelemetry(JasmineTelemetryConstants.EventArea,
                            JasmineTelemetryConstants.TotalTestRunTimeNotParsed, new List<int> { this.stateContext.TestRun.TestRunId }, true);
                    }

                    if (this.stateContext.SuiteErrors > 0)
                    {
                        // Adding telemetry for suite errors
                        this.logger.Info($"JasmineTestResultParser : {this.stateContext.SuiteErrors} suite errors found in the test run.");
                        this.telemetry.AddToCumulativeTelemetry(JasmineTelemetryConstants.EventArea, JasmineTelemetryConstants.SuiteErrors,
                            new List<int> { this.stateContext.TestRun.TestRunId }, true);
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
            this.currentState = JasmineParserStates.ExpectingTestRunStart;

            // Refresh the context
            this.stateContext.Initialize(newTestRun);

            this.logger.Info("JasmineTestResultParser : Successfully reset the parser.");
        }

        private ITestResultParserState TestRunStart => this.testRunStart ??
            (this.testRunStart = new JasmineParserStateExpectingTestRunStart(AttemptPublishAndResetParser, this.logger, this.telemetry));

        private ITestResultParserState ExpectingTestResults => this.expectingTestResults ??
            (this.expectingTestResults = new JasmineParserStateExpectingTestResults(AttemptPublishAndResetParser, this.logger, this.telemetry));

        private ITestResultParserState ExpectingTestRunSummary => this.expectingTestRunSummary ??
            (this.expectingTestRunSummary = new JasmineParserStateExpectingTestRunSummary(AttemptPublishAndResetParser, this.logger, this.telemetry));
    }
}