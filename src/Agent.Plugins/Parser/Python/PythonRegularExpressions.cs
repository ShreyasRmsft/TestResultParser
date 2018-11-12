namespace Agent.Plugins.TestResultParser.Parser.Python
{
    using System.Text.RegularExpressions;

    public class PythonRegularExpressions
    {
        public static readonly Regex ResultPattern = new Regex(@"^(.+)\s\.\.\.\s(.*)$");

        //public static readonly Regex ErrorResultPattern = new 
        public static readonly Regex FailedResultPattern = new Regex(@"^FAIL:\s(.+)$");
        public static readonly Regex ErrorResultPattern = new Regex(@"^ERROR:\s(.+)$");

        public static readonly Regex PassedOutcomePattern = new Regex(@"ok(\s(\([0-9]+(\.[0-9]+)?)s\))?$");
        public static readonly Regex FailedOutcomePattern = new Regex(@"Fail(\s(\([0-9]+(\.[0-9]+)?)s\))?$");


        public static readonly Regex SummaryTestCountAndTimePattern = new Regex(@"^Ran\s([0-9]+)\stests?\sin\s([0-9]+)(\.([0-9]+))?s");
        public static readonly Regex SummaryOutcomePattern = new Regex(@"OK|FAILED\s\((.*)\)");
        public static readonly Regex SummaryFailurePattern = new Regex($"failures=(?<{RegexCaptureGroups.FailedTests}>[1-9][0-9]*)");

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
    }
}
