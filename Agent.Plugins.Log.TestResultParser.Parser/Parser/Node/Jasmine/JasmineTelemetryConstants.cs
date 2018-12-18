// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.Log.TestResultParser.Parser
{
    public class JasmineTelemetryConstants
    {
        public const string EventArea = "JasmineTestResultParser";

        public const string Initialize = "Initialize";

        public const string FailedSummaryMismatch = "FailedSummaryMismatch";

        public const string UnexpectedFailedTestCaseNumber = "UnexpectedFailedTestCaseNumber";

        public const string UnexpectedPendingTestCaseNumber = "UnexpectedPendingTestCaseNumber";

        public const string FailedTestCasesFoundButNoFailedSummary = "FailedTestCasesFoundButNoFailedSummary";

        public const string PendingTestCasesFoundButNoFailedSummary = "PendingTestCasesFoundButNoFailedSummary";

        public const string PendingSummaryMismatch = "PendingSummaryMismatch";

        public const string PassedTestCasesFoundButNoPassedSummary = "PassedTestCasesFoundButNoPassedSummary";

        public const string FailedTestCasesSummaryMismatchWithExpectedFailed = "FailedTestCasesSummaryMismatchWithExpectedFailed";

        public const string SkippedTestCasesSummaryMismatchWithExpectedSkipped = "SkippedTestCasesSummaryMismatchWithExpectedSkipped";

        public const string TotalTestsZero = "TotalTestsZero";

        public const string TotalTestRunTimeZero = "TotalTestRunTimeZero";
    }
}
