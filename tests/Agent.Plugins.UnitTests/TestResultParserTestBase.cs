// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.UnitTests.MochaTestResultParserTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.RegularExpressions;
    using Agent.Plugins.TestResultParser.Loggers.Interfaces;
    using Agent.Plugins.TestResultParser.Parser.Interfaces;
    using Agent.Plugins.TestResultParser.Parser.Models;
    using Agent.Plugins.TestResultParser.Telemetry.Interfaces;
    using Agent.Plugins.TestResultParser.TestResult.Models;
    using Agent.Plugins.TestResultParser.TestRunManger;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    public abstract class TestResultParserTestBase
    {
        protected Mock<ITraceLogger> diagnosticDataCollector;
        protected Mock<ITelemetryDataCollector> telemetryDataCollector;
        protected Mock<ITestRunManager> testRunManagerMock;
        protected ITestResultParser parser;

        public TestResultParserTestBase()
        {
            testRunManagerMock = new Mock<ITestRunManager>();

            // Mock logger to log to console for easy debugging
            diagnosticDataCollector = new Mock<ITraceLogger>();

            diagnosticDataCollector.Setup(x => x.Info(It.IsAny<string>())).Callback<string>(data => { Console.WriteLine($"Info: {data}"); });
            diagnosticDataCollector.Setup(x => x.Info(It.IsAny<string>())).Callback<string>(data => { Console.WriteLine($"Verbose: {data}"); });
            diagnosticDataCollector.Setup(x => x.Info(It.IsAny<string>())).Callback<string>(data => { Console.WriteLine($"Warning: {data}"); });
            diagnosticDataCollector.Setup(x => x.Info(It.IsAny<string>())).Callback<string>(data => { Console.WriteLine($"Error: {data}"); });

            // No-op for telemetry
            telemetryDataCollector = new Mock<ITelemetryDataCollector>();
        }

        public void TestSuccessScenariosWithBasicAssertions(string testCase)
        {
            int indexOfTestRun = 0;
            int lastTestRunId = 0;
            var resultFileContents = File.ReadAllLines($"{testCase}Result.txt");
            
            testRunManagerMock.Setup(x => x.Publish(It.IsAny<TestRun>())).Callback<TestRun>(testRun =>
            {
                ValidateTestRun(testRun, resultFileContents, indexOfTestRun++, lastTestRunId);
                lastTestRunId = testRun.TestRunId;
            });

            foreach (var line in GetLines(testCase))
            {
                this.parser.Parse(line);
            }

            testRunManagerMock.Verify(x => x.Publish(It.IsAny<TestRun>()), Times.Exactly(resultFileContents.Length / 4), $"Expected {resultFileContents.Length / 4 } test runs.");
            Assert.AreEqual(resultFileContents.Length / 5, indexOfTestRun, $"Expected {resultFileContents.Length / 4} test runs.");
        }

        public void TestPartialSuccessScenariosWithBasicAssertions(string testCase)
        {
            int indexOfTestRun = 0;
            int lastTestRunId = 0;
            var resultFileContents = File.ReadAllLines($"{testCase}Result.txt");

            testRunManagerMock.Setup(x => x.Publish(It.IsAny<TestRun>())).Callback<TestRun>(testRun =>
            {
                ValidatePartialSuccessTestRun(testRun, resultFileContents, indexOfTestRun++, lastTestRunId);
                lastTestRunId = testRun.TestRunId;
            });

            foreach (var line in GetLines(testCase))
            {
                this.parser.Parse(line);
            }

            testRunManagerMock.Verify(x => x.Publish(It.IsAny<TestRun>()), Times.Exactly(resultFileContents.Length / 7), $"Expected {resultFileContents.Length / 7 } test runs.");
            Assert.AreEqual(resultFileContents.Length / 8, indexOfTestRun, $"Expected {resultFileContents.Length / 7} test runs.");
        }

        public void TestWithDetailedAssertions(string testCase)
        {
            var resultFileContents = File.ReadAllLines($"{testCase}Result.txt");

            testRunManagerMock.Setup(x => x.Publish(It.IsAny<TestRun>())).Callback<TestRun>(testRun =>
            {
                ValidateTestRunWithDetails(testRun, resultFileContents);
            });

            foreach (var line in GetLines(testCase))
            {
                this.parser.Parse(line);
            }

            testRunManagerMock.Verify(x => x.Publish(It.IsAny<TestRun>()), Times.Once, $"Expected a test run to have been published.");
        }

        public void TestNegativeTestsScenarios(string testCase)
        {
            foreach (var line in GetLines(testCase))
            {
                this.parser.Parse(line);
            }

            testRunManagerMock.Verify(x => x.Publish(It.IsAny<TestRun>()), Times.Never, $"Expected no test run to have been published.");
        }

        #region Log Data Utilities

        public IEnumerable<LogData> GetLines(string testCase)
        {
            var testResultsConsoleOut = File.ReadAllLines($"{testCase}.txt");
            int lineNumber = 1;
            foreach (var line in testResultsConsoleOut)
            {
                yield return new LogData() { Line = RemoveTimeStampFromLogLineIfPresent(line), LineNumber = lineNumber++ };
            }
        }

        public static IEnumerable<object[]> GetTestCasesFromRelativePath(string relativePathToTestCase)
        {
            foreach (var testCase in new DirectoryInfo(relativePathToTestCase).GetFiles("TestCase*.txt"))
            {
                if (!testCase.Name.EndsWith("Result.txt"))
                {
                    // Uncomment the below line to run for a particular test case for debugging 
                    // if (testCase.Name.Contains("TestCase002"))
                    yield return new object[] { testCase.FullName.Split(".txt")[0] };
                }
            }
        }
        public string RemoveTimeStampFromLogLineIfPresent(string line)
        {
            // Remove the preceding timestamp if present.
            var trimTimeStamp = new Regex("^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}\\.[0-9]{7}Z (?<TrimmedLog>.*)", RegexOptions.ExplicitCapture);
            var match = trimTimeStamp.Match(line);

            if (match.Success)
            {
                return match.Groups["TrimmedLog"].Value;
            }

            return line;
        }

        #endregion

        #region ValidationHelpers

        public void ValidateTestRun(TestRun testRun, string[] resultFileContents, int indexOfTestRun, int lastTestRunId)
        {
            int i = indexOfTestRun * 5;

            int expectedPassedTestsCount = int.Parse(resultFileContents[i + 0]);
            int expectedFailedTestsCount = int.Parse(resultFileContents[i + 1]);
            int expectedSkippedTestsCount = int.Parse(resultFileContents[i + 2]);
            int expectedTotalTestsCount = int.Parse(resultFileContents[i + 3]);
            long expectedTestRunDuration = long.Parse(resultFileContents[i + 4]);

            Assert.AreEqual(expectedPassedTestsCount, testRun.TestRunSummary.TotalPassed, "Passed tests summary does not match.");
            Assert.AreEqual(expectedFailedTestsCount, testRun.TestRunSummary.TotalFailed, "Failed tests summary does not match.");
            Assert.AreEqual(expectedSkippedTestsCount, testRun.TestRunSummary.TotalSkipped, "Skipped tests summary does not match.");
            Assert.AreEqual(expectedTotalTestsCount, testRun.TestRunSummary.TotalTests, "Total tests summary does not match.");

            Assert.AreEqual(expectedPassedTestsCount, testRun.PassedTests.Count, "Passed tests count does not match.");
            Assert.AreEqual(expectedFailedTestsCount, testRun.FailedTests.Count, "Failed tests count does not match.");
            Assert.AreEqual(expectedSkippedTestsCount, testRun.SkippedTests.Count, "Skipped tests count does not match.");

            Assert.IsTrue(testRun.TestRunId > lastTestRunId, $"Expected test run id greater than {lastTestRunId} but found {testRun.TestRunId} instead.");
            Assert.AreEqual(expectedTestRunDuration, testRun.TestRunSummary.TotalExecutionTime.TotalMilliseconds, "Test run duration did not match.");
        }

        public void ValidatePartialSuccessTestRun(TestRun testRun, string[] resultFileContents, int indexOfTestRun, int lastTestRunId)
        {
            int i = indexOfTestRun * 8;

            int expectedPassedTestsSummaryCount = int.Parse(resultFileContents[i + 0]);
            int expectedPassedTestsCount = int.Parse(resultFileContents[i + 1]);
            int expectedFailedTestsSummary = int.Parse(resultFileContents[i + 2]);
            int expectedFailedTestsCount = int.Parse(resultFileContents[i + 3]);
            int expectedSkippedTestsSummaryCount = int.Parse(resultFileContents[i + 4]);
            int expectedSkippedTestsCount = int.Parse(resultFileContents[i + 5]);
            int expectedTotalTestsCount = int.Parse(resultFileContents[i + 6]);
            long expectedTestRunDuration = long.Parse(resultFileContents[i + 7]);

            Assert.AreEqual(expectedPassedTestsSummaryCount, testRun.TestRunSummary.TotalPassed, "Passed tests summary does not match.");
            Assert.AreEqual(expectedFailedTestsSummary, testRun.TestRunSummary.TotalFailed, "Failed tests summary does not match.");
            Assert.AreEqual(expectedSkippedTestsSummaryCount, testRun.TestRunSummary.TotalSkipped, "Skipped tests summary does not match.");
            Assert.AreEqual(expectedTotalTestsCount, testRun.TestRunSummary.TotalTests, "Total tests summary does not match.");

            Assert.AreEqual(expectedPassedTestsCount, testRun.PassedTests.Count, "Passed tests count does not match.");
            Assert.AreEqual(expectedFailedTestsCount, testRun.FailedTests.Count, "Failed tests count does not match.");
            Assert.AreEqual(expectedSkippedTestsCount, testRun.SkippedTests.Count, "Skipped tests count does not match.");

            Assert.IsTrue(testRun.TestRunId > lastTestRunId, $"Expected test run id greater than {lastTestRunId} but found {testRun.TestRunId} instead.");
            Assert.AreEqual(expectedTestRunDuration, testRun.TestRunSummary.TotalExecutionTime.TotalMilliseconds, "Test run duration did not match.");
        }

        public void ValidateTestRunWithDetails(TestRun testRun, string[] resultFileContents)
        {
            int currentLine = 0;
            int expectedPassedTestsCount = int.Parse(resultFileContents[currentLine].Split(" ")[1]);
            currentLine++;

            Assert.AreEqual(expectedPassedTestsCount, testRun.PassedTests.Count, "Passed tests count does not match.");
            for (int testIndex = 0; testIndex < expectedPassedTestsCount; currentLine++, testIndex++)
            {
                Assert.AreEqual(resultFileContents[currentLine], testRun.PassedTests[testIndex].Name, "Test Case name does not match.");
            }

            currentLine++;
            int expectedFailedTestsCount = int.Parse(resultFileContents[currentLine].Split(" ")[1]);
            currentLine++;

            Assert.AreEqual(expectedFailedTestsCount, testRun.FailedTests.Count, "Failed tests count does not match.");
            for (int testIndex = 0; testIndex < expectedFailedTestsCount; currentLine++, testIndex++)
            {
                Assert.AreEqual(resultFileContents[currentLine], testRun.FailedTests[testIndex].Name, "Test Case name does not match.");
            }

            currentLine++;
            int expectedSkippedTestsCount = int.Parse(resultFileContents[currentLine].Split(" ")[1]);
            currentLine++;

            Assert.AreEqual(expectedSkippedTestsCount, testRun.SkippedTests.Count, "Skipped tests count does not match.");
            for (int testIndex = 0; testIndex < expectedSkippedTestsCount; currentLine++, testIndex++)
            {
                Assert.AreEqual(resultFileContents[currentLine], testRun.SkippedTests[testIndex].Name, "Test Case name does not match.");
            }
        }

        #endregion
    }
}
