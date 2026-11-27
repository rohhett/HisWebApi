using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using HISWEBAPI.DTO;
using HISWEBAPI.Exceptions;
using HISWEBAPI.Models;
using Microsoft.Extensions.Caching.Distributed;

namespace HISWEBAPI.Repositories.Implementations
{
    /// <summary>
    /// Lab TAT Monitor (2026-11-26). Screen and rules from the owner's "Lab TAT Monitor" design
    /// (Test + Priority + Shift + Working Calendar). No OLD HIS page exists for this feature.
    ///
    /// Due time of a sample:
    ///   test → department; TAT start time → shift; of all matching rules the most specific wins
    ///   (Test 40 &gt; Department 20 &gt; Default 0, +4 when the priority is named, +2 when the shift is named;
    ///   on a tie an explicit rule beats the Investigation Master TAT, and the newer rule wins);
    ///   clock = rule clock, or the priority's clock when the rule says INHERIT;
    ///   24X7 adds the minutes straight; WORKING counts only the department's open hours and skips holidays.
    /// Status: reported → On time / Late (end after due); pending → Breached (due passed),
    ///   At risk (time left below the at-risk % of the target), else On track.
    /// SPs: script Database/2026-11-26_LabTATMonitor.sql.
    /// </summary>
    public partial class LabRepository
    {
        private const int TatFallbackMinutes = 240;        // design: rule not found → 4 h
        private const int TatCalendarMaxDays = 60;         // design: working-calendar walk stops after 60 days
        private const int TatWorklistDays = 60;            // pending samples ordered in the last 60 days
        private const int TatSampleWindowExtraDays = 15;   // a sample can be received some days after it was ordered
        private const int TatMaxRows = 1000;

