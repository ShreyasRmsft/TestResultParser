// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using System.Collections.Generic;
using System.IO;
using Agent.Plugins.Log.TestResultParser.Parser;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Agent.Plugins.UnitTests.JestTestResultParserTests
{
    [TestClass]
    public class JestTestResultParserTests : TestResultParserTestBase
    {
        [TestInitialize]
        public void TestInit()
        {
            this.parser = new JestTestResultParser(this.testRunManagerMock.Object, this.diagnosticDataCollector.Object, this.telemetryDataCollector.Object);
        }

        #region DataDrivenTests

       [DataTestMethod]
       [DynamicData(nameof(GetSuccessScenariosTestCases), DynamicDataSourceType.Method)]
       public void SuccessScenariosWithBasicAssertions(string testCase)
       {
           testCase = Path.Combine("JestTestResultParserTests", "Resources", "SuccessScenarios", testCase);
           TestSuccessScenariosWithBasicAssertions(testCase, true, false, false);
       }

        [DataTestMethod]
        [DynamicData(nameof(GetPartialSuccessTestCases), DynamicDataSourceType.Method)]
        public void PartialSuccessScenariosWithBasicAssertions(string testCase)
        {
            testCase = Path.Combine("JestTestResultParserTests", "Resources", "PartialSuccess", testCase);
            TestPartialSuccessScenariosWithBasicAssertions(testCase);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetDetailedTestsTestCases), DynamicDataSourceType.Method)]
        public void DetailedAssertions(string testCase)
        {
            testCase = Path.Combine("JestTestResultParserTests", "Resources", "DetailedTests", testCase);
            TestWithDetailedAssertions(testCase);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetNegativeTestsTestCases), DynamicDataSourceType.Method)]
        public void NegativeTests(string testCase)
        {
            testCase = Path.Combine("JestTestResultParserTests", "Resources", "NegativeTests", testCase);
            TestNegativeTestsScenarios(testCase);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetCommonNegativeTestsTestCases), DynamicDataSourceType.Method)]
        public void CommonNegativeTests(string testCase)
        {
            testCase = Path.Combine("CommonTestResources", "NegativeTests", testCase);
            TestNegativeTestsScenarios(testCase);
        }

        #endregion

        #region Data Drivers

        public static IEnumerable<object[]> GetSuccessScenariosTestCases()
        {
            return GetTestCasesFromRelativePath(Path.Combine("JestTestResultParserTests", "Resources", "SuccessScenarios"));
        }

        public static IEnumerable<object[]> GetPartialSuccessTestCases()
        {
            return GetTestCasesFromRelativePath(Path.Combine("JestTestResultParserTests", "Resources", "PartialSuccess"));
        }

        public static IEnumerable<object[]> GetDetailedTestsTestCases()
        {
            return GetTestCasesFromRelativePath(Path.Combine("JestTestResultParserTests", "Resources", "DetailedTests"));
        }

        public static IEnumerable<object[]> GetNegativeTestsTestCases()
        {
            return GetTestCasesFromRelativePath(Path.Combine("JestTestResultParserTests", "Resources", "NegativeTests"));
        }

        public static IEnumerable<object[]> GetCommonNegativeTestsTestCases()
        {
            return GetTestCasesFromRelativePath(Path.Combine("CommonTestResources", "NegativeTests"));
        }

        #endregion
    }
}
