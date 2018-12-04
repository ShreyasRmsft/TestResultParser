// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.UnitTests
{
    using System.Collections.Generic;
    using Agent.Plugins.TestResultParser.Loggers.Interfaces;
    using Agent.Plugins.TestResultParser.Publish.Interfaces;
    using Agent.Plugins.TestResultParser.Telemetry.Interfaces;
    using Agent.Plugins.TestResultParser.TestResult.Models;
    using Agent.Plugins.TestResultParser.TestRunManger;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using TestResult = Plugins.TestResultParser.TestResult.Models.TestResult;

    [TestClass]
    public class TestRunManagerTests
    {
        private TestRunManager testRunManager;
        private Mock<ITestRunPublisher> publisher;
        private Mock<ITraceLogger> diagnosticDataCollector;
        private Mock<ITelemetryDataCollector> telemetryDataCollector;

        public TestRunManagerTests()
        {
            this.publisher = new Mock<ITestRunPublisher>();
            this.diagnosticDataCollector = new Mock<ITraceLogger>();
            this.telemetryDataCollector = new Mock<ITelemetryDataCollector>();
            this.testRunManager = new TestRunManager(publisher.Object, diagnosticDataCollector.Object, telemetryDataCollector.Object);
        }

        [TestMethod]
        public void PublishShouldReturnNullForTestRunIdZero()
        {
            var testRun = new TestRun("XyzTestResultParser/1.0", 0);
            this.testRunManager.Publish(testRun);

            this.publisher.Verify(x => x.Publish(It.IsAny<TestRun>()), Times.Never);
            this.diagnosticDataCollector.Verify(x => x.Error("TestRunManger.ValidateAndPrepareForPublish : TestRunId was not set. Expected a non zero test run id."));
        }

        [TestMethod]
        public void PublishShouldReturnNullForEmptyParserUri()
        {
            this.testRunManager.Publish(new TestRun("", 1));

            this.publisher.Verify(x => x.Publish(It.IsAny<TestRun>()), Times.Never);
            this.diagnosticDataCollector.Verify(x => x.Error("TestRunManger.ValidateAndPrepareForPublish : TestRun did not have a valid parser uri associated with it."));
        }

        [TestMethod]
        public void PublishForNullTestRunShouldNotPublishAndLogError()
        {
            this.testRunManager.Publish(null);

            this.publisher.Verify(x => x.Publish(It.IsAny<TestRun>()), Times.Never);
            this.diagnosticDataCollector.Verify(x => x.Error("TestRunManger.ValidateAndPrepareForPublish : TestRun or TestRunSummary is null."));
        }

        [TestMethod]
        public void PublishForTestRunWithNullTestSummaryShouldNotPublishAndLogError()
        {
            TestRun testRun = new TestRun("Dummy", 1);
            testRun.TestRunSummary = null;

            this.testRunManager.Publish(testRun);

            this.publisher.Verify(x => x.Publish(It.IsAny<TestRun>()), Times.Never);
            this.diagnosticDataCollector.Verify(x => x.Error("TestRunManger.ValidateAndPrepareForPublish : TestRun or TestRunSummary is null."));
        }

        [TestMethod]
        public void PublishForTestRunWithInvalidTestSummaryShouldNotPublishAndLogError()
        {
            TestRun testRun = new TestRun("Dummy", 1);
            testRun.TestRunSummary = this.CreateTestRunSummary(4, 3, 1, 1);

            this.testRunManager.Publish(testRun);

            this.publisher.Verify(x => x.Publish(It.IsAny<TestRun>()), Times.Never);
            this.diagnosticDataCollector.Verify(x=>x.Error("TestRunManger.ValidateAndPrepareForPublish : Total test count reported is less than sum of passed and failed tests."));
        }

        [TestMethod]
        public void PublishForTestRunWithNoResultsAndValidTestSummaryShouldPublishTheTestRun()
        {
            TestRun testRun = new TestRun("Dummy", 1) { TestRunSummary = this.CreateTestRunSummary(4, 2, 1) };

            this.testRunManager.Publish(testRun);

            this.publisher.Verify(x => x.Publish(testRun), Times.Once);
        }

        [TestMethod]
        public void PublishForTestRunWithMismatchedFailedTestResultsAndTestSummaryShouldPublishTestRunWithFailedTestsCleared()
        {
            var passedTestList = this.CreateTestList(2, TestOutcome.Passed);
            var failedTestList = this.CreateTestList(2, TestOutcome.Failed);
            var skippedTestList = this.CreateTestList(0, TestOutcome.Skipped);

            var summary = this.CreateTestRunSummary(4, 2, 1);
            TestRun testRun = new TestRun("Dummy", 1) { PassedTests = passedTestList, FailedTests = failedTestList, SkippedTests = skippedTestList, TestRunSummary = summary };

            TestRun resultTestRun = null;
            this.publisher.Setup(x => x.Publish(It.IsAny<TestRun>())).Callback((TestRun t) => resultTestRun = t);

            this.testRunManager.Publish(testRun);

            this.diagnosticDataCollector.Verify(x => x.Warning("TestRunManger.ValidateAndPrepareForPublish : Failed test count does not match the Test summary."));
            this.publisher.Verify(x => x.Publish(It.IsAny<TestRun>()), Times.Once);
            this.VerifyTestRun(resultTestRun, summary, passedTestList, null, skippedTestList);
        }

        [TestMethod]
        public void PublishForTestRunWithMismatchedPassedTestResultsAndTestSummaryShouldPublishTestRunWithPassedTestsCleared()
        {
            var passedTestList = this.CreateTestList(1, TestOutcome.Passed);
            var failedTestList = this.CreateTestList(1, TestOutcome.Failed);
            var skippedTestList = this.CreateTestList(0, TestOutcome.Skipped);

            var summary = this.CreateTestRunSummary(4, 2, 1);
            TestRun testRun = new TestRun("Dummy", 1) { PassedTests = passedTestList, FailedTests = failedTestList, SkippedTests = skippedTestList, TestRunSummary = summary };

            TestRun resultTestRun = null;
            this.publisher.Setup(x => x.Publish(It.IsAny<TestRun>())).Callback((TestRun t) => resultTestRun = t);

            this.testRunManager.Publish(testRun);

            this.diagnosticDataCollector.Verify(x => x.Warning("TestRunManger.ValidateAndPrepareForPublish : Passed test count does not match the Test summary."));
            this.publisher.Verify(x => x.Publish(It.IsAny<TestRun>()), Times.Once);
            this.VerifyTestRun(resultTestRun, summary, null, failedTestList, skippedTestList);
        }

        [TestMethod]
        public void PublishForTestRunWithNoFailedTestResultsAndTestSummaryContainingFailuresShouldPublishTestRunWithFailedTestsCleared()
        {
            var passedTestList = this.CreateTestList(2, TestOutcome.Passed);
            var failedTestList = this.CreateTestList(2, TestOutcome.Failed);
            var skippedTestList = this.CreateTestList(0, TestOutcome.Skipped);

            var summary = this.CreateTestRunSummary(4, 2, 1);
            TestRun testRun = new TestRun("Dummy", 1) { PassedTests = passedTestList, FailedTests = failedTestList, SkippedTests = skippedTestList, TestRunSummary = summary };

            TestRun resultTestRun = null;
            this.publisher.Setup(x => x.Publish(It.IsAny<TestRun>())).Callback((TestRun t) => resultTestRun = t);

            this.testRunManager.Publish(testRun);

            this.publisher.Verify(x => x.Publish(It.IsAny<TestRun>()), Times.Once);
            this.VerifyTestRun(resultTestRun, summary, passedTestList, null, skippedTestList);
        }

        [TestMethod]
        public void PublishForTestRunWithMismatchedPassedTestResultsAndTestSummaryShouldPublishTestRunWithSkippedTestsCleared()
        {
            var passedTestList = this.CreateTestList(0, TestOutcome.Passed);
            var skippedTestList = this.CreateTestList(1, TestOutcome.Skipped);
            var failedTestList = this.CreateTestList(1, TestOutcome.Failed);

            var summary = this.CreateTestRunSummary(4, 0, 1, 2);
            TestRun testRun = new TestRun("Dummy", 1) { PassedTests = passedTestList, FailedTests = failedTestList, SkippedTests = skippedTestList, TestRunSummary = summary };

            TestRun resultTestRun = null;
            this.publisher.Setup(x => x.Publish(It.IsAny<TestRun>())).Callback((TestRun t) => resultTestRun = t);

            this.testRunManager.Publish(testRun);

            this.diagnosticDataCollector.Verify(x => x.Warning("TestRunManger.ValidateAndPrepareForPublish : Skipped test count does not match the Test summary."));
            this.publisher.Verify(x => x.Publish(It.IsAny<TestRun>()), Times.Once);
            this.VerifyTestRun(resultTestRun, summary, passedTestList, failedTestList, null);
        }

        [TestMethod]
        public void PublishForTestRunWithValidTestResultsAndTestSummaryShouldPublishTestRun()
        {
            var passedTestList = this.CreateTestList(2, TestOutcome.Passed);
            var failedTestList = this.CreateTestList(1, TestOutcome.Failed);
            var skippedTestList = this.CreateTestList(0, TestOutcome.Skipped);

            var summary = this.CreateTestRunSummary(4, 2, 1);
            TestRun testRun = new TestRun("Dummy", 1) { PassedTests = passedTestList, FailedTests = failedTestList, SkippedTests = skippedTestList, TestRunSummary = summary };

            TestRun resultTestRun = null;
            this.publisher.Setup(x => x.Publish(It.IsAny<TestRun>())).Callback((TestRun t) => resultTestRun = t);

            this.testRunManager.Publish(testRun);

            this.publisher.Verify(x => x.Publish(It.IsAny<TestRun>()), Times.Once);
            this.VerifyTestRun(resultTestRun, summary, passedTestList, failedTestList, skippedTestList);
        }

        private TestRunSummary CreateTestRunSummary(int totalTests, int totalPassed = 0, int totalFailed = 0, int totalSkipped = 0)
        {
            return new TestRunSummary
            {
                TotalTests = totalTests,
                TotalPassed = totalPassed,
                TotalFailed = totalFailed,
                TotalSkipped = totalSkipped,
                TotalExecutionTime = new System.TimeSpan(0, 0, 0, 1, 50)
            };
        }

        private List<TestResult> CreateTestList(int numberOfTests, TestOutcome outcome)
        {
            var testList = new List<TestResult>();
            for (int i = 1; i <= numberOfTests; i++)
            {
                testList.Add(new TestResult { Name = outcome.ToString() + i, Outcome = outcome });
            }
            return testList;
        }

        private void VerifyTestRun(TestRun testRun, TestRunSummary expectedSummary, List<TestResult> expectedPassed = null,
            List<TestResult> expectedFailed = null, List<TestResult> expectedSkipped = null)
        {
            Assert.AreEqual(expectedPassed, testRun.PassedTests);
            Assert.AreEqual(expectedFailed, testRun.FailedTests);
            Assert.AreEqual(expectedSkipped, testRun.SkippedTests);
            Assert.AreEqual(expectedSummary, testRun.TestRunSummary);

            if (expectedSummary != null)
            {
                if (expectedPassed != null) Assert.AreEqual(expectedSummary.TotalPassed, testRun.PassedTests.Count);
                if (expectedFailed != null) Assert.AreEqual(expectedSummary.TotalFailed, testRun.FailedTests.Count);
                if (expectedSkipped != null) Assert.AreEqual(expectedSummary.TotalSkipped, testRun.SkippedTests.Count);
            }
        }
    }
}
