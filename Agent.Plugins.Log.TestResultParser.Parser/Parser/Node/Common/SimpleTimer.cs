// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.Log.TestResultParser.Parser
{
    using System;
    using System.Diagnostics;
    using Agent.Plugins.Log.TestResultParser.Contracts;

    /// <summary>
    /// This is a utitily class used for recording timing
    /// information. Its usage is 
    /// using (SimpleTimer timer = new SimpleTimer("MyOperation"))
    /// {
    ///     MyOperation...
    /// }
    /// </summary>
    public class SimpleTimer : IDisposable
    {
        /// <summary>
        /// Creates a timer with threshold. A perf message is logged only if
        /// the time elapsed is more than the threshold.
        /// </summary>
        public SimpleTimer(string timerName, string telemetryArea, string telemetryEventName, ITraceLogger logger, 
            ITelemetryDataCollector telemetryDataCollector, TimeSpan threshold, bool publishTelemetry = true)
        {
            _name = timerName;
            _telemetryEventName = telemetryEventName;
            _telemetryArea = telemetryArea;
            _logger = logger;
            _telemetry = telemetryDataCollector;
            _threshold = threshold;
            _timer = Stopwatch.StartNew();
            _publishTelemetry = publishTelemetry;
        }

        /// <summary>
        /// Implement IDisposable
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Stop the watch and log the trace message with the elapsed time.
        /// Additionaly also adds the elapsed time to telemetry under the timer name and if
        /// the timer is called multiple times the 
        /// </summary>
        public void StopAndLog()
        {
            _timer.Stop();

            if (_threshold.TotalMilliseconds == 0)
            {
                _logger.Info($"PERF : {_name} : took {_timer.Elapsed.TotalMilliseconds} ms.");
                return;
            }

            if (_timer.Elapsed > _threshold)
            {
                _logger.Warning($"PERF : {_name} : took {_timer.Elapsed.TotalMilliseconds} ms.");
                if (_publishTelemetry)
                {
                    // Is the precision enough?
                    _telemetry.AddToCumulativeTelemetry(_telemetryArea, _telemetryEventName, _timer.Elapsed, true);
                }
            }
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;
            if (disposing)
            {
                StopAndLog();
                GC.SuppressFinalize(this);
            }
            _disposed = true;
        }

        #region private variables.

        private bool _disposed;
        private ITraceLogger _logger;
        private ITelemetryDataCollector _telemetry;
        private readonly Stopwatch _timer;
        private readonly string _name;
        private readonly string _telemetryEventName;
        private readonly string _telemetryArea;
        private readonly TimeSpan _threshold;
        private readonly bool _publishTelemetry;

        #endregion
    }
}