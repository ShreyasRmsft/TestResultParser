// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.UnitTests.MochaTestResultParserTests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using Agent.Plugins.TestResultParser.Parser.Node.Mocha;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class MochaTestResultParserRegexTests
    {
        [DataTestMethod]
        [DynamicData(nameof(GetRegexPatters), DynamicDataSourceType.Method)]
        public void RegexPatternTest(string regexPattern)
        {
            var postiveTestCases = File.ReadAllLines(Path.Combine("MochaTestResultParserTests", "Resources", "RegexTests", "PositiveMatches", $"{regexPattern}.txt"));
            var regex = typeof(MochaTestResultParserRegexes).GetProperty(regexPattern).GetValue(null);
            foreach (var testCase in postiveTestCases)
            {
                Assert.IsTrue(((Regex)regex).Match(testCase).Success, $"Should have matched:{testCase}");
            }

            var negativeTestCases = File.ReadAllLines(Path.Combine("MochaTestResultParserTests", "Resources", "RegexTests", "NegativeMatches", $"{regexPattern}.txt"));

            foreach (var testCase in negativeTestCases)
            {
                Assert.IsFalse(((Regex)regex).Match(testCase).Success, $"Should not have matched:{testCase}");
            }
        }

        public static IEnumerable<object[]> GetRegexPatters()
        {
            foreach (var property in typeof(MochaTestResultParserRegexes).GetProperties(BindingFlags.Public | BindingFlags.Static))
            {
                //if(property.Name.Contains("FailedTestsSummary"))
                yield return new object[] { property.Name };
            }
        }
    }
}
