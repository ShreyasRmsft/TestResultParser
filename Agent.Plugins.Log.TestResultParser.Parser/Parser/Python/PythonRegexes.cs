// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.Log.TestResultParser.Parser
{
    using System;
    using System.Text.RegularExpressions;

    public class PythonRegexes
    {
        /// <summary>
        /// Default timeout for all the regexes
        /// </summary>
        private static readonly TimeSpan RegexDefaultTimeout = TimeSpan.FromMilliseconds(100);

        // Pattern : ^(.+) ... (.*)$
        // Example : test1 (testProject) ... ok
        public static Regex TestResult { get; } = new Regex($"^(?<{RegexCaptureGroups.TestCaseName}>.+) \\.\\.\\. (?<{RegexCaptureGroups.TestOutcome}>.*)$",
            RegexOptions.ExplicitCapture, RegexDefaultTimeout);

        // TODO: Have separate pattern for error if required
        // Pattern : ^(FAIL|ERROR)( )?:(.+)$
        // Example : FAIL: failingTest1
        public static Regex FailedResult { get; } = new Regex($"^(FAIL|ERROR) ?: ?(?<{RegexCaptureGroups.TestCaseName}>(.+))$",
            RegexOptions.ExplicitCapture, RegexDefaultTimeout);

        // TODO: Treat expected failures different?
        // Example : Hello ok
        public static Regex PassedOutcome { get; } = new Regex(@"(^(ok|expected failure)|( (ok|expected failure)))$",
            RegexOptions.ExplicitCapture, RegexDefaultTimeout);

        // Example : skipped 'Reason'
        public static Regex SkippedOutcome { get; } = new Regex(@"^skipped", RegexOptions.ExplicitCapture, RegexDefaultTimeout);

        // Pattern : ^Ran ([0-9]+) tests? in ([0-9]+)(\.([0-9]+))?s
        // Example : Ran 12 tests in 2.2s
        public static Regex TestCountAndTimeSummary { get; } = new Regex($"^Ran (?<{RegexCaptureGroups.TotalTests}>[0-9]+) tests? in (?<{RegexCaptureGroups.TestRunTime}>[0-9]+)(\\.(?<{RegexCaptureGroups.TestRunTimeMs}>[0-9]+))?s",
            RegexOptions.ExplicitCapture, RegexDefaultTimeout);

        /// <summary>
        /// Indicates the beginning of a stack trace, or the end of the last stack trace (just before summary)
        /// </summary>
        public static Regex StackTraceBorder { get; } = new Regex("^----------------------------------------------------------------------(-)*$",
            RegexOptions.ExplicitCapture, RegexDefaultTimeout);

        /// <summary>
        /// Indicates the end of a stack trace (also starting of the heading of the next one)
        /// </summary>
        public static Regex StackTraceEnd { get; } = new Regex("^======================================================================(=)*$",
            RegexOptions.ExplicitCapture, RegexDefaultTimeout);

        // Example : Failed (failures=2)
        public static Regex TestOutcomeSummary { get; } = new Regex($"^(OK|FAILED) ?(\\((?<{RegexCaptureGroups.TestOutcome}>.*)\\))?$",
            RegexOptions.ExplicitCapture, RegexDefaultTimeout);

        public static Regex SummaryFailure { get; } = new Regex($"(^|, ?)failures ?= ?(?<{RegexCaptureGroups.FailedTests}>[1-9][0-9]*)",
            RegexOptions.ExplicitCapture, RegexDefaultTimeout);

        public static Regex SummaryErrors { get; } = new Regex($"(^|, ?)errors ?= ?(?<{RegexCaptureGroups.Errors}>[1-9][0-9]*)",
            RegexOptions.ExplicitCapture, RegexDefaultTimeout);

        public static Regex SummarySkipped { get; } = new Regex($"(^|, ?)skipped ?= ?(?<{RegexCaptureGroups.SkippedTests}>[1-9][0-9]*)",
            RegexOptions.ExplicitCapture, RegexDefaultTimeout);
    }
}
