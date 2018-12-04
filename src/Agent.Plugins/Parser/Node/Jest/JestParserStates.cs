// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.TestResultParser.Parser.Node.Jest
{
    public enum JestParserStates
    {
        ExpectingTestRunStart,
        ExpectingTestResults,
        ExpectingStackTraces,
        ExpectingTestRunSummary
    }
}
