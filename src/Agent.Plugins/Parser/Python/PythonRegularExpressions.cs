namespace Agent.Plugins.TestResultParser.Parser.Python
{
    using System.Text.RegularExpressions;

    public class PythonRegularExpressions
    {
        // Pattern : ^(.+) \.\.\. (.*)$
        // Example : test1 (testProject) ... ok
        public static Regex TestResult { get; } = new Regex($"^(?<{RegexCaptureGroups.TestCaseName}>.+) \\.\\.\\. (?<{RegexCaptureGroups.TestOutcome}>.*)$", RegexOptions.ExplicitCapture);

        // TODO: Have separate pattern for error if required
        // Pattern : ^(FAIL|ERROR)( )?:(.+)$
        // Example : FAIL: failingTest1
        public static Regex FailedResult { get; } = new Regex($"^(FAIL|ERROR) ?: ?(?<{RegexCaptureGroups.TestCaseName}>(.+))$", RegexOptions.ExplicitCapture);

        // TODO: Treat expected failures different?
        // Example : Hello ok
        public static Regex PassedOutcome { get; } = new Regex(@"(^(ok|expected failure)|( (ok|expected failure)))$");

        // Example : skipped 'Reason'
        public static Regex SkippedOutcome { get; } = new Regex(@"^skipped");

        // Pattern : ^Ran ([0-9]+) tests? in ([0-9]+)(\.([0-9]+))?s
        // Example : Ran 12 tests in 2.2s
        public static Regex TestCountAndTimeSummary { get; } = new Regex($"^Ran (?<{RegexCaptureGroups.TotalTests}>[0-9]+) tests? in (?<{RegexCaptureGroups.TestRunTime}>[0-9]+)(\\.(?<{RegexCaptureGroups.TestRunTimeMs}>[0-9]+))?s", RegexOptions.ExplicitCapture);

        // Example : Failed (failures=2)
        public static Regex TestOutcomeSummary { get; } = new Regex($"^(OK|FAILED) ?(\\((?<{RegexCaptureGroups.TestOutcome}>.*)\\))?$");
        public static Regex SummaryFailure { get; } = new Regex($"(^|, ?)failures ?= ?(?<{RegexCaptureGroups.FailedTests}>[1-9][0-9]*)", RegexOptions.ExplicitCapture);
        public static Regex SummaryErrors { get; } = new Regex($"(^|, ?)errors ?= ?(?<{RegexCaptureGroups.Errors}>[1-9][0-9]*)", RegexOptions.ExplicitCapture);
        public static Regex SummarySkipped { get; } = new Regex($"(^|, ?)skipped ?= ?(?<{RegexCaptureGroups.SkippedTests}>[1-9][0-9]*)", RegexOptions.ExplicitCapture);
    }
}
