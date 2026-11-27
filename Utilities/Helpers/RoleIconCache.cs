using HISWEBAPI.Data.Helpers;
using HISWEBAPI.Models;
using Microsoft.Extensions.Caching.Distributed;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;

namespace HISWEBAPI.Utilities.Helpers
{
    /// <summary>
    /// Role icons (dbo.RoleIconMaster, PNG as base64) shared by the role list and the user's roles.
    /// The whole list is cached in Redis without expiry; the 27 icons are about 60 KB.
    /// (owner 2026-11-26: role icons come from the database as base64, not from files)
    /// </summary>
    public static class RoleIconCache
    {
        public const string CacheKey = "_RoleIconMaster_All";

        public static List<RoleIconModel> GetAll(IDistributedCache cache, ICustomSqlHelper sqlHelper)
        {
            var cached = cache.GetString(CacheKey);
            if (!string.IsNullOrEmpty(cached))
                return JsonSerializer.Deserialize<List<RoleIconModel>>(cached) ?? new List<RoleIconModel>();

            var dataTable = sqlHelper.GetDataTable("S_GetRoleIconList", CommandType.StoredProcedure);
            var icons = dataTable?.AsEnumerable().Select(row => new RoleIconModel
            {
                RoleIconId = row.Field<int>("RoleIconId"),
                IconName = row.Field<string>("IconName"),
                IsActive = row.Field<int>("IsActive"),
                RoleIcon = $"data:{row.Field<string>("MimeType")};base64,{row.Field<string>("IconBase64")}",
            }).ToList() ?? new List<RoleIconModel>();

            if (icons.Any())
                cache.SetString(CacheKey, JsonSerializer.Serialize(icons), new DistributedCacheEntryOptions());

            return icons;
        }

        /// <summary>RoleIconId -> data URL ("data:image/png;base64,...").</summary>
        public static Dictionary<int, string> GetMap(IDistributedCache cache, ICustomSqlHelper sqlHelper) =>
            GetAll(cache, sqlHelper).ToDictionary(i => i.RoleIconId, i => i.RoleIcon ?? string.Empty);
    }
}
