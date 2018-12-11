﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using Agent.Plugins.Log.TestResultParser.Parser;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Agent.Plugins.UnitTests.MochaTestResultParserTests
{
    [TestClass]
    public class MochaTestResultParserTests : TestResultParserTestBase
    {
        [TestInitialize]
        public void TestInit()
        {
            this.parser = new MochaTestResultParser(this.testRunManagerMock.Object, this.diagnosticDataCollector.Object, this.telemetryDataCollector.Object);
        }

        #region DataDrivenTests

        [DataTestMethod]
        [DynamicData(nameof(GetSuccessScenariosTestCases), DynamicDataSourceType.Method)]
        public void SuccessScenariosWithBasicAssertions(string testCase)
        {
            TestSuccessScenariosWithBasicAssertions(testCase);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetPartialSuccessTestCases), DynamicDataSourceType.Method)]
        public void PartialSuccessScenariosWithBasicAssertions(string testCase)
        {
            TestPartialSuccessScenariosWithBasicAssertions(testCase);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetDetailedTestsTestCases), DynamicDataSourceType.Method)]
        public void DetailedAssertions(string testCase)
        {
            TestWithDetailedAssertions(testCase);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetNegativeTestsTestCases), DynamicDataSourceType.Method)]
        public void NegativeTests(string testCase)
        {
            TestNegativeTestsScenarios(testCase);
        }

        #endregion

        #region Data Drivers

        public static IEnumerable<object[]> GetSuccessScenariosTestCases()
        {
            return GetTestCasesFromRelativePath(Path.Combine("MochaTestResultParserTests", "Resources", "SuccessScenarios"));
        }

        public static IEnumerable<object[]> GetPartialSuccessTestCases()
        {
            return GetTestCasesFromRelativePath(Path.Combine("MochaTestResultParserTests", "Resources", "PartialSuccess"));
        }

        public static IEnumerable<object[]> GetDetailedTestsTestCases()
        {
            return GetTestCasesFromRelativePath(Path.Combine("MochaTestResultParserTests", "Resources", "DetailedTests"));
        }

        public static IEnumerable<object[]> GetNegativeTestsTestCases()
        {
            return GetTestCasesFromRelativePath(Path.Combine("MochaTestResultParserTests", "Resources", "NegativeTests"));
        }

        #endregion
    }
}
