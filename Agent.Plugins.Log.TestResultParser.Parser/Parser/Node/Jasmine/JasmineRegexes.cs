// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.Log.TestResultParser.Parser
{
    using System.Text.RegularExpressions;

    public class JasmineRegexes
    {
        /// <summary>
        /// Matches lines with the following regex:
        /// ^Started$
        /// </summary>
        public static Regex TestRunStart { get; } = new Regex($"^Started$");

        /// <summary>
        /// Matches lines with the following regex:
        /// ^[.F*]+$
        /// </summary>
        public static Regex TestStatus { get; } = new Regex($"^[.F*]+$");

        /// <summary>
        /// Matches lines with the following regex:
        /// ^Failures:$
        /// </summary>
        public static Regex FailuresStart { get; } = new Regex($"^Failures:$");

        /// <summary>
        /// Matches lines with the following regex:
        /// ^Pending:$
        /// </summary>
        public static Regex PendingStart { get; } = new Regex($"^Pending:$");

        /// <summary>
        /// Matches lines with the following regex:
        /// ^([1-9][0-9]*)\\) (.+$)
        /// </summary>
        public static Regex FailedOrPendingTestCase { get; } = new Regex($"^(?<{RegexCaptureGroups.FailedTestCaseNumber}>[1-9][0-9]*)\\) (?<{RegexCaptureGroups.TestCaseName}>.+$)", RegexOptions.ExplicitCapture);

        /// <summary>
        /// Matches lines with the following regex:
        /// ^([1-9][0-9]*) specs, ([0-9]+) failures?(, ([0-9]+) pending specs?)?
        /// </summary>
        public static Regex TestsSummaryMatcher { get; } = new Regex($"^(?<{RegexCaptureGroups.TotalTests}>[1-9][0-9]*) specs?, (?<{RegexCaptureGroups.FailedTests}>[0-9]+) failures?(, (?<{RegexCaptureGroups.SkippedTests}>[0-9]+) pending specs?)?", RegexOptions.ExplicitCapture);

        /// <summary>
        /// Matches lines with the following regex:
        /// ^Finished in (([0-9]+(.[0-9]+)*)*) seconds$
        /// </summary>
        public static Regex TestRunTimeMatcher { get; } = new Regex($"^Finished in (?<{RegexCaptureGroups.TestRunTime}>([0-9]+(.[0-9]+)*)*) seconds$");

        /// <summary>
        /// Matches lines with the following regex:
        /// ^Suite error: (.*)
        /// </summary>
        public static Regex SuiteError { get; } = new Regex($"^Suite error: (.*)");
    }
}
