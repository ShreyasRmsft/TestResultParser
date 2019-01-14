// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Agent.Plugins.Log.TestResultParser.Contracts;

namespace Agent.Plugins.Log.TestResultParser.Parser
{
    /// <summary>
    /// Base class for a jasmine test result parser state
    /// Has common methods that each state will need to use
    /// </summary>
    public class JasmineParserStateBase : NodeParserStateBase
    {
        /// <summary>
        /// Constructor for a jasmine parser state
        /// </summary>
        /// <param name="parserResetAndAttempPublish">Delegate sent by the parser to reset the parser and attempt publication of test results</param>
        /// <param name="logger"></param>
        /// <param name="telemetryDataCollector"></param>
        protected JasmineParserStateBase(ParserResetAndAttemptPublish parserResetAndAttempPublish, ITraceLogger logger, ITelemetryDataCollector telemetryDataCollector, string parserName)
            : base(parserResetAndAttempPublish, logger, telemetryDataCollector, parserName)
        {

        }
    }
}
