namespace Agent.Plugins.UnitTests.PythonTestResultParserTests
{
    using Agent.Plugins.TestResultParser.Parser.Python;
    using Agent.Plugins.UnitTests.MochaTestResultParserTests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System.Collections.Generic;
    using System.IO;
    
    [TestClass]
    public class PythonTestResultParserTests : TestResultParserTestBase
    {
        [TestInitialize]
        public void TestInit()
        {
            this.parser = new PythonTestResultParser(testRunManagerMock.Object, diagnosticDataCollector.Object, telemetryDataCollector.Object);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetSuccessScenariosTestCases), DynamicDataSourceType.Method)]
        public void SuccessScenariosWithBasicAssertions(string testCase)
        {
            testCase = Path.Combine("PythonTestResultParserTests", "Resources", "SuccessScenarios", testCase);
            TestSuccessScenariosWithBasicAssertions(testCase);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetPartialSuccessTestCases), DynamicDataSourceType.Method)]
        public void PartialSuccessScenariosWithBasicAssertions(string testCase)
        {
            testCase = Path.Combine("PythonTestResultParserTests", "Resources", "PartialSuccess", testCase);
            TestPartialSuccessScenariosWithBasicAssertions(testCase);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetDetailedTestCases), DynamicDataSourceType.Method)]
        public void DetailedAssertions(string testCase)
        {
            testCase = Path.Combine("PythonTestResultParserTests", "Resources", "DetailedTests", testCase);
            TestWithDetailedAssertions(testCase);
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
    }
}
