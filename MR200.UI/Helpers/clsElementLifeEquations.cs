using DataAccess.Enums;

namespace MR200.UI.Helpers
{
    /// <summary>
    /// Where each monitored element sits in the drive train. Derived from
    /// <c>Elements.OrderOfElementAtMachine</c>, which is the operator-facing
    /// position number (1-16) stamped on the machine.
    /// </summary>
    public enum enElementDriveGroup
    {
        FeedingShaft = 1,
        CuttingShaft = 2,
        VBelt = 3
    }

    /// <summary>
    /// Converts machine running time into consumed element life.
    ///
    /// Every result is a rate per SECOND, so the caller multiplies by real elapsed
    /// wall-clock seconds. Life is therefore independent of frame rate, timer
    /// interval and CPU speed.
    /// </summary>
    public static class clsElementLifeEquations
    {
        /// <summary>
        /// Orders 1-8 are the two 16009 bearings on each of the four feeding shafts,
        /// 9-12 the 16007/16008 bearings on the two cutting shafts, 13-16 the A46
        /// V-belts driving the feeding shafts.
        /// </summary>
        public static enElementDriveGroup DriveGroupOf(int orderOfElementAtMachine) =>
            orderOfElementAtMachine switch
            {
                >= 1 and <= 8 => enElementDriveGroup.FeedingShaft,
                >= 9 and <= 12 => enElementDriveGroup.CuttingShaft,
                _ => enElementDriveGroup.VBelt
            };

        /// <summary>
        /// Revolutions performed in one second at a given shaft speed.
        /// dRevolutions = RPM * dt / 60
        /// </summary>
        public static double RevolutionsPerSecond_Unit_RevolutionPerSecond(double rotationalSpeedInRPM)
            => rotationalSpeedInRPM / 60.0;

        /// <summary>
        /// V-belt passes performed in one second. One "pass" is one complete circuit
        /// of the belt around its loop, so the belt covers its own pitch length once:
        /// passes/s = beltLinearSpeed [m/s] / beltPitchLength [m]
        /// </summary>
        public static double BeltPassesPerSecond_Unit_PassPerSecond(
            double beltLinearSpeedInMeterPerSecond, double beltPitchLengthInMeter)
        {
            if (beltPitchLengthInMeter <= 0) return 0;
            return beltLinearSpeedInMeterPerSecond / beltPitchLengthInMeter;
        }

        /// <summary>
        /// Life consumed per second of running by one element, expressed in that
        /// element's own life unit (revolutions for bearings, passes for belts).
        /// </summary>
        /// <param name="cuttingShaftSpeedInRPM">
        /// The machine's live cutting-shaft speed, taken from the application's own
        /// <see cref="clsMainEquations.NumberOfRotations_Unit_RPM"/> result so the
        /// two never diverge.
        /// </param>
        public static double LifeConsumedPerSecond(
            enElementDriveGroup driveGroup,
            double cuttingShaftSpeedInRPM,
            double feedingShaftSpeedInRPM,
            double beltLinearSpeedInMeterPerSecond,
            double beltPitchLengthInMeter)
        {
            double rate = driveGroup switch
            {
                enElementDriveGroup.FeedingShaft =>
                    RevolutionsPerSecond_Unit_RevolutionPerSecond(feedingShaftSpeedInRPM),
                enElementDriveGroup.CuttingShaft =>
                    RevolutionsPerSecond_Unit_RevolutionPerSecond(cuttingShaftSpeedInRPM),
                _ => BeltPassesPerSecond_Unit_PassPerSecond(
                    beltLinearSpeedInMeterPerSecond, beltPitchLengthInMeter)
            };

            return double.IsNaN(rate) || double.IsInfinity(rate) || rate < 0 ? 0 : rate;
        }

        /// <summary>Life added over a real elapsed interval. dLife = rate * dt.</summary>
        public static double LifeAdded(double lifeConsumedPerSecond, double elapsedSeconds)
        {
            if (elapsedSeconds <= 0) return 0;
            double added = lifeConsumedPerSecond * elapsedSeconds;
            return double.IsNaN(added) || double.IsInfinity(added) || added < 0 ? 0 : added;
        }

        /// <summary>Percentage of the rated life already used, clamped to 0..100+.</summary>
        public static double LifeUsedPercentage(double consumedLife, double defaultLife)
        {
            if (defaultLife <= 0 || double.IsNaN(defaultLife)) return 0;
            double pct = consumedLife / defaultLife * 100.0;
            return double.IsNaN(pct) || double.IsInfinity(pct) || pct < 0 ? 0 : pct;
        }

        /// <summary>Remaining life, never negative.</summary>
        public static double RemainingLife(double consumedLife, double defaultLife)
            => Math.Max(0, defaultLife - consumedLife);

        /// <summary>
        /// Health band for the predictive-maintenance display. Thresholds are
        /// configuration-driven, never scattered through the application.
        /// </summary>
        public static enElementHealthStatus HealthStatus(
            double lifeUsedPercentage, bool failureDetected,
            double warningThresholdPercent, double criticalThresholdPercent)
        {
            if (failureDetected || lifeUsedPercentage >= 100) return enElementHealthStatus.Failed;
            if (lifeUsedPercentage >= criticalThresholdPercent) return enElementHealthStatus.Critical;
            if (lifeUsedPercentage >= warningThresholdPercent) return enElementHealthStatus.Warning;
            return enElementHealthStatus.Normal;
        }

        /// <summary>Short unit label for display and e-mail.</summary>
        public static string UnitLabel(enElementLifeUnit unit)
            => unit == enElementLifeUnit.Pass ? "passes" : "revolutions";
    }
}
