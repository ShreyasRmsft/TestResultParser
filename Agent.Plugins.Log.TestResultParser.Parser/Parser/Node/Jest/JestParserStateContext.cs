// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Agent.Plugins.Log.TestResultParser.Contracts;

namespace Agent.Plugins.Log.TestResultParser.Parser
{
    public class JestParserStateContext : TestResultParserStateContext
    {
        public JestParserStateContext(TestRun testRun) : base(testRun)
        {
            Initialize(testRun);
        }

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
        /// Used to identify if a run had the --verbose option enabled
        /// </summary>
        public bool VerboseOptionEnabled { get; set; }

        /// <summary>
        /// This is used to identify that the failed tests summary indicator has been encountered
        /// All the failed test cases are reported after this again hence we use this to ignore them
        /// </summary>
        public bool FailedTestsSummaryIndicatorEncountered { get; set; }

        /// <summary>
        /// Initializes all the values to their defaults
        /// </summary>
        public new void Initialize(TestRun testRun)
        {
            base.Initialize(testRun);
            LinesWithinWhichMatchIsExpected = 0;
            NextExpectedMatch = null;
            VerboseOptionEnabled = false;
            FailedTestsSummaryIndicatorEncountered = false;
        }
    }
}
