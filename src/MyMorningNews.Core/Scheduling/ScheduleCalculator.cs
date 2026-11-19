namespace MyMorningNews.Core.Scheduling
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Cronos;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// Computes schedule occurrences (plan section 14). Pure: no I/O and no clock reads. Day-based schedules are computed in
    /// the schedule's IANA time zone. A local time that does not exist (spring forward) moves forward past the gap, and an
    /// ambiguous local time (fall back) fires once, at its first occurrence.
    /// Thread safety: all members are thread safe.
    /// </summary>
    public static class ScheduleCalculator
    {
        #region Private-Members

        private const int _MaxScanDays = 800;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Validate a schedule and normalize its values (clamps intervals, sorts and de-duplicates times and days).
        /// </summary>
        /// <param name="schedule">Schedule; normalized in place.</param>
        /// <param name="minimumIntervalMinutes">Smallest EveryNMinutes interval allowed. Minimum 1.</param>
        /// <returns>Validation errors; empty when valid.</returns>
        /// <exception cref="ArgumentNullException">schedule is null.</exception>
        public static List<string> Validate(Schedule schedule, int minimumIntervalMinutes)
        {
            ArgumentNullException.ThrowIfNull(schedule);
            List<string> errors = new List<string>();
            if (TimeZoneHelper.TryFind(schedule.TimeZone) == null) errors.Add("timeZone \"" + schedule.TimeZone + "\" is not a known IANA time zone.");
            schedule.TimesOfDay = (schedule.TimesOfDay ?? new List<string>()).Select(t => (t ?? String.Empty).Trim()).Where(t => t.Length > 0).Distinct().ToList();
            foreach (string t in schedule.TimesOfDay)
                if (!TryParseTime(t, out _)) errors.Add("timesOfDay value \"" + t + "\" must be HH:mm (00:00 to 23:59).");
            schedule.TimesOfDay = schedule.TimesOfDay.Where(t => TryParseTime(t, out _)).Select(t => { TryParseTime(t, out TimeSpan ts); return ts.ToString(@"hh\:mm", CultureInfo.InvariantCulture); }).Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList();
            if (schedule.EndUtc.HasValue && schedule.StartUtc.HasValue && schedule.EndUtc.Value <= schedule.StartUtc.Value) errors.Add("endUtc must be after startUtc.");

            bool needsTimes = schedule.IntervalType == IntervalTypeEnum.EveryNDays || schedule.IntervalType == IntervalTypeEnum.EveryNWeeks ||
                schedule.IntervalType == IntervalTypeEnum.DaysOfWeek || schedule.IntervalType == IntervalTypeEnum.DaysOfMonth;
            if (needsTimes && schedule.TimesOfDay.Count == 0) errors.Add("timesOfDay needs at least one time for " + schedule.IntervalType + ".");
            if (schedule.TimesOfDay.Count > 24) errors.Add("timesOfDay is limited to 24 times.");

            switch (schedule.IntervalType)
            {
                case IntervalTypeEnum.EveryNMinutes:
                    schedule.IntervalValue = Math.Clamp(schedule.IntervalValue, Math.Max(1, minimumIntervalMinutes), 1440);
                    break;
                case IntervalTypeEnum.EveryNHours:
                    schedule.IntervalValue = Math.Clamp(schedule.IntervalValue, 1, 168);
                    schedule.MinuteOffset = Math.Clamp(schedule.MinuteOffset, 0, 59);
                    break;
                case IntervalTypeEnum.EveryNDays:
                    schedule.IntervalValue = Math.Clamp(schedule.IntervalValue, 1, 365);
                    break;
                case IntervalTypeEnum.EveryNWeeks:
                    schedule.IntervalValue = Math.Clamp(schedule.IntervalValue, 1, 52);
                    if ((schedule.DaysOfWeekMask & 127) == 0) errors.Add("daysOfWeekMask needs at least one day.");
                    break;
                case IntervalTypeEnum.DaysOfWeek:
                    if ((schedule.DaysOfWeekMask & 127) == 0) errors.Add("daysOfWeekMask needs at least one day.");
                    break;
                case IntervalTypeEnum.DaysOfMonth:
                    schedule.DaysOfMonth = (schedule.DaysOfMonth ?? new List<int>()).Distinct().OrderBy(d => d).ToList();
                    if (schedule.DaysOfMonth.Count == 0) errors.Add("daysOfMonth needs at least one day.");
                    if (schedule.DaysOfMonth.Any(d => d != -1 && (d < 1 || d > 31))) errors.Add("daysOfMonth values are 1 to 31, or -1 for the last day.");
                    break;
                case IntervalTypeEnum.Once:
                    if (!schedule.RunOnceUtc.HasValue) errors.Add("runOnceUtc is required for Once.");
                    break;
                case IntervalTypeEnum.Cron:
                    if (String.IsNullOrWhiteSpace(schedule.CronExpression)) errors.Add("cronExpression is required for Cron.");
                    else if (!TryParseCron(schedule.CronExpression, out _)) errors.Add("cronExpression \"" + schedule.CronExpression + "\" is not valid.");
                    break;
            }

            schedule.DaysOfWeekMask &= 127;
            return errors;
        }

        /// <summary>
        /// The first occurrence strictly after a time.
        /// </summary>
        /// <param name="schedule">Schedule (validated).</param>
        /// <param name="afterUtc">Exclusive lower bound (UTC).</param>
        /// <returns>Next occurrence (UTC), or null when the schedule never fires again.</returns>
        /// <exception cref="ArgumentNullException">schedule is null.</exception>
        public static DateTime? NextOccurrence(Schedule schedule, DateTime afterUtc)
        {
            ArgumentNullException.ThrowIfNull(schedule);
            DateTime after = Utc(afterUtc);
            if (schedule.StartUtc.HasValue && after < Utc(schedule.StartUtc.Value).AddTicks(-1)) after = Utc(schedule.StartUtc.Value).AddTicks(-1);
            TimeZoneInfo tz = TimeZoneHelper.TryFind(schedule.TimeZone) ?? TimeZoneInfo.Utc;
            DateTime? next = schedule.IntervalType switch
            {
                IntervalTypeEnum.EveryNMinutes => FixedStep(Anchor(schedule), TimeSpan.FromMinutes(Math.Max(1, schedule.IntervalValue)), after),
                IntervalTypeEnum.EveryNHours => FixedStep(HourAnchor(schedule, tz), TimeSpan.FromHours(Math.Max(1, schedule.IntervalValue)), after),
                IntervalTypeEnum.Once => schedule.RunOnceUtc.HasValue && Utc(schedule.RunOnceUtc.Value) > after ? Utc(schedule.RunOnceUtc.Value) : null,
                IntervalTypeEnum.Cron => NextCron(schedule, tz, after),
                IntervalTypeEnum.Manual => null,
                _ => NextByDay(schedule, tz, after)
            };

            if (next.HasValue && schedule.EndUtc.HasValue && next.Value > Utc(schedule.EndUtc.Value)) return null;
            return next;
        }

        /// <summary>
        /// The next occurrences strictly after a time.
        /// </summary>
        /// <param name="schedule">Schedule (validated).</param>
        /// <param name="afterUtc">Exclusive lower bound (UTC).</param>
        /// <param name="count">How many. Minimum 1, maximum 100.</param>
        /// <returns>Occurrences in ascending order; fewer when the schedule ends.</returns>
        /// <exception cref="ArgumentNullException">schedule is null.</exception>
        public static List<DateTime> NextOccurrences(Schedule schedule, DateTime afterUtc, int count)
        {
            ArgumentNullException.ThrowIfNull(schedule);
            List<DateTime> result = new List<DateTime>();
            DateTime cursor = Utc(afterUtc);
            int n = Math.Clamp(count, 1, 100);
            while (result.Count < n)
            {
                DateTime? next = NextOccurrence(schedule, cursor);
                if (!next.HasValue) break;
                result.Add(next.Value);
                cursor = next.Value;
            }

            return result;
        }

        /// <summary>
        /// Convert a local wall-clock time in a time zone to UTC: a nonexistent time moves forward past the gap, and an
        /// ambiguous time resolves to its first occurrence.
        /// </summary>
        /// <param name="local">Local time (kind ignored).</param>
        /// <param name="tz">Time zone.</param>
        /// <returns>UTC time.</returns>
        /// <exception cref="ArgumentNullException">tz is null.</exception>
        public static DateTime LocalToUtc(DateTime local, TimeZoneInfo tz)
        {
            ArgumentNullException.ThrowIfNull(tz);
            DateTime l = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
            if (tz.IsInvalidTime(l))
            {
                // Use the offset in effect just before the gap, so 02:30 on a spring-forward day becomes 03:30 local.
                DateTime before = l;
                for (int i = 0; i < 240 && tz.IsInvalidTime(before); i++) before = before.AddMinutes(-1);
                return DateTime.SpecifyKind(l - tz.GetUtcOffset(before), DateTimeKind.Utc);
            }

            if (tz.IsAmbiguousTime(l))
            {
                TimeSpan offset = tz.GetAmbiguousTimeOffsets(l).Max();
                return DateTime.SpecifyKind(l - offset, DateTimeKind.Utc);
            }

            return DateTime.SpecifyKind(l - tz.GetUtcOffset(l), DateTimeKind.Utc);
        }

        #endregion

        #region Private-Methods

        private static DateTime Utc(DateTime value)
        {
            return value.Kind == DateTimeKind.Utc ? value : value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        private static DateTime Anchor(Schedule schedule)
        {
            DateTime a = Utc(schedule.StartUtc ?? schedule.CreatedUtc);
            return new DateTime(a.Year, a.Month, a.Day, a.Hour, a.Minute, 0, DateTimeKind.Utc);
        }

        private static DateTime HourAnchor(Schedule schedule, TimeZoneInfo tz)
        {
            DateTime a = Utc(schedule.StartUtc ?? schedule.CreatedUtc);
            DateTime local = TimeZoneInfo.ConvertTimeFromUtc(a, tz);
            DateTime localAnchor = new DateTime(local.Year, local.Month, local.Day, local.Hour, Math.Clamp(schedule.MinuteOffset, 0, 59), 0, DateTimeKind.Unspecified);
            return LocalToUtc(localAnchor, tz);
        }

        private static DateTime FixedStep(DateTime anchor, TimeSpan step, DateTime after)
        {
            if (anchor > after) return anchor;
            long steps = (after - anchor).Ticks / step.Ticks + 1;
            return anchor.AddTicks(steps * step.Ticks);
        }

        private static DateTime? NextCron(Schedule schedule, TimeZoneInfo tz, DateTime after)
        {
            if (!TryParseCron(schedule.CronExpression, out CronExpression? cron) || cron == null) return null;
            return cron.GetNextOccurrence(after, tz, false);
        }

        private static DateTime? NextByDay(Schedule schedule, TimeZoneInfo tz, DateTime after)
        {
            List<TimeSpan> times = schedule.TimesOfDay.Select(t => { bool ok = TryParseTime(t, out TimeSpan ts); return (ok, ts); }).Where(x => x.ok).Select(x => x.ts).OrderBy(x => x).ToList();
            if (times.Count == 0) return null;
            DateTime anchorLocal = TimeZoneInfo.ConvertTimeFromUtc(Utc(schedule.StartUtc ?? schedule.CreatedUtc), tz).Date;
            DateTime day = TimeZoneInfo.ConvertTimeFromUtc(after, tz).Date.AddDays(-1);
            for (int i = 0; i < _MaxScanDays; i++, day = day.AddDays(1))
            {
                if (!DayMatches(schedule, day, anchorLocal)) continue;
                foreach (TimeSpan t in times)
                {
                    DateTime utc = LocalToUtc(day + t, tz);
                    if (utc > after) return utc;
                }
            }

            return null;
        }

        private static bool DayMatches(Schedule schedule, DateTime day, DateTime anchorLocal)
        {
            int dowBit = 1 << (int)day.DayOfWeek;
            switch (schedule.IntervalType)
            {
                case IntervalTypeEnum.EveryNDays:
                    int days = (int)(day - anchorLocal).TotalDays;
                    return days >= 0 && days % Math.Max(1, schedule.IntervalValue) == 0;
                case IntervalTypeEnum.EveryNWeeks:
                    DateTime anchorWeek = anchorLocal.AddDays(-(int)anchorLocal.DayOfWeek);
                    DateTime week = day.AddDays(-(int)day.DayOfWeek);
                    int weeks = (int)((week - anchorWeek).TotalDays / 7);
                    return weeks >= 0 && weeks % Math.Max(1, schedule.IntervalValue) == 0 && (schedule.DaysOfWeekMask & dowBit) != 0;
                case IntervalTypeEnum.DaysOfWeek:
                    return (schedule.DaysOfWeekMask & dowBit) != 0;
                case IntervalTypeEnum.DaysOfMonth:
                    int last = DateTime.DaysInMonth(day.Year, day.Month);
                    return schedule.DaysOfMonth.Contains(day.Day) || (day.Day == last && schedule.DaysOfMonth.Contains(-1));
                default:
                    return false;
            }
        }

        private static bool TryParseTime(string value, out TimeSpan time)
        {
            time = TimeSpan.Zero;
            if (!TimeSpan.TryParseExact(value, new[] { @"hh\:mm", @"h\:mm" }, CultureInfo.InvariantCulture, out TimeSpan t)) return false;
            if (t < TimeSpan.Zero || t >= TimeSpan.FromDays(1)) return false;
            time = t;
            return true;
        }

        private static bool TryParseCron(string? expression, out CronExpression? cron)
        {
            cron = null;
            if (String.IsNullOrWhiteSpace(expression)) return false;
            string e = expression.Trim();
            try
            {
                cron = CronExpression.Parse(e, e.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length == 6 ? CronFormat.IncludeSeconds : CronFormat.Standard);
                return true;
            }
            catch (CronFormatException)
            {
                return false;
            }
        }

        #endregion
    }
}
