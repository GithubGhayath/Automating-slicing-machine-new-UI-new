using MR200.UI.Database.Maintenance;

namespace MR200.UI.MaintenanceSystem
{
    public sealed class ElementFailureEventArgs : EventArgs
    {
        public ElementFailureEventArgs(MonitoredElement failedElement, IReadOnlyList<MonitoredElement> allElements)
        {
            FailedElement = failedElement;
            AllElements = allElements;
        }

        public MonitoredElement FailedElement { get; }
        public IReadOnlyList<MonitoredElement> AllElements { get; }
    }

    /// <summary>
    /// Tracks how much life every monitored element consumes while the machine runs.
    ///
    /// Lifecycle, mapped onto the machine controls the application already has:
    ///   BeginRun()          - Start pressed. Loads the elements once, from the database.
    ///   Tick()              - called from the existing simulation timer.
    ///   Pause()             - Stop pressed. Freezes accumulation WITHOUT persisting it.
    ///   CompleteOperation() - End Process pressed. Writes the accumulated life.
    ///
    /// The database is read once per production operation and written once per
    /// production operation; nothing queries it from inside the loop.
    /// </summary>
    public sealed class ElementLifeMonitoringService
    {
        private readonly List<MonitoredElement> _elements = new();
        private DateTime _lastTickAt;
        private double _cuttingShaftSpeedInRPM;

        /// <summary>Raised once, on the UI thread, the first time an element reaches its rated life.</summary>
        public event EventHandler<ElementFailureEventArgs>? ElementFailed;

        /// <summary>Raised when loading or saving monitored elements fails, so the UI can report it.</summary>
        public event EventHandler<Exception>? MonitoringError;

        /// <summary>Elements currently being monitored. Empty until the first RUN.</summary>
        public IReadOnlyList<MonitoredElement> Elements => _elements;

        /// <summary>True between the first RUN and the completion of that production operation.</summary>
        public bool IsSessionActive { get; private set; }

        /// <summary>True while the machine is actually running (false while paused).</summary>
        public bool IsRunning { get; private set; }

        /// <summary>Machine speed the cutting-shaft bearings are currently turning at.</summary>
        public double CuttingShaftSpeedInRPM => _cuttingShaftSpeedInRPM;

        /// <summary>
        /// RUN. Loads the monitored elements and their persistent ConsumedLife the
        /// first time, then starts accumulating. Pressing Start again after a pause
        /// resumes the same production operation rather than discarding its life.
        /// </summary>
        public void BeginRun(double cuttingShaftSpeedInRPM)
        {
            _cuttingShaftSpeedInRPM = cuttingShaftSpeedInRPM;

            if (!IsSessionActive)
            {
                if (!TryLoadElements()) return;
                IsSessionActive = true;
            }

            foreach (var element in _elements)
                element.RefreshRate(_cuttingShaftSpeedInRPM);

            // Reset the clock so time spent paused or stopped never counts as running.
            _lastTickAt = DateTime.Now;
            IsRunning = true;
        }

        /// <summary>
        /// PAUSE. Freezes the counters. Deliberately does NOT finalize or persist the
        /// accumulated life - that only happens when the operation completes.
        /// </summary>
        public void Pause()
        {
            IsRunning = false;
        }

        /// <summary>
        /// Advances every element life counter by the real elapsed wall-clock time
        /// since the previous tick, then checks for end of life. Independent of timer
        /// interval, frame rate and CPU speed.
        /// </summary>
        public void Tick()
        {
            if (!IsRunning || _elements.Count == 0) return;

            var now = DateTime.Now;
            double elapsedSeconds = (now - _lastTickAt).TotalSeconds;
            _lastTickAt = now;

            // A clock change or a very long stall must not inject a life spike.
            if (elapsedSeconds <= 0 || elapsedSeconds > 60) return;

            foreach (var element in _elements)
                element.Accumulate(elapsedSeconds);

            DetectEndOfLife();
        }

        /// <summary>
        /// Raises at most one failure per tick, and at most one per element ever.
        /// </summary>
        private void DetectEndOfLife()
        {
            var failed = _elements.FirstOrDefault(e => e.HasReachedEndOfLife);
            if (failed == null) return;

            // Latch before notifying so a re-entrant stop cannot raise it twice.
            failed.MarkFailed();
            IsRunning = false;

            ElementFailed?.Invoke(this, new ElementFailureEventArgs(failed, _elements.ToList()));
        }

        /// <summary>
        /// Production operation completed. Adds the runtime accumulation onto the
        /// persistent ConsumedLife in a single transaction and ends the session, so the
        /// next RUN reloads fresh values and continues counting from there.
        /// </summary>
        public bool CompleteOperation()
        {
            IsRunning = false;
            if (!IsSessionActive) return true;

            bool persisted = PersistAccumulatedLife();
            IsSessionActive = false;
            return persisted;
        }

        /// <summary>
        /// Writes the accumulated life without ending the session. Used when an element
        /// fails, so the failure and the life that caused it survive a restart.
        /// </summary>
        public bool PersistAccumulatedLife()
        {
            var pending = _elements
                .Where(e => e.RuntimeAccumulatedLife > 0)
                .ToDictionary(e => e.ElementId, e => e.RuntimeAccumulatedLife);

            if (pending.Count == 0) return true;

            try
            {
                MaintenanceCRUD.PersistAccumulatedLife(pending);
                foreach (var element in _elements)
                    element.OnAccumulationPersisted();
                return true;
            }
            catch (Exception ex)
            {
                MonitoringError?.Invoke(this, ex);
                return false;
            }
        }

        /// <summary>Latches the failure in the database so it is not raised again after a restart.</summary>
        public void PersistFailureFlag(int elementId)
        {
            try { MaintenanceCRUD.MarkElementFailed(elementId); }
            catch (Exception ex) { MonitoringError?.Invoke(this, ex); }
        }

        /// <summary>
        /// Drops the in-memory session, e.g. after an element has been replaced, so the
        /// next RUN reloads the corrected counters from the database.
        /// </summary>
        public void InvalidateSession()
        {
            IsRunning = false;
            IsSessionActive = false;
            _elements.Clear();
        }

        /// <summary>
        /// Elements at or past the configured warning threshold, worst first, excluding
        /// the one that just failed.
        /// </summary>
        public IReadOnlyList<MonitoredElement> GetElementsApproachingFailure(int excludeElementId)
        {
            double threshold = MaintenanceSettings.WarningThresholdPercent;
            return _elements
                .Where(e => e.ElementId != excludeElementId && e.LifeUsedPercentage >= threshold)
                .OrderByDescending(e => e.LifeUsedPercentage)
                .ToList();
        }

        private bool TryLoadElements()
        {
            try
            {
                var elements = MaintenanceCRUD.GetMonitoredElements();
                _elements.Clear();
                foreach (var element in elements)
                    _elements.Add(new MonitoredElement(element));
                return true;
            }
            catch (Exception ex)
            {
                MonitoringError?.Invoke(this, ex);
                return false;
            }
        }
    }
}
