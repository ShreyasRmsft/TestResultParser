// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.Log.TestResultParser.Parser
{
    using System.Collections.Generic;

    public delegate void ParserResetAndAttemptPublish();

    public interface ITestResultParserState
    {
        /// <summary>
        /// Collection of regexes and the corresponding post match action on a successful match
        /// </summary>
        IEnumerable<RegexActionPair> RegexesToMatch { get; }

        /// <summary>
        /// Default action when no pattern in the state matches the given line
        /// </summary>
        /// <param name="line">Log line</param>
        /// <param name="stateContext">State context object containing information regarding the parser's state</param>
        /// <returns>True if the parser was reset</returns>
        bool PeformNoPatternMatchedAction(string line, AbstractParserStateContext stateContext);
    }
}
