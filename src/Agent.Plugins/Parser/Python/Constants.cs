namespace Agent.Plugins.TestResultParser.Parser.Python
{
    using System.Text.RegularExpressions;

    public class Constants
    {
        public static readonly Regex ResultPattern = new Regex(@"^(.+)\s\.\.\.\s(.*)$");
        public static readonly Regex PassedResultPattern = new Regex(@"ok(\s(\([0-9]+(\.[0-9]+)?)s\))?$");
        public static readonly Regex FailedResultPattern = new Regex(@"FAIL(\s(\([0-9]+(\.[0-9]+)?)s\))?$");
        public static readonly Regex ExpectedFailureResultPattern = new Regex(@"expected failure(\s(\([0-9]+(\.[0-9]+)?)s\))?$");
        public static readonly Regex SkippedResultPattern = new Regex(@"^skipped|SKIP:"); // TODO: check if skip: (0.02s) is the pattern or skip (0.02s): is.
        public const int PartialStartGroupIndex = 1;
        public const int PartialEndGroupIndex = 2;
        public const int ResultPatternExpectedGroups = 3;
        public const int OutcomePatternsExpectedGroups = 2;
        public const int TestTimeGroupIndex = 1;
    }
}
