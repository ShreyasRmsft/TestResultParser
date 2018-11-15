namespace Agent.Plugins.TestResultParser.Parser.Python
{
    using System.Text.RegularExpressions;

    public class PythonRegularExpressions
    {
        public static readonly Regex ResultPattern = new Regex(@"^(.+) \.\.\. (.*)$");

        //public static readonly Regex ErrorResultPattern = new 
        public static readonly Regex FailedResultPattern = new Regex(@"^(FAIL|ERROR): (.+)$");

        public static readonly Regex PassedOutcomePattern = new Regex(@"(^ok)|( ok)$");

        public static readonly Regex SummaryTestCountAndTimePattern = new Regex(@"^Ran ([0-9]+) tests? in ([0-9]+)(\.([0-9]+))?s");
        public static readonly Regex SummaryAllowedWhiteSpaceLine = new Regex(@"^((\s|\t)+)?$");
        public static readonly Regex SummaryOutcomePattern = new Regex(@"(OK|FAILED)( \((.*)\))?");
        public static readonly Regex SummaryFailurePattern = new Regex($"(^|, )failures=(?<{RegexCaptureGroups.FailedTests}>[1-9][0-9]*)");
        public static readonly Regex SummaryErrorsPattern = new Regex($"(^|, )errors=(?<{RegexCaptureGroups.NumberOfErrors}>[1-9][0-9]*)");

        //public static readonly Regex ExpectedFailureOutcomePattern = new Regex(@"expected failure(\s(\([0-9]+(\.[0-9]+)?)s\))?$");
        //public static readonly Regex SkippedOutcomePattern = new Regex(@"^skipped|SKIP:");

        //public const int ResultNameGroupIndex = 1;
        //public const int ResultOutcomeGroupIndex = 2;
        //public const int ResultPatternExpectedGroups = 3;

        //public const int FailedResultPatternExpectedGroups = 2;
        //public const int PassedOutcomePatternsExpectedGroups = 2;
        //public const int SkippedOutcomePatternsExpectedGroups = 1;
        //public const int TestTimeGroupIndex = 1;
    }

    public class RegexCaptureGroups
    {
        public static string FailedTests { get; } = "FailedTests";
        public static string NumberOfErrors { get; } = "NumberOfErrors";
    }
}
