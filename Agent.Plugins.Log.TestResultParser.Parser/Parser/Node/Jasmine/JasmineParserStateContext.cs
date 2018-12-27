// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.Log.TestResultParser.Parser
{
    using Agent.Plugins.Log.TestResultParser.Contracts;

    public class JasmineParserStateContext : AbstractParserStateContext
    {
        public JasmineParserStateContext(TestRun testRun) : base(testRun)
        {
            Initialize(testRun);
        }

        /// <summary>
        /// Test case number of the last failed test case encountered as part of the current run
        /// </summary>
        public int LastFailedTestCaseNumber { get; set; }

        /// <summary>
        /// Test case number of the last pending test case encountered as part of the current run
        /// </summary>
        public int LastPendingTestCaseNumber { get; set; }

        /// <summary>
        /// Bool value if pending starter regex has been matched
        /// </summary>
        public bool PendingStarterMatched { get; set; }

        /// <summary>
        /// Bool value if failures starter regex has been matched
        /// </summary>
        public bool FailureStarterMatched { get; set; }

        /// <summary>
        /// Passed tests to expect from the test status
        /// </summary>
        public int PassedTestsToExpect { get; set; }

        /// <summary>
        /// Failed tests to expect from the test status
        /// </summary>
        public int FailedTestsToExpect { get; set; }

        /// <summary>
        /// Skipped tests to expect from the test status
        /// </summary>
        public int SkippedTestsToExpect { get; set; }

        /// <summary>
        /// Number of suite errors
        /// </summary>
        public int SuiteErrors { get; set; }

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
        /// Bool variable to keep check if time has been parsed
        /// </summary>
        public bool IsTimeParsed { get; set; }

        /// <summary>
        /// Initializes all the values to their defaults
        /// </summary>
        public new void Initialize(TestRun testRun)
        {
            base.Initialize(testRun);
            LastFailedTestCaseNumber = 0;
            LastPendingTestCaseNumber = 0;
            PassedTestsToExpect = 0;
            FailedTestsToExpect = 0;
            SkippedTestsToExpect = 0;
            PendingStarterMatched = false;
            FailureStarterMatched = false;
            LinesWithinWhichMatchIsExpected = 0;
            NextExpectedMatch = null;
            IsTimeParsed = false;
        }
    }
}
