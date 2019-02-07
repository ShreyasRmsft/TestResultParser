// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.Log.TestResultParser.Parser
{
    using Agent.Plugins.Log.TestResultParser.Contracts;

    public class MochaParserStateContext : AbstractParserStateContext
    {
        public MochaParserStateContext(TestRun testRun) : base(testRun)
        {
            Initialize(testRun);
        }

        /// <summary>
        /// This indicates the number of stack traces (they look exactly the same as a failed test case in mocha)
        /// to be skipped post summary
        /// </summary>
        public int StackTracesToExpectPostSummary { get; set; }

        /// <summary>
        /// Test case number of the last failed test case encountered as part of the current run
        /// </summary>
        public int LastFailedTestCaseNumber { get; set; }

        /// <summary>
        /// Current stack trace index. Used to insert the stack trace to the appropriate failed test case later in the stack traces state.
        /// </summary>
        public int CurrentStackTraceIndex => LastFailedTestCaseNumber - 1;

        /// <summary>
        /// Initializes all the values to their defaults
        /// </summary>
        public new void Initialize(TestRun testRun)
        {
            base.Initialize(testRun);
            StackTracesToExpectPostSummary = 0;
            LastFailedTestCaseNumber = 0;
        }
    }
}
