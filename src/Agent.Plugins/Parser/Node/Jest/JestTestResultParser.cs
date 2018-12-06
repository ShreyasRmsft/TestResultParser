﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.TestResultParser.Parser.Node.Jest
{
    using System;
    using System.Collections.Generic;
    using Agent.Plugins.TestResultParser.Loggers;
    using Agent.Plugins.TestResultParser.Loggers.Interfaces;
    using Agent.Plugins.TestResultParser.Parser.Interfaces;
    using Agent.Plugins.TestResultParser.Parser.Models;
    using Agent.Plugins.TestResultParser.Parser.Node.Jest.States;
    using Agent.Plugins.TestResultParser.Telemetry;
    using Agent.Plugins.TestResultParser.Telemetry.Interfaces;
    using Agent.Plugins.TestResultParser.TestResult.Models;
    using Agent.Plugins.TestResultParser.TestRunManger;

    public class JestTestResultParser : ITestResultParser
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

        private readonly ITraceLogger logger;
        private readonly ITelemetryDataCollector telemetryDataCollector;
        private readonly ITestRunManager testRunManager;

        private readonly ITestResultParserState testRunStart;
        private readonly ITestResultParserState expectingTestResults;
        private readonly ITestResultParserState expectingStackTraces;
        private readonly ITestResultParserState expectingTestRunSummary;

        public string Name => nameof(JestTestResultParser);

        public string Version => "1.0";

        /// <summary>
        /// Default constructor accepting only test run manager instance, rest of the requirements assume default values
        /// </summary>
        /// <param name="testRunManager"></param>
        public JestTestResultParser(ITestRunManager testRunManager) : this(testRunManager, TraceLogger.Instance, TelemetryDataCollector.Instance)
        {

        }

        /// <summary>
        /// Detailed constructor where specified logger and telemetry data collector are initialized along with test run manager
        /// </summary>
        /// <param name="testRunPublisher"></param>
        /// <param name="diagnosticDataCollector"></param>
        /// <param name="telemetryDataCollector"></param>
        public JestTestResultParser(ITestRunManager testRunManager, ITraceLogger logger, ITelemetryDataCollector telemetryDataCollector)
        {
            logger.Info("MochaTestResultParser : Starting mocha test result parser.");
            telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.Initialize, true);

            this.testRunManager = testRunManager;
            this.logger = logger;
            this.telemetryDataCollector = telemetryDataCollector;

            // Initialize the starting state of the parser
            var testRun = new TestRun($"{Name}/{Version}", 1);
            this.stateContext = new JestParserStateContext(testRun);
            this.currentState = JestParserStates.ExpectingTestRunStart;

            this.testRunStart = new ExpectingTestRunStart(AttemptPublishAndResetParser, logger, telemetryDataCollector);
            this.expectingTestResults = new ExpectingTestResults(AttemptPublishAndResetParser, logger, telemetryDataCollector);
            this.expectingStackTraces = new ExpectingStackTraces(AttemptPublishAndResetParser, logger, telemetryDataCollector);
            this.expectingTestRunSummary = new ExpectingTestRunSummary(AttemptPublishAndResetParser, logger, telemetryDataCollector);
            
        }

        /// <inheritdoc/>
        public void Parse(LogData testResultsLine)
        {
            if (testResultsLine == null || testResultsLine.Line == null)
            {
                this.logger.Error("MochaTestResultParser : Parse : Input line was null.");
                return;
            }

            try
            {
                this.stateContext.CurrentLineNumber = testResultsLine.LineNumber;

                // State model for the jest parser that defines the regexes to match against in each state
                // Each state re-orders the regexes based on the frequency of expected matches
                switch (this.currentState)
                {
                    // This state primarily looks for test run start indicator and
                    // transitions to the next one after encountering one
                    case JestParserStates.ExpectingTestRunStart:

                        if (AttemptMatch(this.expectingTestResults, testResultsLine))
                            return;
                        break;

                    // This state primarily looks for test results and transitions
                    // to the next one after a stack trace or summary is encountered
                    case JestParserStates.ExpectingTestResults:

                        if (AttemptMatch(this.expectingTestResults, testResultsLine))
                            return;
                        break;

                    // This state primarily looks for stack traces/failed test cases
                    // and transitions on encountering summary
                    case JestParserStates.ExpectingStackTraces:

                        if (AttemptMatch(this.expectingStackTraces, testResultsLine))
                            return;
                        break;

                    // This state primarily looks for test run summary 
                    // and transitions back to testresults state on encountering
                    // another test run start marker indicating tests being run from
                    // more than one file
                    case JestParserStates.ExpectingTestRunSummary:

                        if (AttemptMatch(this.expectingTestRunSummary, testResultsLine))
                            return;
                        break;
                }

                // This is a mechanism to enforce matches that have to occur within 
                // a specific number of lines after encountering the previous match
                // one obvious usage is for successive summary lines which
                // come one after the other
                if (this.stateContext.LinesWithinWhichMatchIsExpected == 1)
                {
                    this.logger.Info($"JestTestResultParser : Parse : Was expecting {this.stateContext.ExpectedMatch} before line {testResultsLine.LineNumber}, but no matches occurred.");
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
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, "Exceptions", new List<string> { e.Message });

                // Rethrowing this so that the plugin is aware that the parser is erroring out
                // Ideally this would never should happen
                throw;
            }
        }

        /// <summary>
        /// Attempts to match the line with each regex specified by the current state
        /// </summary>
        /// <param name="state">Current state</param>
        /// <param name="testResultsLine">Input line</param>
        /// <returns>True if a match occurs</returns>
        private bool AttemptMatch(ITestResultParserState state, LogData testResultsLine)
        {
            foreach (var regexActionPair in state.RegexesToMatch)
            {
                var match = regexActionPair.Regex.Match(testResultsLine.Line);
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

            //// We have encountered failed test cases but no failed summary was encountered
            //if (testRunToPublish.FailedTests.Count != 0 && testRunToPublish.TestRunSummary.TotalFailed == 0)
            //{
            //    this.logger.Error("MochaTestResultParser : Failed tests were encountered but no failed summary was encountered.");
            //    this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea,
            //        TelemetryConstants.FailedTestCasesFoundButNoFailedSummary, new List<int> { this.stateContext.TestRun.TestRunId }, true);
            //}
            //else if (testRunToPublish.TestRunSummary.TotalFailed != testRunToPublish.FailedTests.Count)
            //{
            //    // If encountered failed tests does not match summary fire telemtry
            //    this.logger.Error($"MochaTestResultParser : Failed tests count does not match failed summary" +
            //        $" at line {this.stateContext.CurrentLineNumber}");
            //    this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea,
            //        TelemetryConstants.PassedSummaryMismatch, new List<int> { testRunToPublish.TestRunId }, true);
            //}

            //// We have encountered pending test cases but no pending summary was encountered
            //if (testRunToPublish.SkippedTests.Count != 0 && testRunToPublish.TestRunSummary.TotalSkipped == 0)
            //{
            //    this.logger.Error("MochaTestResultParser : Skipped tests were encountered but no skipped summary was encountered.");
            //    this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea,
            //        TelemetryConstants.PendingTestCasesFoundButNoFailedSummary, new List<int> { this.stateContext.TestRun.TestRunId }, true);
            //}
            //else if (testRunToPublish.TestRunSummary.TotalSkipped != testRunToPublish.SkippedTests.Count)
            //{
            //    // If encountered skipped tests does not match summary fire telemetry
            //    this.logger.Error($"MochaTestResultParser : Pending tests count does not match pending summary" +
            //        $" at line {this.stateContext.CurrentLineNumber}");
            //    this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea,
            //        TelemetryConstants.PendingSummaryMismatch, new List<int> { testRunToPublish.TestRunId }, true);
            //}

            // Ensure some summary data was detected before attempting a publish, ie. check if the state is not test results state
            switch (this.currentState)
            {
                case JestParserStates.ExpectingTestRunStart:

                    //this.logger.Error("MochaTestResultParser : Skipping publish as testcases were encountered but no summary was encountered.");
                    //this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea,
                    //    TelemetryConstants.PassedTestCasesFoundButNoPassedSummary, new List<int> { this.stateContext.TestRun.TestRunId }, true);

                    break;

                case JestParserStates.ExpectingTestResults:
                    if (testRunToPublish.PassedTests.Count != 0
                        || testRunToPublish.FailedTests.Count != 0
                        || testRunToPublish.SkippedTests.Count != 0)
                    {
                        //this.logger.Error("MochaTestResultParser : Skipping publish as testcases were encountered but no summary was encountered.");
                        //this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea,
                        //    TelemetryConstants.PassedTestCasesFoundButNoPassedSummary, new List<int> { this.stateContext.TestRun.TestRunId }, true);
                    }
                    break;

                case JestParserStates.ExpectingStackTraces:
                    if (testRunToPublish.PassedTests.Count != 0
                        || testRunToPublish.FailedTests.Count != 0
                        || testRunToPublish.SkippedTests.Count != 0)
                    {
                        //this.logger.Error("MochaTestResultParser : Skipping publish as testcases were encountered but no summary was encountered.");
                        //this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea,
                        //    TelemetryConstants.PassedTestCasesFoundButNoPassedSummary, new List<int> { this.stateContext.TestRun.TestRunId }, true);
                    }
                    break;

                default:
                    // Publish the test run if reset and publish was called from any state other than the test results state

                    // Calculate total tests
                    testRunToPublish.TestRunSummary.TotalTests =
                        testRunToPublish.TestRunSummary.TotalPassed +
                        testRunToPublish.TestRunSummary.TotalFailed +
                        testRunToPublish.TestRunSummary.TotalSkipped;

                    this.testRunManager.Publish(testRunToPublish);
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
            this.currentState = JestParserStates.ExpectingTestResults;

            // Refresh the context
            this.stateContext.Initialize(newTestRun);

            //this.logger.Info("MochaTestResultParser : Successfully reset the parser.");
        }
    }
}