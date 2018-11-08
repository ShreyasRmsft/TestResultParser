// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.TestResultParser.Parser.Node.Mocha
{
    using Agent.Plugins.TestResultParser.Parser.Interfaces;
    using Agent.Plugins.TestResultParser.Parser.Models;
    using Agent.Plugins.TestResultParser.Telemetry;
    using Agent.Plugins.TestResultParser.Telemetry.Interfaces;
    using Agent.Plugins.TestResultParser.TestResult.Models;
    using Agent.Plugins.TestResultParser.TestRunManger;
    using TestResult = TestResult.Models.TestResult;

    public class MochaTestResultParser : ITestResultParser
    {
        public TestRun TestRun = new TestRun { };

        private MochaTestResultParserStateContext stateContext = new MochaTestResultParserStateContext();

        private MochaTestResultParserStateModel state = MochaTestResultParserStateModel.ParsingTestResults;

        private IDiagnosticDataCollector diagnosticDataCollector;

        private ITelemetryDataCollector telemetryDataCollector;

        private ITestRunManager testRunManager;

        /// <summary>
        /// TODO
        /// </summary>
        /// <param name="testRunManager"></param>
        public MochaTestResultParser(ITestRunManager testRunManager) : this(testRunManager, DiagnosticDataCollector.Instance, TelemetryDataCollector.Instance)
        {

        }

        /// <summary>
        /// TODO
        /// </summary>
        /// <param name="testRunPublisher"></param>
        /// <param name="diagnosticDataCollector"></param>
        /// <param name="telemetryDataCollector"></param>
        public MochaTestResultParser(ITestRunManager testRunManager, IDiagnosticDataCollector diagnosticDataCollector, ITelemetryDataCollector telemetryDataCollector)
        {
            this.testRunManager = testRunManager;
            this.diagnosticDataCollector = diagnosticDataCollector;
            this.telemetryDataCollector = telemetryDataCollector;
        }

        /// <inheritdoc/>
        public void Parse(LogLineData testResultsLine)
        {
            switch (state)
            {
                case MochaTestResultParserStateModel.ParsingTestResults:

                    if (MatchPassedTestCase(testResultsLine.Line))
                    {
                        //LogIfDebug(testResultsLine, "MatchPassed");
                        return;
                    }
                    else if (MatchFailedTestCase(testResultsLine.Line))
                    {
                        //LogIfDebug(testResultsLine, "MatchPassedUnicode");
                        return;
                    }
                    else if (MatchPassedSummary(testResultsLine.Line))
                    {
                        //LogIfDebug(testResultsLine, "MatchPassedSummary");
                        return;
                    }

                    break;
            }
        }

        private bool MatchPassedTestCase(string testResultsLine)
        {
            var match = MochaTestResultParserRegularExpressions.PassedTestCaseMatcher.Match(testResultsLine);

            if (match.Success)
            {
                var testResult = new TestResult();

                testResult.Outcome = TestOutcome.Passed;
                testResult.Name = match.Groups[RegexCaptureGroups.TestCaseName].Value;

                // Also since this is an action performed in context of a state should there be a separate function?
                // Should this intelligence come from the caller?
                // TODO: Logic for resetting the run. This should also include a publish step if enough summary data was encountered
                if (state == MochaTestResultParserStateModel.ParsingTestRunSummary)
                {
                    StartNewTestResult();
                }

                TestRun.PassedTests.Add(testResult);

                return true;
            }

            return false;
        }

        private bool MatchFailedTestCase(string mochaTestResultsLine)
        {
            var match = MochaTestResultParserRegularExpressions.FailedTestCaseMatcher.Match(mochaTestResultsLine);

            if (match.Success)
            {
                var testResult = new TestResult();

                if (!int.TryParse(match.Groups[RegexCaptureGroups.FailedTestCaseNumber].Value, out int testCaseNumber))
                {
                    diagnosticDataCollector.Error("TODO");
                    telemetryDataCollector.AddProperty("TODO", "TODO");
                }

                if (testCaseNumber != stateContext.LastFailedTestCaseNumber + 1)
                {
                    diagnosticDataCollector.Error("TODO");
                    telemetryDataCollector.AddProperty("TODO", "TODO");
                    //throw new Exception($"Expecting failed test case with number {lastFailedTestCaseNumberEncountered + 1} but found {testCaseNumber} instead.");
                }

                stateContext.LastFailedTestCaseNumber++;

                if (stateContext.FailedTestsToSkipParsingPostSummary > 0)
                {
                    stateContext.FailedTestsToSkipParsingPostSummary--;
                    if (stateContext.FailedTestsToSkipParsingPostSummary == 0)
                    {
                        // we can also choose to ignore extra failures post summary if the number is not 1
                        stateContext.LastFailedTestCaseNumber = 0;
                    }

                    return true;
                }

                // Also since this is an action performed in context of a state should there be a separate function?
                // Should this intelligence come from the caller?
                // TODO: Logic for resetting the run. This should also include a publish step if enough summary data was encountered
                if (state == MochaTestResultParserStateModel.ParsingTestRunSummary)
                {
                    StartNewTestResult();
                }

                testResult.Outcome = TestOutcome.Failed;
                testResult.Name = match.Groups[RegexCaptureGroups.TestCaseName].Value;

                TestRun.PassedTests.Add(testResult);

                return true;
            }

            return false;
        }
    }
}
