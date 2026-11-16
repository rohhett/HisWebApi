using System;
using System.Collections.Generic;
using System.Linq;
using HISWEBAPI.DTO;
using HISWEBAPI.Models;
using Microsoft.Extensions.Caching.Distributed;

namespace HISWEBAPI.Repositories.Implementations
{
    /// <summary>
    /// Stock Receiving: unsaved GRN entries kept in Redis only (owner 2026-11-16) - "Previous Records" on the GRN page.
    /// The screen autosaves the GRN being entered; if the session expires, the connection drops or the tab closes,
    /// the user resumes it from the list. One Redis key per user: _GrnUnsaved_{hospId}_{userId} (userId from the JWT,
    /// so a user never sees another user's records). Kept 7 days after the last change, at most 20 records per user.
    /// </summary>
    public partial class StoreRepository
    {
        private const int GrnUnsavedMaxPerUser = 20;
        private static readonly TimeSpan GrnUnsavedKeepFor = TimeSpan.FromDays(7);
        private static readonly object GrnUnsavedLock = new();

        private static string GrnUnsavedKey(AllGlobalValues g) => $"_GrnUnsaved_{g.hospId}_{g.userId}";

        private List<GrnUnsavedDraftModel> ReadGrnUnsaved(AllGlobalValues g)
        {
            string? json = _distributedCache.GetString(GrnUnsavedKey(g));
            if (string.IsNullOrEmpty(json)) return new List<GrnUnsavedDraftModel>();
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<GrnUnsavedDraftModel>>(json) ?? new List<GrnUnsavedDraftModel>();
            }
            catch (System.Text.Json.JsonException)
            {
                _log.Warn($"Unreadable unsaved-GRN cache for user {g.userId} - starting empty");
                return new List<GrnUnsavedDraftModel>();
            }
        }

        private void WriteGrnUnsaved(AllGlobalValues g, List<GrnUnsavedDraftModel> list)
        {
            if (list.Count == 0)
            {
                _distributedCache.Remove(GrnUnsavedKey(g));
                return;
            }
            _distributedCache.SetString(GrnUnsavedKey(g), System.Text.Json.JsonSerializer.Serialize(list),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = GrnUnsavedKeepFor });
        }

        public ServiceResult<GrnUnsavedDraftModel> SaveGrnUnsavedDraft(GrnUnsavedDraftSaveRequest request, AllGlobalValues globalValues)
        {
            try
            {
                string id = (request.DraftId ?? string.Empty).Trim();
                if (id != "" && !Guid.TryParseExact(id, "N", out _))
                    return Invalid<GrnUnsavedDraftModel>("Invalid record id");

                GrnUnsavedDraftModel draft;
                lock (GrnUnsavedLock)
                {
                    var list = ReadGrnUnsaved(globalValues);
                    var now = DateTime.Now;
                    draft = list.FirstOrDefault(d => d.DraftId == id) ?? new GrnUnsavedDraftModel
                    {
                        DraftId = Guid.NewGuid().ToString("N"),
                        CreatedOn = now
                    };
                    draft.BranchId = request.BranchId;
                    draft.VendorName = (request.VendorName ?? string.Empty).Trim();
                    draft.DocNo = (request.DocNo ?? string.Empty).Trim();
                    draft.ItemCount = request.ItemCount;
                    draft.ModifiedOn = now;
                    draft.Data = request.Data;

                    list.RemoveAll(d => d.DraftId == draft.DraftId);
                    list.Insert(0, draft);
                    if (list.Count > GrnUnsavedMaxPerUser) list = list.Take(GrnUnsavedMaxPerUser).ToList();
                    WriteGrnUnsaved(globalValues, list);
                }

                var reply = new GrnUnsavedDraftModel
                {
                    DraftId = draft.DraftId, BranchId = draft.BranchId, VendorName = draft.VendorName, DocNo = draft.DocNo,
                    ItemCount = draft.ItemCount, CreatedOn = draft.CreatedOn, ModifiedOn = draft.ModifiedOn
                };
                return ServiceResult<GrnUnsavedDraftModel>.Success(reply, "Info", "Unsaved GRN kept", 200);
            }
            catch (Exception ex)
            {
                return ServerError<GrnUnsavedDraftModel>(ex);
            }
        }

        public ServiceResult<IEnumerable<GrnUnsavedDraftModel>> GetGrnUnsavedDrafts(int branchId, AllGlobalValues globalValues)
        {
            try
            {
                var list = ReadGrnUnsaved(globalValues)
                    .Where(d => d.BranchId == branchId)
                    .OrderByDescending(d => d.ModifiedOn)
                    .ToList();
                return ServiceResult<IEnumerable<GrnUnsavedDraftModel>>.Success(list, "Info", $"{list.Count} unsaved GRN(s)", 200);
            }
            catch (Exception ex)
            {
                return ServerError<IEnumerable<GrnUnsavedDraftModel>>(ex);
            }
        }

        public ServiceResult<bool> DeleteGrnUnsavedDraft(string draftId, AllGlobalValues globalValues)
        {
            try
            {
                int removed;
                lock (GrnUnsavedLock)
                {
                    var list = ReadGrnUnsaved(globalValues);
                    removed = list.RemoveAll(d => d.DraftId == draftId.Trim());
                    if (removed > 0) WriteGrnUnsaved(globalValues, list);
                }
                return ServiceResult<bool>.Success(removed > 0, "Info", removed > 0 ? "Unsaved GRN deleted" : "Already removed", 200);
            }
            catch (Exception ex)
            {
                return ServerError<bool>(ex);
            }
        }
    }
}