        private static readonly string[] TatStartPoints = { "order", "collected", "received" };
        private static readonly string[] TatEndPoints = { "entered", "verified", "dispatched" };
        private static readonly string[] TatRuleClocks = { "INHERIT", "WORKING", "24X7" };
        private static readonly string[] TatDayShort = { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" };
        private static readonly Regex TatTimeRegex = new(@"^([01]\d|2[0-3]):[0-5]\d$");

        private static string TatConfigCacheKey(int hospId) => $"_LabTATConfig_{hospId}";

        private sealed class TatSampleRow
        {
            public int PatientInvestigationId, LabNo, InvestigationId, DepartmentId, IsUrgent, DelayReasonId;
            public string BarCode = "", VisitType = "", UHID = "", PatientName = "", DelayReason = "";
            public DateTime? OrderedOn, CollectedOn, ReceivedOn, EnteredOn, VerifiedOn, DispatchedOn;
        }

        private sealed class TatCalc
        {
            public TatSampleRow S = null!;
            public LabTATTestModel? Test;
            public LabTATPriorityModel? Priority;
            public LabTATShiftModel? Shift;
            public LabTATRuleModel? Rule;
            public string Clock = "WORKING";
            public DateTime Start, Due;
            public DateTime? End;
            public double Target, Delay;
            public double? Tat;
            public string Status = "";
        }

        #region Reads

        public ServiceResult<LabTATConfigModel> GetLabTATConfig(AllGlobalValues globalValues)
        {
            try
            {
                var cfg = LoadTatConfig(globalValues.hospId);

                // samples per rule over the last 30 days, like the design's "Samples" column
                var from = DateTime.Today.AddDays(-30);
                var calcs = CalcTat(LoadTatSamples(globalValues.hospId, from.AddDays(-TatSampleWindowExtraDays)), cfg, DateTime.Now)
                    .Where(c => c.Start >= from).ToList();
                var used = calcs.Where(c => c.Rule != null).GroupBy(c => c.Rule!.RuleId).ToDictionary(g => g.Key, g => g.Count());
                foreach (var r in cfg.Rules) r.SampleCount = used.TryGetValue(r.RuleId, out var n) ? n : 0;

                // Test Master tab: effective TAT per test and priority in the first shift
                var firstShift = cfg.Shifts.FirstOrDefault();
                cfg.TestMatrix = cfg.Tests.Select(t => new LabTATTestMatrixModel
                {
                    InvestigationId = t.InvestigationId,
                    InvestigationCode = t.InvestigationCode,
                    InvestigationName = t.InvestigationName,
                    DepartmentName = t.DepartmentName,
                    Cells = cfg.Priorities.Select(p =>
                    {
                        var rule = MatchTatRule(cfg, t.InvestigationId, t.DepartmentId, p.PriorityId, firstShift?.ShiftId ?? 0);
                        return new LabTATTestMatrixCellModel { PriorityId = p.PriorityId, TatMinutes = rule?.TatMinutes, RuleLevel = rule?.RuleLevel ?? "" };
                    }).ToList()
                }).ToList();

                return ServiceResult<LabTATConfigModel>.Success(cfg, "Info", $"{cfg.Rules.Count} TAT rule(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                return TatServerError<LabTATConfigModel>(ex);
            }
        }

        public ServiceResult<LabTATDashboardModel> GetLabTATDashboard(string? period, int? departmentId, int? priorityId, AllGlobalValues globalValues)
        {
            try
            {
                var cfg = LoadTatConfig(globalValues.hospId);
                var now = DateTime.Now;
                int pdays = TatPeriodDays(period);
                var from = DateTime.Today.AddDays(-pdays);
                var all = CalcTat(LoadTatSamples(globalValues.hospId, from.AddDays(-TatSampleWindowExtraDays)), cfg, now);
                var f = all.Where(c => c.Start >= from
                                       && (departmentId is null or 0 || c.S.DepartmentId == departmentId)
                                       && (priorityId is null or 0 || c.Priority?.PriorityId == priorityId)).ToList();
                int tg = cfg.Setting.OnTimeTargetPercent;

                var model = new LabTATDashboardModel
                {
                    TargetPercent = tg,
                    AtRiskPercent = cfg.Setting.AtRiskPercent,
                    BasisText = cfg.BasisText,
                    PeriodText = pdays == 0 ? "Today" : pdays == 6 ? "Last 7 days" : "Last 30 days",
                    PrintedOn = now.ToString("dd-MM-yyyy HH:mm"),
                    Summary = TatStats(f, cfg),
                    ByPriority = cfg.Priorities.Select(p => Group(p.PriorityId.ToString(), p.PriorityName, f.Where(c => c.Priority?.PriorityId == p.PriorityId), cfg)).ToList(),
                    ByShift = cfg.Shifts.Select(s => Group(s.ShiftId.ToString(), $"{s.ShiftName} ({s.FromTime}–{s.ToTime})", f.Where(c => c.Shift?.ShiftId == s.ShiftId), cfg)).ToList(),
                    ByDepartment = cfg.Departments.Select(d => Group(d.DepartmentId.ToString(), d.DepartmentName, f.Where(c => c.S.DepartmentId == d.DepartmentId), cfg)).ToList(),
                };

                // test-wise: only tests that have samples in the period
                model.ByTest = f.GroupBy(c => c.S.InvestigationId)
                    .Select(g =>
                    {
                        var t = g.First().Test;
                        var gs = Group(g.Key.ToString(), t == null ? $"Test {g.Key}" : $"{t.InvestigationName}{(string.IsNullOrEmpty(t.InvestigationCode) ? "" : " · " + t.InvestigationCode)}", g, cfg);
                        gs.DepartmentId = g.First().S.DepartmentId;
                        return gs;
                    })
                    .OrderBy(g => g.Label).ToList();

                for (int d = Math.Min(pdays, 13); d >= 0; d--)
                {
                    var day = DateTime.Today.AddDays(-d);
                    model.ByDay.Add(Group(day.ToString("yyyy-MM-dd"), $"{day:dd} {TatDayShort[(int)day.DayOfWeek]}", f.Where(c => c.Start.Date == day), cfg));
                }

                var prioCols = cfg.Priorities.Select(p => (p.PriorityId.ToString(), p.PriorityName)).ToList();
                var deptRows = cfg.Departments.Select(d => (d.DepartmentId.ToString(), d.DepartmentName)).ToList();
                var shiftKeys = cfg.Shifts.Select(s => (s.ShiftId.ToString(), s.ShiftName)).ToList();
                string PrioKey(TatCalc c) => c.Priority?.PriorityId.ToString() ?? "";
                string DeptKey(TatCalc c) => c.S.DepartmentId.ToString();
                string ShiftKey(TatCalc c) => c.Shift?.ShiftId.ToString() ?? "";
                model.DeptPriority = Matrix(f, cfg, deptRows, DeptKey, prioCols, PrioKey);
                model.ShiftPriority = Matrix(f, cfg, shiftKeys, ShiftKey, prioCols, PrioKey);
                model.DeptShift = Matrix(f, cfg, deptRows, DeptKey, shiftKeys, ShiftKey);

                var late = f.Where(c => c.Status == "Late").ToList();
                int cum = 0;
                model.LateReasons = late.GroupBy(c => string.IsNullOrEmpty(c.S.DelayReason) ? "Reason not entered" : c.S.DelayReason)
                    .OrderByDescending(g => g.Count())
                    .Select(g =>
                    {
                        cum += g.Count();
                        return new LabTATLateReasonModel
                        {
                            Reason = g.Key,
                            Count = g.Count(),
                            Percent = Math.Round(g.Count() * 100.0 / late.Count, 1),
                            CumPercent = Math.Round(cum * 100.0 / late.Count, 0)
                        };
                    }).ToList();

                model.TopLateTests = f.Where(c => c.End != null).GroupBy(c => c.S.InvestigationId)
                    .Select(g => new LabTATTopLateTestModel
                    {
                        InvestigationName = g.First().Test?.InvestigationName ?? "",
                        DepartmentName = g.First().Test?.DepartmentName ?? "",
                        Reported = g.Count(),
                        Late = g.Count(c => c.Status == "Late"),
                    })
                    .Where(x => x.Late > 0)
                    .OrderByDescending(x => x.Late).Take(8)
                    .Select(x => { x.LatePercent = Math.Round(x.Late * 100.0 / x.Reported, 1); return x; })
                    .ToList();

                return ServiceResult<LabTATDashboardModel>.Success(model, "Info", $"{f.Count} sample(s) in the period", 200);
            }
            catch (Exception ex)
            {
                return TatServerError<LabTATDashboardModel>(ex);
            }
        }

        public ServiceResult<LabTATWorklistModel> GetLabTATWorklist(int? departmentId, AllGlobalValues globalValues)
        {
            try
            {
                var cfg = LoadTatConfig(globalValues.hospId);
                var now = DateTime.Now;
                var pend = CalcTat(LoadTatSamples(globalValues.hospId, DateTime.Today.AddDays(-TatWorklistDays)), cfg, now)
                    .Where(c => c.End == null && (departmentId is null or 0 || c.S.DepartmentId == departmentId))
                    .OrderBy(c => c.Due).ToList();

                var model = new LabTATWorklistModel
                {
                    Now = now.ToString("dd-MM-yyyy HH:mm"),
                    Pending = pend.Count,
                    Breached = pend.Count(c => c.Status == "Breached"),
                    AtRisk = pend.Count(c => c.Status == "At risk"),
                    AtRiskPercent = cfg.Setting.AtRiskPercent,
                    Rows = pend.Take(TatMaxRows).Select(c => ToSampleModel(c, now)).ToList()
                };
                return ServiceResult<LabTATWorklistModel>.Success(model, "Info", $"{model.Pending} pending sample(s)", 200);
            }
            catch (Exception ex)
            {
                return TatServerError<LabTATWorklistModel>(ex);
            }
        }

        public ServiceResult<IEnumerable<LabTATSampleModel>> GetLabTATLateRegister(string? period, int? departmentId, int? priorityId, AllGlobalValues globalValues)
        {
            try
            {
                var cfg = LoadTatConfig(globalValues.hospId);
                var now = DateTime.Now;
                var from = DateTime.Today.AddDays(-TatPeriodDays(period));
                var list = CalcTat(LoadTatSamples(globalValues.hospId, from.AddDays(-TatSampleWindowExtraDays)), cfg, now)
                    .Where(c => c.Status == "Late" && c.Start >= from
                                && (departmentId is null or 0 || c.S.DepartmentId == departmentId)
                                && (priorityId is null or 0 || c.Priority?.PriorityId == priorityId))
                    .OrderByDescending(c => c.Delay)
                    .Take(TatMaxRows)
                    .Select(c => ToSampleModel(c, now)).ToList();
                return ServiceResult<IEnumerable<LabTATSampleModel>>.Success(list, "Info", $"{list.Count} late report(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                return TatServerError<IEnumerable<LabTATSampleModel>>(ex);
            }
        }

        public ServiceResult<LabTATSimulationModel> SimulateLabTAT(SimulateLabTATRequest request, AllGlobalValues globalValues)
        {
            try
            {
                var cfg = LoadTatConfig(globalValues.hospId);
                var test = cfg.Tests.FirstOrDefault(t => t.InvestigationId == request.InvestigationId);
                var prio = cfg.Priorities.FirstOrDefault(p => p.PriorityId == request.PriorityId);
                if (test == null) return TatInvalid<LabTATSimulationModel>("Select a lab test");
                if (prio == null) return TatInvalid<LabTATSimulationModel>("Select a priority");

                var start = request.StartOn!.Value;
                var shift = ShiftOf(start, cfg);
                var rule = MatchTatRule(cfg, test.InvestigationId, test.DepartmentId, prio.PriorityId, shift?.ShiftId ?? 0);
                var clock = ClockOf(rule, prio);
                var trace = new List<string>();
                var due = DueOf(start, rule?.TatMinutes ?? TatFallbackMinutes, test.DepartmentId, clock, cfg, trace);
                var cands = TatCandidates(cfg, test.InvestigationId, test.DepartmentId, prio.PriorityId, shift?.ShiftId ?? 0);

                string calendar = clock == "24X7" ? "Not applied"
                    : string.Join(" · ", Enumerable.Range(0, 7).Select(i =>
                    {
                        var w = cfg.WorkingHours.FirstOrDefault(h => h.DepartmentId == test.DepartmentId && h.DayOfWeekNo == i);
                        return TatDayShort[i] + " " + (w == null ? "00:00-24:00" : w.IsOpen == 1 ? $"{w.FromTime}-{w.ToTime}" : "closed");
                    }));

                var model = new LabTATSimulationModel
                {
                    DueOn = due.ToString("dd-MM-yyyy HH:mm"),
                    SpanMinutes = Math.Round((due - start).TotalMinutes),
                    RuleMinutes = rule?.TatMinutes,
                    Trace = trace,
                    Candidates = cands.Select(r => new LabTATSimulationCandidateModel { Text = $"{r.LevelText}: {TatDur(r.TatMinutes)}", IsChosen = rule != null && r.RuleId == rule.RuleId }).ToList(),
                    Steps = new List<LabTATSimulationStepModel>
                    {
                        new() { Label = "1. Test → Department", Value = $"{test.InvestigationName} → {(string.IsNullOrEmpty(test.DepartmentName) ? "-" : test.DepartmentName)}" },
                        new() { Label = "2. Shift (from start time)", Value = $"{shift?.ShiftName ?? "-"} · {start:dd-MM-yyyy HH:mm} ({start:ddd})" },
                        new() { Label = "3. Matching rules", Value = $"{cands.Count} rule(s) match; the most specific is chosen" },
                        new() { Label = "4. Chosen rule", Value = rule == null ? $"none (fallback {TatDur(TatFallbackMinutes)})" : $"{rule.LevelText} → {TatDur(rule.TatMinutes)}{(string.IsNullOrEmpty(rule.Note) ? "" : $" ({rule.Note})")}" },
                        new() { Label = "5. Clock", Value = $"{TatClockText(clock)}{(rule == null || rule.ClockType == "INHERIT" ? $" (from priority {prio.PriorityName})" : " (from rule)")}" },
                        new() { Label = "6. Working calendar", Value = calendar },
                    }
                };
                return ServiceResult<LabTATSimulationModel>.Success(model, "Info", "TAT calculated", 200);
            }
            catch (Exception ex)
            {
                return TatServerError<LabTATSimulationModel>(ex);
            }
        }

        #endregion

        #region Save

        public ServiceResult<LabTATSaveResponse> CreateUpdateLabTATRule(CreateUpdateLabTATRuleRequest request, AllGlobalValues globalValues)
        {
            try
            {
                var cfg = LoadTatConfig(globalValues.hospId);
                string level = (request.RuleLevel ?? "").Trim().ToUpperInvariant();
                string clock = (request.ClockType ?? "").Trim().ToUpperInvariant();
                if (!TatRuleClocks.Contains(clock)) return TatInvalid<LabTATSaveResponse>("Clock must be Inherit, Working calendar or 24x7");
                if (request.RuleId == 0)
                {
                    if (level is not ("TEST" or "DEPT" or "ALL")) return TatInvalid<LabTATSaveResponse>("Level must be Test, Department or All tests");
                    if (level == "TEST" && cfg.Tests.All(t => t.InvestigationId != request.InvestigationId)) return TatInvalid<LabTATSaveResponse>("Select a lab test");
                    if (level == "DEPT" && cfg.Departments.All(d => d.DepartmentId != request.DepartmentId)) return TatInvalid<LabTATSaveResponse>("Select a department");
                    if (request.PriorityId != 0 && cfg.Priorities.All(p => p.PriorityId != request.PriorityId)) return TatInvalid<LabTATSaveResponse>("Select a valid priority");
                    if (request.ShiftId != 0 && cfg.Shifts.All(s => s.ShiftId != request.ShiftId)) return TatInvalid<LabTATSaveResponse>("Select a valid shift");
                }

                return TatWrite("IU_LabTATRule", new
                {
                    hospId = globalValues.hospId,
                    ruleId = request.RuleId,
                    ruleLevel = level,
                    investigationId = request.InvestigationId,
                    departmentId = request.DepartmentId,
                    priorityId = request.PriorityId,
                    shiftId = request.ShiftId,
                    tatMinutes = request.TatMinutes,
                    clockType = clock,
                    note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                    userId = globalValues.userId,
                    IpAddress = globalValues.ipAddress
                }, request.RuleId == 0, globalValues.hospId, "The same TAT rule already exists — change its minutes in the grid");
            }
            catch (Exception ex)
            {
                return TatServerError<LabTATSaveResponse>(ex);
            }
        }

        public ServiceResult<LabTATSaveResponse> UpdateLabTATRuleStatus(UpdateLabTATRuleStatusRequest request, AllGlobalValues globalValues)
        {
            try
            {
                return TatWrite("U_LabTATRuleStatus", new
                {
                    hospId = globalValues.hospId,
                    ruleId = request.RuleId,
                    userId = globalValues.userId,
                    IpAddress = globalValues.ipAddress
                }, false, globalValues.hospId, null, new Dictionary<int, string>
                {
                    [-2] = "The default rule of a priority cannot be removed — change its minutes instead"
                });
            }
            catch (Exception ex)
            {
                return TatServerError<LabTATSaveResponse>(ex);
            }
        }

        public ServiceResult<LabTATSaveResponse> UpdateLabTATPriorityClock(UpdateLabTATPriorityClockRequest request, AllGlobalValues globalValues)
        {
            try
            {
                string clock = (request.ClockType ?? "").Trim().ToUpperInvariant();
                if (clock is not ("WORKING" or "24X7")) return TatInvalid<LabTATSaveResponse>("Clock must be Working calendar or 24x7");
                return TatWrite("U_LabTATPriorityClock", new
                {
                    hospId = globalValues.hospId,
                    priorityId = request.PriorityId,
                    clockType = clock,
                    userId = globalValues.userId,
                    IpAddress = globalValues.ipAddress
                }, false, globalValues.hospId);
            }
            catch (Exception ex)
            {
                return TatServerError<LabTATSaveResponse>(ex);
            }
        }

        public ServiceResult<LabTATSaveResponse> UpdateLabTATShift(UpdateLabTATShiftRequest request, AllGlobalValues globalValues)
        {
            try
            {
                if (!TatTimeRegex.IsMatch(request.FromTime ?? "") || !TatTimeRegex.IsMatch(request.ToTime ?? ""))
                    return TatInvalid<LabTATSaveResponse>("Shift times must be HH:mm");
                if (request.FromTime == request.ToTime) return TatInvalid<LabTATSaveResponse>("Shift start and end cannot be the same");
                return TatWrite("U_LabTATShift", new
                {
                    hospId = globalValues.hospId,
                    shiftId = request.ShiftId,
                    fromTime = request.FromTime,
                    toTime = request.ToTime,
                    userId = globalValues.userId,
                    IpAddress = globalValues.ipAddress
                }, false, globalValues.hospId);
            }
            catch (Exception ex)
            {
                return TatServerError<LabTATSaveResponse>(ex);
            }
        }

        public ServiceResult<LabTATSaveResponse> SaveLabTATSetting(SaveLabTATSettingRequest request, AllGlobalValues globalValues)
        {
            try
            {
                string start = (request.TatStartPoint ?? "").Trim().ToLowerInvariant();
                string end = (request.TatEndPoint ?? "").Trim().ToLowerInvariant();
                if (!TatStartPoints.Contains(start)) return TatInvalid<LabTATSaveResponse>("TAT start must be order, collected or received");
                if (!TatEndPoints.Contains(end)) return TatInvalid<LabTATSaveResponse>("TAT end must be entered, verified or dispatched");
                return TatWrite("IU_LabTATSetting", new
                {
                    hospId = globalValues.hospId,
                    tatStartPoint = start,
                    tatEndPoint = end,
                    atRiskPercent = request.AtRiskPercent,
                    onTimeTargetPercent = request.OnTimeTargetPercent,
                    userId = globalValues.userId,
                    IpAddress = globalValues.ipAddress
                }, false, globalValues.hospId);
            }
            catch (Exception ex)
            {
                return TatServerError<LabTATSaveResponse>(ex);
            }
        }

        public ServiceResult<LabTATSaveResponse> SaveLabTATWorkingHours(SaveLabTATWorkingHoursRequest request, AllGlobalValues globalValues)
        {
            try
            {
                bool open = request.IsOpen == 1;
                if (open)
                {
                    bool toOk = request.ToTime == "24:00" || TatTimeRegex.IsMatch(request.ToTime ?? "");
                    if (!TatTimeRegex.IsMatch(request.FromTime ?? "") || !toOk) return TatInvalid<LabTATSaveResponse>("Working hours must be HH:mm");
                    if (TatHm(request.FromTime!) >= TatHm(request.ToTime!)) return TatInvalid<LabTATSaveResponse>("Closing time must be after opening time");
                }
                return TatWrite("IU_LabTATWorkingHours", new
                {
                    hospId = globalValues.hospId,
                    departmentId = request.DepartmentId,
                    dayOfWeekNo = request.DayOfWeekNo,
                    isOpen = open ? 1 : 0,
                    fromTime = open ? request.FromTime : null,
                    toTime = open ? request.ToTime : null,
                    userId = globalValues.userId,
                    IpAddress = globalValues.ipAddress
                }, false, globalValues.hospId);
            }
            catch (Exception ex)
            {
                return TatServerError<LabTATSaveResponse>(ex);
            }
        }

        public ServiceResult<LabTATSaveResponse> CreateUpdateLabTATHoliday(CreateUpdateLabTATHolidayRequest request, AllGlobalValues globalValues)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.HolidayName)) return TatInvalid<LabTATSaveResponse>("Date and name are required");
                return TatWrite("IU_LabTATHoliday", new
                {
                    hospId = globalValues.hospId,
                    holidayId = request.HolidayId,
                    holidayDate = request.HolidayDate!.Value.Date,
                    holidayName = request.HolidayName.Trim(),
                    departmentId = request.DepartmentId,
                    userId = globalValues.userId,
                    IpAddress = globalValues.ipAddress
                }, request.HolidayId == 0, globalValues.hospId, "A holiday on this date for this department already exists");
            }
            catch (Exception ex)
            {
                return TatServerError<LabTATSaveResponse>(ex);
            }
        }

