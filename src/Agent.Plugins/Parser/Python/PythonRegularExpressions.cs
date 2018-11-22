namespace Agent.Plugins.TestResultParser.Parser.Python
{
    using System.Text.RegularExpressions;

    public class PythonRegularExpressions
    {
        public static readonly Regex ResultPattern = new Regex($"^(?<{RegexCaptureGroups.TestCaseName}>.+) \\.\\.\\. (?<{RegexCaptureGroups.TestOutcome}>.*)$", RegexOptions.ExplicitCapture);

        // TODO: Have separate pattern for error if required
        public static readonly Regex FailedResultPattern = new Regex($"^(FAIL|ERROR)( )?:(?<{RegexCaptureGroups.TestCaseName}>(.+))$", RegexOptions.ExplicitCapture);

        public static readonly Regex PassedOutcomePattern = new Regex(@"(^ok|expected failure)|( (ok|expected failure))$");
        public static readonly Regex SkippedOutcomePattern = new Regex(@"^skipped");

        public static readonly Regex SummaryTestCountAndTimePattern = new Regex(@"^Ran ([0-9]+) tests? in ([0-9]+)(\.([0-9]+))?s");
        public static readonly Regex SummaryAllowedWhiteSpaceLine = new Regex(@"^((\s|\t)+)?$");
        public static readonly Regex SummaryOutcomePattern = new Regex(@"(OK|FAILED)( \((.*)\))?");
        public static readonly Regex SummaryFailurePattern = new Regex($"(^|, )failures( )?=(?<{RegexCaptureGroups.FailedTests}>[1-9][0-9]*)", RegexOptions.ExplicitCapture);
        public static readonly Regex SummaryErrorsPattern = new Regex($"(^|, )errors( )?=(?<{RegexCaptureGroups.Errors}>[1-9][0-9]*)", RegexOptions.ExplicitCapture);
        public static readonly Regex SummarySkippedPattern = new Regex($"(^|, )skipped( )?=(?<{RegexCaptureGroups.SkippedTests}>[1-9][0-9]*)", RegexOptions.ExplicitCapture);
    }
}
