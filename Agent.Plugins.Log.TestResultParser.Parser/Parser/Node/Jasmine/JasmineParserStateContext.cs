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
        }

    }
}
