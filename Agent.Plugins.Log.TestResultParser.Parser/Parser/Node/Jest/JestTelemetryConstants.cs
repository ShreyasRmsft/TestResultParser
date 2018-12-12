// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.Log.TestResultParser.Parser
{
    // TODO: mark the telemetry events that are considered as failure points/events

    public class JestTelemetryConstants
    {
        public const string EventArea = "JestTestResultParser";

        public const string Initialize = "Initialize";

        public const string TotalTestsZero = "TotalTestsZero";

        public const string TotalTestRunTimeZero = "TotalTestRunTimeZero";

        public const string UnexpectedTestRunStart = "UnexpectedTestRunStart";

        public const string TestCasesFoundButNoSummary = "TestCasesFoundButNoSummary";

        public const string PassedSummaryMismatch = "PassedSummaryMismatch";

        public const string FailedSummaryMismatch = "FailedSummaryMismatch";

        public const string SkippedSummaryMismatch = "SkippedSummaryMismatch";

        public const string PassedTestCasesFoundButNoPassedSummary = "PassedTestCasesFoundButNoPassedSummary";

        public const string FailedTestCasesFoundButNoFailedSummary = "FailedTestCasesFoundButNoFailedSummary";

        public const string SkippedTestCasesFoundButNoSkippedSummary = "SkippedTestCasesFoundButNoSkippedSummary";
    }
}
