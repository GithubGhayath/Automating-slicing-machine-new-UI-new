using DataAccess.Data;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace MR200.UI.Database.Maintenance
{
    /// <summary>
    /// Data access for the maintenance system. Follows the same static-context
    /// pattern already used by <see cref="Wood.WoodCRUD"/> and
    /// <see cref="Utility.Utility"/>.
    ///
    /// Calls here happen on RUN, on production completion and when the maintenance
    /// screen is opened - never inside the simulation loop.
    /// </summary>
    public static class MaintenanceCRUD
    {
        /// <summary>All monitored physical elements with their element-information, in machine order.</summary>
        public static List<Element> GetMonitoredElements()
        {
            using var context = new AppDbContext();
            return context.Elements
                .Include(e => e.ElementInformation)
                .OrderBy(e => e.OrderOfElementAtMachine)
                .AsNoTracking()
                .ToList();
        }

        /// <summary>All maintenance records, newest first, with their element and element-information.</summary>
        public static List<DataAccess.Entities.Maintenance> GetMaintenanceHistory()
        {
            using var context = new AppDbContext();
            return context.Maintenance
                .Include(m => m.Element).ThenInclude(e => e.ElementInformation)
                .OrderByDescending(m => m.MaintenanceDate)
                .AsNoTracking()
                .ToList();
        }

        /// <summary>Maintenance performed on one element inside the given window, newest first.</summary>
        public static List<DataAccess.Entities.Maintenance> GetMaintenanceForElementSince(int elementId, DateTime since)
        {
            using var context = new AppDbContext();
            return context.Maintenance
                .Where(m => m.ElementId == elementId && m.MaintenanceDate >= since)
                .OrderByDescending(m => m.MaintenanceDate)
                .AsNoTracking()
                .ToList();
        }

        /// <summary>The most recent maintenance operation on the whole machine, or null if there is none.</summary>
        public static DataAccess.Entities.Maintenance? GetLastMachineMaintenance()
        {
            using var context = new AppDbContext();
            return context.Maintenance
                .Include(m => m.Element).ThenInclude(e => e.ElementInformation)
                .OrderByDescending(m => m.MaintenanceDate)
                .AsNoTracking()
                .FirstOrDefault();
        }

        /// <summary>
        /// Writes the runtime-accumulated life of a completed production operation onto
        /// the persistent counters. One transaction for the whole batch so the counters
        /// can never end up half-written.
        /// </summary>
        /// <param name="accumulatedLifeByElementId">Element id -> life to add, in that element's unit.</param>
        public static void PersistAccumulatedLife(IReadOnlyDictionary<int, double> accumulatedLifeByElementId)
        {
            if (accumulatedLifeByElementId.Count == 0) return;

            using var context = new AppDbContext();
            using var transaction = context.Database.BeginTransaction();
            try
            {
                var ids = accumulatedLifeByElementId.Keys.ToList();
                var elements = context.Elements.Where(e => ids.Contains(e.Id)).ToList();

                foreach (var element in elements)
                    element.AccumulateLife(accumulatedLifeByElementId[element.Id]);

                context.SaveChanges();
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Latches the one-shot failure flag so the monitoring loop raises a single
        /// failure event per element even across application restarts.
        /// </summary>
        public static void MarkElementFailed(int elementId)
        {
            using var context = new AppDbContext();
            var element = context.Elements.FirstOrDefault(e => e.Id == elementId);
            if (element == null) return;
            element.MarkFailureDetected();
            context.SaveChanges();
        }

        /// <summary>
        /// Records a maintenance / replacement operation and, when the element was
        /// replaced, restarts its life at zero. Both happen in one transaction so the
        /// history and the counter can never disagree. Historical records are never
        /// deleted.
        /// </summary>
        /// <returns>The id of the created maintenance record.</returns>
        public static int RecordMaintenance(int elementId, string doneBy, double cost,
            double elementPrice, double stoppingTimeCost, DateTime maintenanceDate,
            bool resetConsumedLife)
        {
            using var context = new AppDbContext();
            using var transaction = context.Database.BeginTransaction();
            try
            {
                var element = context.Elements.FirstOrDefault(e => e.Id == elementId)
                    ?? throw new InvalidOperationException($"Element {elementId} was not found.");

                var record = new DataAccess.Entities.Maintenance(doneBy, cost, elementPrice,
                    stoppingTimeCost, maintenanceDate);

                element.Maintenances.Add(record);

                if (resetConsumedLife)
                    element.ResetLifeAfterReplacement();

                context.SaveChanges();
                transaction.Commit();
                return record.Id;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Overrides an element type's rated life. Used only by the controlled
        /// end-of-life test described in the maintenance documentation.
        /// </summary>
        public static void SetDefaultLife(int elementInformationId, double defaultLife)
        {
            using var context = new AppDbContext();
            var info = context.ElementsInformation.FirstOrDefault(i => i.Id == elementInformationId);
            if (info == null) return;
            info.SetDefaultLife(defaultLife);
            context.SaveChanges();
        }
    }
}
