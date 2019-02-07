using System.Collections.Generic;
using System.IO;
using Agent.Plugins.Log.TestResultParser.Parser;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Agent.Plugins.UnitTests.PythonTestResultParserTests
{
    [TestClass]
    public class PythonTestResultParserTests : TestResultParserTestBase
    {
        [TestInitialize]
        public void TestInit()
        {
            this.Parser = new PythonTestResultParser(TestRunManagerMock.Object, DiagnosticDataCollector.Object, TelemetryDataCollector.Object);
            this.IsPythonParser = true;
        }

        [DataTestMethod]
        [DynamicData(nameof(GetSuccessScenariosTestCases), DynamicDataSourceType.Method)]
        public void PythonTestResultParserSuccessScenariosWithBasicAssertions(string testCase)
        {
            testCase = Path.Combine("PythonTestResultParserTests", "Resources", "SuccessScenarios", testCase);
            TestSuccessScenariosWithBasicAssertions(testCase);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetPartialSuccessTestCases), DynamicDataSourceType.Method)]
        public void PythonTestResultParserPartialSuccessScenariosWithBasicAssertions(string testCase)
        {
            testCase = Path.Combine("PythonTestResultParserTests", "Resources", "PartialSuccess", testCase);
            TestPartialSuccessScenariosWithBasicAssertions(testCase);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetDetailedTestCases), DynamicDataSourceType.Method)]
        public void PythonTestResultParserDetailedAssertions(string testCase)
        {
            testCase = Path.Combine("PythonTestResultParserTests", "Resources", "DetailedTests", testCase);
            TestWithDetailedAssertions(testCase);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetStackTraceTestCases), DynamicDataSourceType.Method)]
        public void PythonParserStackTraceTests(string testCase)
        {
            testCase = Path.Combine("PythonTestResultParserTests", "Resources", "StackTraceTests", testCase);
            TestWithStackTraceAssertions(testCase);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetCommonNegativeTestsTestCases), DynamicDataSourceType.Method)]
        public void PythonTestResultParserCommonNegativeTests(string testCase)
        {
            testCase = Path.Combine("CommonTestResources", "NegativeTests", testCase);
            TestNegativeTestsScenarios(testCase);
        }

        public static IEnumerable<object[]> GetSuccessScenariosTestCases()
        {
            return GetTestCasesFromRelativePath(Path.Combine("PythonTestResultParserTests", "Resources", "SuccessScenarios"));
        }

        public static IEnumerable<object[]> GetPartialSuccessTestCases()
        {
            return GetTestCasesFromRelativePath(Path.Combine("PythonTestResultParserTests", "Resources", "PartialSuccess"));
        }

        public static IEnumerable<object[]> GetDetailedTestCases()
        {
            return GetTestCasesFromRelativePath(Path.Combine("PythonTestResultParserTests", "Resources", "DetailedTests"));
        }

        public static IEnumerable<object[]> GetStackTraceTestCases()
        {
            return GetTestCasesFromRelativePath(Path.Combine("PythonTestResultParserTests", "Resources", "StackTraceTests"));
        }

        public static IEnumerable<object[]> GetCommonNegativeTestsTestCases()
        {
            return GetTestCasesFromRelativePath(Path.Combine("CommonTestResources", "NegativeTests"));
        }
    }
}
