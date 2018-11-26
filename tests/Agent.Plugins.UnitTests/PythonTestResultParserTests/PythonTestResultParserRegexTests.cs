// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.UnitTests.PythonTestResultParserTests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using Agent.Plugins.TestResultParser.Parser.Python;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PythonTestResultParserRegexTests
    {
        [DataTestMethod]
        [DynamicData(nameof(GetRegexPatterns), DynamicDataSourceType.Method)]
        public void RegexPatternTest(string regexPattern)
        {
            var postiveTestCases = File.ReadAllLines(Path.Combine("PythonTestResultParserTests", "Resources", "RegexTests", "PositiveMatches", $"{regexPattern}.txt"));
            var regex = typeof(PythonRegularExpressions).GetProperty(regexPattern, BindingFlags.Public | BindingFlags.Static).GetValue(0);

            for (int i = 0; i < postiveTestCases.Length; i++)
            {
                Assert.IsTrue(((Regex)regex).Match(postiveTestCases[i]).Success, $"{regexPattern} should have matched:{postiveTestCases[i]} on line {i+1}");
            }

            var negativeTestCases = File.ReadAllLines(Path.Combine("PythonTestResultParserTests", "Resources", "RegexTests", "NegativeMatches", $"{regexPattern}.txt"));

            for (int i = 0; i < negativeTestCases.Length; i++)
            {
                Assert.IsFalse(((Regex)regex).Match(negativeTestCases[i]).Success, $"{regexPattern} should NOT have matched:{negativeTestCases[i]} on line {i+1}");
            }
        }

        public static IEnumerable<object[]> GetRegexPatterns()
        {
            foreach (var property in typeof(PythonRegularExpressions).GetProperties(BindingFlags.Public | BindingFlags.Static))
            {
                //if(property.Name.Contains("FailedTestsSummary"))
                yield return new object[] { property.Name };
            }
        }
    }
}