        public ServiceResult<LabTATSaveResponse> UpdateLabTATHolidayStatus(UpdateLabTATHolidayStatusRequest request, AllGlobalValues globalValues)
        {
            try
            {
                return TatWrite("U_LabTATHolidayStatus", new
                {
                    hospId = globalValues.hospId,
                    holidayId = request.HolidayId,
                    userId = globalValues.userId,
                    IpAddress = globalValues.ipAddress
                }, false, globalValues.hospId);
            }
            catch (Exception ex)
            {
                return TatServerError<LabTATSaveResponse>(ex);
            }
        }

        public ServiceResult<LabTATSaveResponse> SaveLabTATDelayReason(SaveLabTATDelayReasonRequest request, AllGlobalValues globalValues)
        {
            try
            {
                return TatWrite("IU_LabTATDelayReason", new
                {
                    hospId = globalValues.hospId,
                    patientInvestigationId = request.PatientInvestigationId,
                    delayReasonId = request.DelayReasonId,
                    remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim(),
                    userId = globalValues.userId,
                    IpAddress = globalValues.ipAddress
                }, false, globalValues.hospId, clearCache: false);
            }
            catch (Exception ex)
            {
                return TatServerError<LabTATSaveResponse>(ex);
            }
        }

        #endregion

        #region Engine

