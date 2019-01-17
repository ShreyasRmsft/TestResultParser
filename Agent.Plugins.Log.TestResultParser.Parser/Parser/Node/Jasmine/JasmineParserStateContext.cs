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
        /// Current stack trace index. Used to insert the stack trace to the appropriate failed test case later in the stack traces state.
        /// </summary>
        public int CurrentStackTraceIndex { get; set; }

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
        /// Number of suite errors
        /// </summary>
        public int SuiteErrors { get; set; }

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
            PendingStarterMatched = false;
            FailureStarterMatched = false;
            LinesWithinWhichMatchIsExpected = -1;
            CurrentStackTraceIndex = -1;
            NextExpectedMatch = null;
            IsTimeParsed = false;
        }
    }
}
