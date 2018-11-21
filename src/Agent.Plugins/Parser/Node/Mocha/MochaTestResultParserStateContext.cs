// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.TestResultParser.Parser.Node.Mocha
{
    public class MochaTestResultParserStateContext
    {
        public int StackTracesToSkipParsingPostSummary { get; set; } = 0;

        public int LastFailedTestCaseNumber { get; set; } = 0;

        public int LinesWithinWhichMatchIsExpected { get; set; } = -1;

        public string ExpectedMatch { get; set; }
    }
}
