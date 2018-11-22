// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Agent.Plugins.TestResultParser.TestResult.Models;

namespace Agent.Plugins.TestResultParser.Parser.Node.Mocha
{
    public class MochaTestResultParserStateContext
    {
        public MochaTestResultParserStateContext(TestRun testRun)
        {
            TestRun = testRun;
            StackTracesToSkipParsingPostSummary = 0;
            LastFailedTestCaseNumber = 0;
            LinesWithinWhichMatchIsExpected = -1;
        }

        public TestRun TestRun { get; }

        public int StackTracesToSkipParsingPostSummary { get; set; }

        public int LastFailedTestCaseNumber { get; set; }

        public int LinesWithinWhichMatchIsExpected { get; set; }

        public string ExpectedMatch { get; set; }

        public int CurrentLineNumber { get; set; }
    }
}
