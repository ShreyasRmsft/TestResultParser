// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.TestResultParser.Parser.Node.Mocha
{
    public class TelemetryConstants
    {
        public const string EventArea = "MochaTestResultParser";

        public const string Initialize = "Initialize";

        public const string AttemptPublishAndResetParser = "AttemptPublishAndResetParser";

        public const string ParserResetSuccessful = "ParserResetSuccessful";

        public const string ExpectingStackTracesButFoundPassedTest = "ExpectingStackTracesButFoundPassedTest";

        public const string ExpectingStackTracesButFoundPendingTest = "ExpectingStackTracesButFoundPendingTest";

        public const string UnexpectedFailedTestCaseNumber = "UnexpectedFailedTestCaseNumber";

        public const string UnexpectedFailedStackTraceNumber = "UnexpectedFailedStackTraceNumber";

        public const string FailedTestCasesFoundButNoFailedSummary = "FailedTestCasesFoundButNoFailedSummary";

        public const string PendingTestCasesFoundButNoFailedSummary = "PendingTestCasesFoundButNoFailedSummary";

        public const string PassedTestCasesFoundButNoPassedSummary = "PassedTestCasesFoundButNoPassedSummary";

        public const string SummaryWithNoTestCases = "SummaryWithNoTestCases";

        public const string PassedSummaryMismatch = "PassedSummaryMismatch";

        public const string FailedSummaryMismatch = "FailedSummaryMismatch";

        public const string PendingSummaryMismatch = "PendingSummaryMismatch";

        public const string Event = "";
    }
}