        private LabTATConfigModel LoadTatConfig(int hospId)
        {
            string key = TatConfigCacheKey(hospId);
            var cached = _distributedCache.GetString(key);
            if (!string.IsNullOrEmpty(cached))
            {
                var fromCache = JsonSerializer.Deserialize<LabTATConfigModel>(cached);
                if (fromCache != null) return fromCache;
            }

            var ds = _sqlHelper.GetDataSet("S_GetLabTATConfig", CommandType.StoredProcedure, new { hospId });
            DataTable T(int i) => ds != null && ds.Tables.Count > i ? ds.Tables[i] : new DataTable();
            var cfg = new LabTATConfigModel();

            var s = T(0).AsEnumerable().FirstOrDefault();
            if (s != null)
                cfg.Setting = new LabTATSettingModel
                {
                    TatStartPoint = s.Field<string>("TatStartPoint") ?? "received",
                    TatEndPoint = s.Field<string>("TatEndPoint") ?? "verified",
                    AtRiskPercent = s.Field<int>("AtRiskPercent"),
                    OnTimeTargetPercent = s.Field<int>("OnTimeTargetPercent")
                };
            cfg.Priorities = T(1).AsEnumerable().Select(r => new LabTATPriorityModel
            {
                PriorityId = r.Field<int>("PriorityId"),
                PriorityName = r.Field<string>("PriorityName") ?? "",
                ClockType = r.Field<string>("ClockType") ?? "WORKING",
                ColorCode = r.Field<string>("ColorCode") ?? "",
                UrgentFlag = r.Field<int?>("UrgentFlag"),
                SequenceNo = r.Field<int>("SequenceNo")
            }).ToList();
            cfg.Shifts = T(2).AsEnumerable().Select(r => new LabTATShiftModel
            {
                ShiftId = r.Field<int>("ShiftId"),
                ShiftName = r.Field<string>("ShiftName") ?? "",
                FromTime = r.Field<string>("FromTime") ?? "",
                ToTime = r.Field<string>("ToTime") ?? "",
                SequenceNo = r.Field<int>("SequenceNo")
            }).ToList();
            var explicitRules = T(3).AsEnumerable().Select(r => new LabTATRuleModel
            {
                RuleId = r.Field<int>("RuleId"),
                RuleLevel = r.Field<string>("RuleLevel") ?? "ALL",
                InvestigationId = r.Field<int>("InvestigationId"),
                DepartmentId = r.Field<int>("DepartmentId"),
                PriorityId = r.Field<int>("PriorityId"),
                ShiftId = r.Field<int>("ShiftId"),
                TatMinutes = r.Field<int>("TatMinutes"),
                ClockType = r.Field<string>("ClockType") ?? "INHERIT",
                Note = r.Field<string>("Note") ?? ""
            }).ToList();
            cfg.WorkingHours = T(4).AsEnumerable().Select(r => new LabTATWorkingHoursModel
            {
                DepartmentId = r.Field<int>("DepartmentId"),
                DayOfWeekNo = r.Field<int>("DayOfWeekNo"),
                IsOpen = r.Field<int>("IsOpen"),
                FromTime = r.Field<string>("FromTime") ?? "",
                ToTime = r.Field<string>("ToTime") ?? ""
            }).ToList();
            cfg.DelayReasons = T(6).AsEnumerable().Select(r => new LabTATDelayReasonModel
            {
                DelayReasonId = r.Field<int>("DelayReasonId"),
                ReasonName = r.Field<string>("ReasonName") ?? ""
            }).ToList();
            cfg.Departments = T(7).AsEnumerable().Select(r => new LabTATDepartmentModel
            {
                DepartmentId = r.Field<int>("DepartmentId"),
                DepartmentName = r.Field<string>("DepartmentName") ?? ""
            }).ToList();
            var deptName = cfg.Departments.ToDictionary(d => d.DepartmentId, d => d.DepartmentName);
            cfg.Holidays = T(5).AsEnumerable().Select(r => new LabTATHolidayModel
            {
                HolidayId = r.Field<int>("HolidayId"),
                HolidayDate = r.Field<DateTime>("HolidayDate").ToString("yyyy-MM-dd"),
                HolidayName = r.Field<string>("HolidayName") ?? "",
                DepartmentId = r.Field<int>("DepartmentId"),
            }).ToList();
            foreach (var h in cfg.Holidays) h.DepartmentName = h.DepartmentId == 0 ? "All" : deptName.GetValueOrDefault(h.DepartmentId, $"Department {h.DepartmentId}");
            cfg.Tests = T(8).AsEnumerable().Select(r => new LabTATTestModel
            {
                InvestigationId = r.Field<int>("InvestigationId"),
                InvestigationName = r.Field<string>("InvestigationName") ?? "",
                InvestigationCode = r.Field<string>("InvestigationCode") ?? "",
                DepartmentId = r.Field<int>("DepartmentId"),
                MasterTatMinutes = r.Field<int>("MasterTatMinutes")
            }).ToList();
            foreach (var t in cfg.Tests) t.DepartmentName = deptName.GetValueOrDefault(t.DepartmentId, "");

            // Investigation Master TAT (ServiceItemMaster.tatInMin) acts as a test-level rule (any priority, any shift);
            // listed first so an explicit rule with the same specificity wins.
            var masterRules = cfg.Tests.Where(t => t.MasterTatMinutes > 0).Select(t => new LabTATRuleModel
            {
                RuleId = -t.InvestigationId,
                RuleLevel = "TEST",
                InvestigationId = t.InvestigationId,
                DepartmentId = t.DepartmentId,
                TatMinutes = t.MasterTatMinutes,
                ClockType = "INHERIT",
                Note = "Investigation Master TAT",
                IsMasterRule = true
            });
            cfg.Rules = masterRules.Concat(explicitRules).ToList();

            var testById = cfg.Tests.ToDictionary(t => t.InvestigationId);
            foreach (var r in cfg.Rules)
            {
                r.LevelText = (r.RuleLevel == "TEST" ? "Test" : r.RuleLevel == "DEPT" ? "Department" : "Default")
                              + (r.PriorityId != 0 ? " + Priority" : "") + (r.ShiftId != 0 ? " + Shift" : "");
                r.AppliesTo = r.RuleLevel == "ALL" ? "All tests"
                    : r.RuleLevel == "TEST" ? (testById.TryGetValue(r.InvestigationId, out var t) ? $"{t.InvestigationName}{(string.IsNullOrEmpty(t.InvestigationCode) ? "" : $" ({t.InvestigationCode})")}" : $"Test {r.InvestigationId}")
                    : deptName.GetValueOrDefault(r.DepartmentId, $"Department {r.DepartmentId}");
                r.PriorityName = r.PriorityId == 0 ? "Any" : cfg.Priorities.FirstOrDefault(p => p.PriorityId == r.PriorityId)?.PriorityName ?? "-";
                r.ShiftName = r.ShiftId == 0 ? "Any" : cfg.Shifts.FirstOrDefault(x => x.ShiftId == r.ShiftId)?.ShiftName ?? "-";
                r.IsDefault = r.RuleLevel == "ALL" && r.PriorityId != 0 && r.ShiftId == 0;
            }

            var startText = new Dictionary<string, string> { ["order"] = "Order", ["collected"] = "Collection", ["received"] = "Lab receipt" };
            var endText = new Dictionary<string, string> { ["entered"] = "Result entry", ["verified"] = "Verification", ["dispatched"] = "Report dispatch" };
            cfg.BasisText = $"TAT = {startText.GetValueOrDefault(cfg.Setting.TatStartPoint, cfg.Setting.TatStartPoint)} → {endText.GetValueOrDefault(cfg.Setting.TatEndPoint, cfg.Setting.TatEndPoint)} · Target {cfg.Setting.OnTimeTargetPercent}% on time";

            if (cfg.Priorities.Count > 0)
                _distributedCache.SetString(key, JsonSerializer.Serialize(cfg), new DistributedCacheEntryOptions());
            return cfg;
        }

