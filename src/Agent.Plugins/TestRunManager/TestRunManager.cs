// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.TestResultParser.TestRunManger
{
    using Agent.Plugins.TestResultParser.Loggers.Interfaces;
    using Agent.Plugins.TestResultParser.Publish.Interfaces;
    using Agent.Plugins.TestResultParser.Telemetry.Interfaces;
    using Agent.Plugins.TestResultParser.TestResult.Models;
    using System.Linq;

    /// <inheritdoc/>
    public class TestRunManager : ITestRunManager
    {
        private ITestRunPublisher publisher;
        private ITraceLogger diagnosticDataCollector;
        private ITelemetryDataCollector telemetryDataCollector;

        // TODO: Add a default constructor

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
                return null;
            }

            // TotalTests count should always be less than passed and failed test count combined
            if (testRun.TestRunSummary.TotalTests < testRun.TestRunSummary.TotalFailed + testRun.TestRunSummary.TotalPassed)
            {
                this.diagnosticDataCollector.Error("TestRunManger.ValidateAndPrepareForPublish : Total test count reported is less than sum of passed and failed tests.");
                return null;
            }

            // Match the passed test count and clear the passed tests collection if mismatch occurs
            if (testRun.TestRunSummary.TotalPassed != testRun.PassedTests?.Count())
            {
                this.diagnosticDataCollector.Warning("TestRunManger.ValidateAndPrepareForPublish : Passed test count does not match the Test summary.");
                testRun.PassedTests = null;
            }

            // Match the failed test count and clear the failed tests collection if mismatch occurs
            if (testRun.TestRunSummary.TotalFailed != testRun.FailedTests?.Count())
            {
                this.diagnosticDataCollector.Warning("TestRunManger.ValidateAndPrepareForPublish : Failed test count does not match the Test summary.");
                testRun.FailedTests = null;
            }

            return testRun;
        }
    }
}
