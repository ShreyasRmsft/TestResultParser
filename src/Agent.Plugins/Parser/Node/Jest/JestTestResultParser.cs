// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.TestResultParser.Parser.Node.Jest
{
    using System;
    using System.Collections.Generic;
    using Agent.Plugins.TestResultParser.Loggers;
    using Agent.Plugins.TestResultParser.Loggers.Interfaces;
    using Agent.Plugins.TestResultParser.Parser.Interfaces;
    using Agent.Plugins.TestResultParser.Parser.Models;
    using Agent.Plugins.TestResultParser.Telemetry;
    using Agent.Plugins.TestResultParser.Telemetry.Interfaces;
    using Agent.Plugins.TestResultParser.TestResult.Models;
    using Agent.Plugins.TestResultParser.TestRunManger;

    public class JestTestResultParser
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

        private readonly ITestResultParserState expectingTestResults;
        private readonly ITestResultParserState expectingTestRunSummary;
        private readonly ITestResultParserState expectingStackTraces;

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
            this.currentState = JestParserStates.ExpectingTestResults;

            this.expectingTestResults = new ExpectingTestResults(AttemptPublishAndResetParser, logger, telemetryDataCollector);
            this.expectingTestRunSummary = new ExpectingTestRunSummary(AttemptPublishAndResetParser, logger, telemetryDataCollector);
            this.expectingStackTraces = new ExpectingStackTraces(AttemptPublishAndResetParser, logger, telemetryDataCollector);
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

                // State model for the mocha parser that defines the regexes to match against in each state
                // Each state re-orders the regexes based on the frequency of expected matches
                switch (this.currentState)
                {
                    // This state primarily looks for test results 
                    // and transitions to the next one after a line of summary is encountered
                    case JestParserStates.ExpectingTestResults:

                        if (AttemptMatch(this.expectingTestResults, testResultsLine))
                            return;
                        break;

                    // This state primarily looks for test run summary 
                    // If failed tests were found to be present transitions to the next one to look for stack traces
                    // else goes back to the first state after publishing the run
                    case JestParserStates.ExpectingTestRunSummary:

                        if (AttemptMatch(this.expectingTestRunSummary, testResultsLine))
                            return;
                        break;

                    // This state primarily looks for stack traces
                    // If any other match occurs before all the expected stack traces are found it 
                    // fires telemetry for unexpected behavior but moves on to the next test run
                    case JestParserStates.ExpectingStackTraces:

                        if (AttemptMatch(this.expectingStackTraces, testResultsLine))
                            return;
                        break;
                }

                //// This is a mechanism to enforce matches that have to occur within 
                //// a specific number of lines after encountering the previous match
                //// one obvious usage is for successive summary lines containing passed,
                //// pending and failed test summary
                //if (this.stateContext.LinesWithinWhichMatchIsExpected == 1)
                //{
                //    this.logger.Info($"MochaTestResultParser : Parse : Was expecting {this.stateContext.ExpectedMatch} before line {testResultsLine.LineNumber}, but no matches occurred.");
                //    AttemptPublishAndResetParser();
                //    return;
                //}

                //// If no match occurred and a match was expected in a positive number of lines, decrement the counter
                //// A value of zero or lesser indicates not expecting a match
                //if (this.stateContext.LinesWithinWhichMatchIsExpected > 1)
                //{
                //    this.stateContext.LinesWithinWhichMatchIsExpected--;
                //    return;
                //}
            }
            catch (Exception e)
            {
                this.logger.Error($"MochaTestResultParser : Parse : Failed with exception {e}.");

                // This might start taking a lot of space if each and every parse operation starts throwing
                // But if that happens then there's a lot more stuff broken.
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, "Exceptions", new List<string> { e.Message });

                // Rethrowing this so that the plugin is aware that the parser is erroring out
                // Ideally this never should happen
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
    }
}