        private List<TatSampleRow> LoadTatSamples(int hospId, DateTime fromDate)
        {
            var dt = _sqlHelper.GetDataTable("S_GetLabTATSamples", CommandType.StoredProcedure, new { hospId, fromDate });
            return dt?.AsEnumerable().Select(r => new TatSampleRow
            {
                PatientInvestigationId = r.Field<int>("PatientInvestigationId"),
                LabNo = r.Field<int>("LabNo"),
                BarCode = r.Field<string>("BarCode") ?? "",
                InvestigationId = r.Field<int>("InvestigationId"),
                DepartmentId = r.Field<int>("DepartmentId"),
                IsUrgent = r.Field<int>("IsUrgent"),
                VisitType = r.Field<string>("VisitType") ?? "",
                UHID = r.Field<string>("UHID") ?? "",
                PatientName = r.Field<string>("PatientName") ?? "",
                OrderedOn = r.Field<DateTime?>("OrderedOn"),
                CollectedOn = r.Field<DateTime?>("CollectedOn"),
                ReceivedOn = r.Field<DateTime?>("ReceivedOn"),
                EnteredOn = r.Field<DateTime?>("EnteredOn"),
                VerifiedOn = r.Field<DateTime?>("VerifiedOn"),
                DispatchedOn = r.Field<DateTime?>("DispatchedOn"),
                DelayReasonId = r.Field<int>("DelayReasonId"),
                DelayReason = r.Field<string>("DelayReason") ?? ""
            }).ToList() ?? new List<TatSampleRow>();
        }

