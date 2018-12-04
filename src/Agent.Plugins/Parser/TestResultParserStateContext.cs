﻿using Agent.Plugins.TestResultParser.TestResult.Models;

namespace Agent.Plugins.TestResultParser.Parser
{
    /// <summary>
    /// Base class for all state context objects
    /// </summary>
    public abstract class TestResultParserStateContext
    {
        public abstract void Initialize(TestRun testRun);

        /// <summary>
        /// Test run associted with the current iteration of the parser
        /// </summary>
        public TestRun TestRun { get; set; }

        /// <summary>
        /// The current line number of the input console log line
        /// </summary>
        public int CurrentLineNumber { get; set; }

        // TODO: Extract out common properties here when enough parsers have been authored
    }
}
