// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.Log.TestResultParser.Parser
{
    using Agent.Plugins.Log.TestResultParser.Contracts;

    /// <summary>
    /// Base class for all state context objects
    /// </summary>
    public abstract class AbstractParserStateContext : IParserStateContext
    {
        protected AbstractParserStateContext(TestRun testRun)
        {
            Initialize(testRun);
        }

        public void Initialize(TestRun testRun)
        {
            TestRun = testRun;
            CurrentLineNumber = 0;
            NextExpectedMatch = null;
            LinesWithinWhichMatchIsExpected = -1;
        }

        /// <summary>
        /// Test run associted with the current iteration of the parser
        /// </summary>
        public TestRun TestRun { get; set; }

        /// <summary>
        /// The current line number of the input console log line
        /// </summary>
        public int CurrentLineNumber { get; set; }

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
    }
}
