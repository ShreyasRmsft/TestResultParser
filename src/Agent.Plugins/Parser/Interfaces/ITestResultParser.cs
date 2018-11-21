using Agent.Plugins.TestResultParser.Parser.Models;

namespace Agent.Plugins.TestResultParser.Parser.Interfaces
{
    public interface ITestResultParser
    {
        /// <summary>
        /// Parse task output line by line to detect the test result
        /// </summary>
        /// <param name="line">Data to be parsed.</param>
        void Parse(LogData line);

        /// <summary>
        /// Name of the parser
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Version number of the parser
        /// </summary>
        string Version { get; }
    }
}
