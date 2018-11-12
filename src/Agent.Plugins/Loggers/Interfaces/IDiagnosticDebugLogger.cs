namespace Agent.Plugins.TestResultParser.Loggers.Interfaces
{
    public interface IDiagnosticDebugLogger
    {
        /// <summary>
        /// Verbose debug diagnostics.
        /// </summary>
        /// <param name="text">Diagnostics debug log</param>
        void Debug(string text);
    }
}
