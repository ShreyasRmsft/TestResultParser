// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.TestResultParser.TestRunManger
{
    using Agent.Plugins.TestResultParser.Loggers;
    using Agent.Plugins.TestResultParser.Loggers.Interfaces;
    using Agent.Plugins.TestResultParser.Publish.Interfaces;
    using Agent.Plugins.TestResultParser.Telemetry;
    using Agent.Plugins.TestResultParser.Telemetry.Interfaces;
    using Agent.Plugins.TestResultParser.TestResult.Models;
    using Agent.Plugins.TestResultParser.TestRunManager;
    using System.Linq;

    /// <inheritdoc/>
    public class TestRunManager : ITestRunManager
    {
        private ITestRunPublisher publisher;
        private ITraceLogger diagnosticDataCollector;
        private ITelemetryDataCollector telemetryDataCollector;

        /// <summary>
        /// Construct the TestRunManger
        /// </summary>
        /// <param name="testRunPublisher"></param>
        public TestRunManager(ITestRunPublisher testRunPublisher) : this(testRunPublisher, TraceLogger.Instance, TelemetryDataCollector.Instance)
        {
        }

        /// <summary>
        /// Construct the TestRunManger
        /// </summary>
        /// <param name="testRunPublisher"></param>
        public TestRunManager(ITestRunPublisher testRunPublisher, ITraceLogger diagnosticDataCollector, ITelemetryDataCollector telemetryDataCollector)
        {
            this.publisher = testRunPublisher;
            this.diagnosticDataCollector = diagnosticDataCollector;
            this.telemetryDataCollector = telemetryDataCollector;
        }

        /// <inheritdoc/>
        public void Publish(TestRun testRun)
        {
            var validatedTestRun = this.ValidateAndPrepareForPublish(testRun);
            if (validatedTestRun != null)
            {
                publisher.Publish(validatedTestRun);
            }
        }

        /// <inheritdoc/>
        private TestRun ValidateAndPrepareForPublish(TestRun testRun)
        {
            if (testRun == null || testRun.TestRunSummary == null)
            {
                this.diagnosticDataCollector.Error("TestRunManger.ValidateAndPrepareForPublish : TestRun or TestRunSummary is null.");
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.TestRunNull, 1, true);
                return null;
            }

            // Test run should have a valid parser uri associated with it
            if (string.IsNullOrWhiteSpace(testRun.ParserUri))
            {
                this.diagnosticDataCollector.Error("TestRunManger.ValidateAndPrepareForPublish : TestRun did not have a valid parser uri associated with it.");
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.ParserUriEmpty, 1, true);
                return null;
            }

            this.diagnosticDataCollector.Info($"Attempting to publish test run with id {testRun.TestRunId} received from parser {testRun.ParserUri}.");

            // Test run id should be non zero for a valid test run
            if (testRun.TestRunId == 0)
            {
                this.diagnosticDataCollector.Error("TestRunManger.ValidateAndPrepareForPublish : TestRunId was not set. Expected a non zero test run id.");
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.TestRunIdZero, 1, true);
                return null;
            }

            // TotalTests count should always be less than passed and failed test count combined
            if (testRun.TestRunSummary.TotalTests < testRun.TestRunSummary.TotalFailed 
                + testRun.TestRunSummary.TotalPassed + testRun.TestRunSummary.TotalSkipped)
            {
                this.diagnosticDataCollector.Error("TestRunManger.ValidateAndPrepareForPublish : Total test count reported is less than sum of passed and failed tests.");
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.CountMismatch, 1, true);
                return null;
            }

            // Match the passed test count and clear the passed tests collection if mismatch occurs
            if (testRun.TestRunSummary.TotalPassed != testRun.PassedTests?.Count())
            {
                this.diagnosticDataCollector.Warning("TestRunManger.ValidateAndPrepareForPublish : Passed test count does not match the Test summary.");
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.CountMismatch, 1, true);
                testRun.PassedTests = null;
            }

            // Match the failed test count and clear the failed tests collection if mismatch occurs
            if (testRun.TestRunSummary.TotalFailed != testRun.FailedTests?.Count())
            {
                this.diagnosticDataCollector.Warning("TestRunManger.ValidateAndPrepareForPublish : Failed test count does not match the Test summary.");
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.CountMismatch, 1, true);
                testRun.FailedTests = null;
            }

            // Match the skipped test count and clear the skipped tests collection if mismatch occurs
            if (testRun.TestRunSummary.TotalSkipped != testRun.SkippedTests?.Count())
            {
                this.diagnosticDataCollector.Warning("TestRunManger.ValidateAndPrepareForPublish : Skipped test count does not match the Test summary.");
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.CountMismatch, 1, true);
                testRun.SkippedTests = null;
            }

            this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, testRun.ParserUri, 1, true);

            return testRun;
        }
    }
}
