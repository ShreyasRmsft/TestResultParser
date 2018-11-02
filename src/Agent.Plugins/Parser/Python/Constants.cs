namespace Agent.Plugins.TestResultParser.Parser.Python
{
    using System.Text.RegularExpressions;

    public class Constants
    {
        public static readonly Regex ResultPattern = new Regex(@"^(.+)\s\.\.\.\s(.*)$");
        public static readonly Regex FailedResultPattern = new Regex(@"^FAIL:\s(.+)$");
        public static readonly Regex PassedOutcomePattern = new Regex(@"ok(\s(\([0-9]+(\.[0-9]+)?)s\))?$");
        public static readonly Regex ExpectedFailureOutcomePattern = new Regex(@"expected failure(\s(\([0-9]+(\.[0-9]+)?)s\))?$");
        public static readonly Regex SkippedOutcomePattern = new Regex(@"^skipped|SKIP:");
        public const int ResultNameGroupIndex = 1;
        public const int ResultOutcomeGroupIndex = 2;
        public const int ResultPatternExpectedGroups = 3;
        public const int FailedResultPatternExpectedGroups = 2;
        public const int PassedOutcomePatternsExpectedGroups = 2;
        public const int SkippedOutcomePatternsExpectedGroups = 1;
        public const int TestTimeGroupIndex = 1;
    }
}
