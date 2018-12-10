// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.TestResultParser.Parser.Node.Mocha
{
    using Agent.Plugins.TestResultParser.TestResult.Models;

    public class MochaParserStateContext : TestResultParserStateContext
    {
        public MochaParserStateContext(TestRun testRun) : base(testRun)
        {
            Initialize(testRun);
        }

        /// <summary>
        /// This indicates the number of stack traces (they look exactly the same as a failed test case in mocha)
        /// to be skipped post summary
        /// </summary>
        public int StackTracesToSkipParsingPostSummary { get; set; }

        /// <summary>
        /// Test case number of the last failed test case encountered as part of the current run
        /// </summary>
        public int LastFailedTestCaseNumber { get; set; }

        /// <summary>
        /// This is used to enforce that a match is expected within specified number of lines
        /// The parser may take action accordingly
        /// </summary>
        public int LinesWithinWhichMatchIsExpected { get; set; }

        /// <summary>
        /// Hint string for logging and telemetry to specify what match was expected in case it does not occur
        /// in the expected number of lines
        /// </summary>
        public string NextExpectedMatch { get; set; }

        /// <summary>
        /// Initializes all the values to their defaults
        /// </summary>
        public override void Initialize(TestRun testRun)
        {
            base.Initialize(testRun);
            StackTracesToSkipParsingPostSummary = 0;
            LastFailedTestCaseNumber = 0;
            LinesWithinWhichMatchIsExpected = -1;
            NextExpectedMatch = null;
        }
    }
}