        private List<TatCalc> CalcTat(List<TatSampleRow> rows, LabTATConfigModel cfg, DateTime now)
        {
            var tests = cfg.Tests.ToDictionary(t => t.InvestigationId);
            var byFlag = cfg.Priorities.Where(p => p.UrgentFlag.HasValue).GroupBy(p => p.UrgentFlag!.Value).ToDictionary(g => g.Key, g => g.First());
            var defaultPrio = cfg.Priorities.FirstOrDefault();
            var list = new List<TatCalc>(rows.Count);

            foreach (var s in rows)
            {
                DateTime? start = cfg.Setting.TatStartPoint switch { "order" => s.OrderedOn, "collected" => s.CollectedOn, _ => s.ReceivedOn };
                DateTime? end = cfg.Setting.TatEndPoint switch { "entered" => s.EnteredOn, "dispatched" => s.DispatchedOn, _ => s.VerifiedOn };
                if (start == null) continue;                     // TAT clock has not started for this sample
                if (end != null && end < start) end = start;

                var c = new TatCalc { S = s, Start = start.Value, End = end };
                tests.TryGetValue(s.InvestigationId, out c.Test);
                c.Priority = byFlag.TryGetValue(s.IsUrgent, out var p) ? p : defaultPrio;
                c.Shift = ShiftOf(c.Start, cfg);
                c.Rule = MatchTatRule(cfg, s.InvestigationId, s.DepartmentId, c.Priority?.PriorityId ?? 0, c.Shift?.ShiftId ?? 0);
                c.Clock = ClockOf(c.Rule, c.Priority);
                c.Due = DueOf(c.Start, c.Rule?.TatMinutes ?? TatFallbackMinutes, s.DepartmentId, c.Clock, cfg, null);
                c.Target = (c.Due - c.Start).TotalMinutes;
                if (end != null)
                {
                    c.Tat = (end.Value - c.Start).TotalMinutes;
                    c.Status = end.Value <= c.Due ? "On time" : "Late";
                    c.Delay = (end.Value - c.Due).TotalMinutes;
                }
                else
                {
                    double left = (c.Due - now).TotalMinutes, tot = Math.Max(1, c.Target);
                    c.Status = left < 0 ? "Breached" : left * 100 / tot < cfg.Setting.AtRiskPercent ? "At risk" : "On track";
                    c.Delay = -left;
                }
                list.Add(c);
            }
            return list;
        }

        private static LabTATShiftModel? ShiftOf(DateTime t, LabTATConfigModel cfg)
        {
            int m = t.Hour * 60 + t.Minute;
            foreach (var s in cfg.Shifts)
            {
                int a = TatHm(s.FromTime), b = TatHm(s.ToTime);
                if (a < b ? (m >= a && m < b) : (m >= a || m < b)) return s;
            }
            return cfg.Shifts.FirstOrDefault();
        }

        private static int TatScore(LabTATRuleModel r) =>
            (r.RuleLevel == "TEST" ? 40 : r.RuleLevel == "DEPT" ? 20 : 0) + (r.PriorityId != 0 ? 4 : 0) + (r.ShiftId != 0 ? 2 : 0);

        private static List<LabTATRuleModel> TatCandidates(LabTATConfigModel cfg, int investigationId, int departmentId, int priorityId, int shiftId) =>
            cfg.Rules.Where(r => (r.RuleLevel == "ALL"
                                  || (r.RuleLevel == "TEST" && r.InvestigationId == investigationId)
                                  || (r.RuleLevel == "DEPT" && r.DepartmentId == departmentId))
                                 && (r.PriorityId == 0 || r.PriorityId == priorityId)
                                 && (r.ShiftId == 0 || r.ShiftId == shiftId)).ToList();

        private static LabTATRuleModel? MatchTatRule(LabTATConfigModel cfg, int investigationId, int departmentId, int priorityId, int shiftId)
        {
            LabTATRuleModel? best = null;
            int bs = -1;
            foreach (var r in TatCandidates(cfg, investigationId, departmentId, priorityId, shiftId))
            {
                int sc = TatScore(r);
                if (sc >= bs) { bs = sc; best = r; }               // later (explicit / newer) rule wins a tie
            }
            return best;
        }

        private static string ClockOf(LabTATRuleModel? rule, LabTATPriorityModel? prio) =>
            rule != null && rule.ClockType != "INHERIT" ? rule.ClockType : prio?.ClockType ?? "WORKING";

        /// <summary>Open window of a department on one day, or null when closed / holiday. No row = open 24 h.</summary>
        private static (DateTime From, DateTime To)? TatWindow(DateTime day, int departmentId, LabTATConfigModel cfg)
        {
            string ymd = day.ToString("yyyy-MM-dd");
            if (cfg.Holidays.Any(h => h.HolidayDate == ymd && (h.DepartmentId == 0 || h.DepartmentId == departmentId))) return null;
            var w = cfg.WorkingHours.FirstOrDefault(h => h.DepartmentId == departmentId && h.DayOfWeekNo == (int)day.DayOfWeek);
            if (w == null) return (day, day.AddDays(1));
            if (w.IsOpen != 1 || string.IsNullOrEmpty(w.FromTime) || string.IsNullOrEmpty(w.ToTime)) return null;
            return (day.AddMinutes(TatHm(w.FromTime)), day.AddMinutes(TatHm(w.ToTime)));
        }

        private static DateTime DueOf(DateTime start, int minutes, int departmentId, string clock, LabTATConfigModel cfg, List<string>? trace)
        {
            if (clock == "24X7")
            {
                trace?.Add($"24×7 clock: {TatDur(minutes)} added straight");
                return start.AddMinutes(minutes);
            }

            DateTime cur = start, day = start.Date;
            double rem = minutes;
            for (int i = 0; i < TatCalendarMaxDays; i++)
            {
                var w = TatWindow(day, departmentId, cfg);
                if (w != null)
                {
                    var (wf, wt) = w.Value;
                    if (cur < wf) { if (i < 8) trace?.Add($"Lab closed until {wf:dd-MM HH:mm} — clock paused"); cur = wf; }
                    if (cur < wt)
                    {
                        double av = (wt - cur).TotalMinutes;
                        if (rem <= av)
                        {
                            trace?.Add($"From {cur:dd-MM HH:mm} {TatDur(rem)} more → {cur.AddMinutes(rem):dd-MM HH:mm}");
                            return cur.AddMinutes(rem);
                        }
                        rem -= av;
                        if (i < 8) trace?.Add($"{cur:dd-MM HH:mm} – {wt:dd-MM HH:mm}: {TatDur(av)} counted, {TatDur(rem)} left");
                    }
                }
                else if (i < 8)
                {
                    string ymd = day.ToString("yyyy-MM-dd");
                    trace?.Add($"{day:dd-MM-yyyy}: {(cfg.Holidays.Any(h => h.HolidayDate == ymd) ? "holiday" : "lab closed")} — skipped");
                }
                day = day.AddDays(1);
                if (cur < day) cur = day;
            }
            return cur.AddMinutes(rem);
        }

        private static LabTATStatModel TatStats(IEnumerable<TatCalc> source, LabTATConfigModel cfg)
        {
            var rs = source as IList<TatCalc> ?? source.ToList();
            var done = rs.Where(r => r.End != null).ToList();
            int on = done.Count(r => r.Status == "On time");
            var tats = done.Select(r => r.Tat!.Value).OrderBy(x => x).ToList();
            double? Q(double p) => tats.Count == 0 ? null : Math.Round(tats[Math.Min(tats.Count - 1, (int)Math.Floor(p * tats.Count))]);
            return new LabTATStatModel
            {
                Samples = rs.Count,
                Reported = done.Count,
                OnTime = on,
                Late = done.Count - on,
                OnTimePercent = done.Count == 0 ? null : Math.Round(on * 100.0 / done.Count, 1),
                AvgTatMinutes = tats.Count == 0 ? null : Math.Round(tats.Average()),
                MedianTatMinutes = Q(0.5),
                P90TatMinutes = Q(0.9),
                AvgTargetMinutes = done.Count == 0 ? null : Math.Round(done.Average(r => r.Target)),
                Pending = rs.Count - done.Count,
                Breached = rs.Count(r => r.Status == "Breached"),
                AtRisk = rs.Count(r => r.Status == "At risk")
            };
        }

        private static LabTATGroupStatModel Group(string key, string label, IEnumerable<TatCalc> rows, LabTATConfigModel cfg) =>
            new() { Key = key, Label = label, Stats = TatStats(rows, cfg) };

        private static LabTATMatrixModel Matrix(List<TatCalc> f, LabTATConfigModel cfg,
            List<(string Key, string Label)> rows, Func<TatCalc, string> rowKey,
            List<(string Key, string Label)> cols, Func<TatCalc, string> colKey)
        {
            var m = new LabTATMatrixModel
            {
                Cols = cols.Select(c => new LabTATGroupStatModel { Key = c.Key, Label = c.Label }).ToList()
            };
            foreach (var r in rows)
            {
                var inRow = f.Where(x => rowKey(x) == r.Key).ToList();
                m.Rows.Add(Group(r.Key, r.Label, inRow, cfg));
                foreach (var c in cols)
                    m.Cells.Add(new LabTATMatrixCellModel { RowKey = r.Key, ColKey = c.Key, Stats = TatStats(inRow.Where(x => colKey(x) == c.Key), cfg) });
            }
            return m;
        }

        private static LabTATSampleModel ToSampleModel(TatCalc c, DateTime now)
        {
            var r = c.Rule;
            string ruleText = r == null ? "default"
                : (r.RuleLevel == "ALL" ? "Default" : r.AppliesTo) + (r.PriorityId != 0 ? "/" + r.PriorityName : "") + (r.ShiftId != 0 ? "/" + r.ShiftName : "") + " " + TatDur(r.TatMinutes);
            return new LabTATSampleModel
            {
                PatientInvestigationId = c.S.PatientInvestigationId,
                LabNo = c.S.LabNo,
                BarCode = c.S.BarCode,
                UHID = c.S.UHID,
                PatientName = c.S.PatientName,
                VisitType = c.S.VisitType,
                InvestigationId = c.S.InvestigationId,
                InvestigationName = c.Test?.InvestigationName ?? "",
                DepartmentId = c.S.DepartmentId,
                DepartmentName = c.Test?.DepartmentName ?? "",
                PriorityId = c.Priority?.PriorityId ?? 0,
                PriorityName = c.Priority?.PriorityName ?? "",
                ShiftName = c.Shift?.ShiftName ?? "",
                StartOn = c.Start.ToString("dd-MM-yyyy HH:mm"),
                DueOn = c.Due.ToString("dd-MM-yyyy HH:mm"),
                EndOn = c.End?.ToString("dd-MM-yyyy HH:mm") ?? "",
                TargetMinutes = Math.Round(c.Target),
                TatMinutes = c.Tat.HasValue ? Math.Round(c.Tat.Value) : null,
                DelayMinutes = Math.Round(c.Delay),
                UsedPercent = Math.Round(Math.Max(0, Math.Min(100, ((c.End ?? now) - c.Start).TotalMinutes * 100 / Math.Max(1, c.Target)))),
                Status = c.Status,
                RuleText = ruleText,
                ClockType = TatClockText(c.Clock),
                DelayReasonId = c.S.DelayReasonId,
                DelayReason = c.S.DelayReason
            };
        }

        private ServiceResult<LabTATSaveResponse> TatWrite(string sp, object parameters, bool isNew, int hospId,
            string? duplicateText = null, Dictionary<int, string>? rejections = null, bool clearCache = true)
        {
            var raw = _sqlHelper.DML(sp, CommandType.StoredProcedure, parameters, new { Result = 0 });
            int result = Convert.ToInt32(raw);
            if (clearCache) _distributedCache.Remove(TatConfigCacheKey(hospId));

            if (result > 0)
            {
                var ok = _messageService.GetMessageAndTypeByAlertCode(isNew ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<LabTATSaveResponse>.Success(new LabTATSaveResponse { Id = result }, ok.Type, ok.Message, isNew ? 201 : 200);
            }
            if (result == -1 && duplicateText != null)
            {
                var dup = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                return ServiceResult<LabTATSaveResponse>.Failure(dup.Type, duplicateText, 409);
            }
            if (rejections != null && rejections.TryGetValue(result, out var text))
                return TatInvalid<LabTATSaveResponse>(text);
            if (result == 0)
            {
                var nf = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                return ServiceResult<LabTATSaveResponse>.Failure(nf.Type, nf.Message, 404);
            }
            _log.Error($"{sp} returned unexpected result {result}");
            var fail = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
            return ServiceResult<LabTATSaveResponse>.Failure(fail.Type, fail.Message, 500);
        }

        private static int TatPeriodDays(string? period) => (period ?? "").Trim().ToLowerInvariant() switch
        {
            "today" => 0,
            "7d" => 6,
            _ => 30
        };

        private static int TatHm(string s)
        {
            var p = (s ?? "").Split(':');
            int h = p.Length > 0 && int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var a) ? a : 0;
            int m = p.Length > 1 && int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var b) ? b : 0;
            return h * 60 + m;
        }

        private static string TatClockText(string clock) => clock == "24X7" ? "24x7" : "Working calendar";

        private static string TatDur(double minutes)
        {
            int m = (int)Math.Round(Math.Abs(minutes));
            int d = m / 1440, h = m % 1440 / 60, mm = m % 60;
            string s = ((d > 0 ? d + "d " : "") + (h > 0 ? h + "h " : "") + (d == 0 && mm > 0 ? mm + "m" : "")).Trim();
            return (minutes < 0 ? "-" : "") + (s.Length == 0 ? "0m" : s);
        }

        private ServiceResult<T> TatInvalid<T>(string message)
        {
            var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
            return ServiceResult<T>.Failure(alert.Type, message, 200);
        }

        private ServiceResult<T> TatServerError<T>(Exception ex, [System.Runtime.CompilerServices.CallerMemberName] string method = "")
        {
            LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{method}");
            var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
            return ServiceResult<T>.Failure(alert.Type, alert.Message, 500);
        }

        #endregion
    }
}
