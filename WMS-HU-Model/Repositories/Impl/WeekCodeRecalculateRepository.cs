using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Newtonsoft.Json;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Models.WeekCode;

namespace WMS_HU_Model.Repositories
{
    public class WeekCodeRecalculateRepository : IWeekCodeRecalculateRepository
    {
        public const int HoldReleaseRequestIdMaxLength = 100;

        public const string CurrentBoxesSql = @"
SELECT b.Id AS BoxHUId,
       b.HUCode AS BoxCode,
       b.IsHold,
       i.Id AS ItemId,
       i.PackCreatedTime,
       i.DateCode,
       i.WeekRuleSource AS OldRuleSource,
       i.WeekRuleValue AS OldRuleValue,
       i.WeekFormatCode AS OldWeekFormatCode,
       i.WeekYear AS OldWeekYear,
       i.WeekNo AS OldWeekNo,
       i.WeekSortKey AS OldWeekSortKey
FROM dbo.WMS_FG_HU b
LEFT JOIN dbo.WMS_FG_HU_CurrentLocation dc ON dc.HUId = b.Id
LEFT JOIN dbo.WMS_FG_Location dl ON dl.Id = dc.LocationId
LEFT JOIN dbo.WMS_FG_HU_Relation pr ON pr.ChildHUId = b.Id AND pr.IsActive = 1
LEFT JOIN dbo.WMS_FG_HU ph ON ph.Id = pr.ParentHUId AND ph.LifecycleStatus = 'ACTIVE'
LEFT JOIN dbo.WMS_FG_HU_CurrentLocation pc ON pc.HUId = ph.Id
LEFT JOIN dbo.WMS_FG_Location pl ON pl.Id = pc.LocationId
INNER JOIN dbo.WMS_FG_HU_Relation r ON r.ParentHUId = b.Id AND r.IsActive = 1
INNER JOIN dbo.WMS_FG_HU p ON p.Id = r.ChildHUId
    AND p.HUType = 'INNER_PACK'
    AND p.LifecycleStatus = 'ACTIVE'
INNER JOIN dbo.WMS_FG_HU_Item i ON i.HUId = p.Id AND i.IsActive = 1
WHERE b.HUType = 'OUTER_BOX'
  AND b.PlantCode = @PlantCode
  AND b.LifecycleStatus = 'ACTIVE'
  AND i.ProdNo = @MaterialCode
  AND ISNULL(i.ProdRev, '') = ISNULL(@MaterialRevision, '')
  AND COALESCE(dl.Id, pl.Id) IS NOT NULL
  AND COALESCE(dl.LocationCode, pl.LocationCode) <> 'SHIPPED'
ORDER BY b.Id, i.Id;";

        public const string RevalidateBoxSql = @"
SELECT i.Id
FROM dbo.WMS_FG_HU b WITH (UPDLOCK, HOLDLOCK)
LEFT JOIN dbo.WMS_FG_HU_CurrentLocation dc WITH (UPDLOCK, HOLDLOCK) ON dc.HUId = b.Id
LEFT JOIN dbo.WMS_FG_Location dl ON dl.Id = dc.LocationId
LEFT JOIN dbo.WMS_FG_HU_Relation pr WITH (UPDLOCK, HOLDLOCK) ON pr.ChildHUId = b.Id AND pr.IsActive = 1
LEFT JOIN dbo.WMS_FG_HU ph WITH (UPDLOCK, HOLDLOCK) ON ph.Id = pr.ParentHUId AND ph.LifecycleStatus = 'ACTIVE'
LEFT JOIN dbo.WMS_FG_HU_CurrentLocation pc WITH (UPDLOCK, HOLDLOCK) ON pc.HUId = ph.Id
LEFT JOIN dbo.WMS_FG_Location pl ON pl.Id = pc.LocationId
INNER JOIN dbo.WMS_FG_HU_Relation r WITH (UPDLOCK, HOLDLOCK) ON r.ParentHUId = b.Id AND r.IsActive = 1
INNER JOIN dbo.WMS_FG_HU p WITH (UPDLOCK, HOLDLOCK) ON p.Id = r.ChildHUId
    AND p.HUType = 'INNER_PACK'
    AND p.LifecycleStatus = 'ACTIVE'
INNER JOIN dbo.WMS_FG_HU_Item i WITH (UPDLOCK, HOLDLOCK) ON i.HUId = p.Id AND i.IsActive = 1
WHERE b.Id = @BoxHUId
  AND b.HUType = 'OUTER_BOX'
  AND b.PlantCode = @PlantCode
  AND b.LifecycleStatus = 'ACTIVE'
  AND i.ProdNo = @MaterialCode
  AND ISNULL(i.ProdRev, '') = ISNULL(@MaterialRevision, '')
  AND COALESCE(dl.Id, pl.Id) IS NOT NULL
  AND COALESCE(dl.LocationCode, pl.LocationCode) <> 'SHIPPED';";

        private readonly string _connectionString;

        public WeekCodeRecalculateRepository()
            : this(ConfigurationManager.ConnectionStrings["BaseDb"] == null
                ? null
                : ConfigurationManager.ConnectionStrings["BaseDb"].ConnectionString)
        {
        }

        public WeekCodeRecalculateRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public List<WeekCodeRecalculateBoxFact> GetCurrentBoxes(string plant, string material, string revision)
        {
            using (SqlConnection connection = Open())
            {
                List<CurrentItemRow> rows = connection.Query<CurrentItemRow>(CurrentBoxesSql, new
                {
                    PlantCode = plant,
                    MaterialCode = material,
                    MaterialRevision = revision
                }).ToList();

                return rows.GroupBy(x => new { x.BoxHUId, x.BoxCode })
                    .Select(group => new WeekCodeRecalculateBoxFact
                    {
                        BoxHUId = group.Key.BoxHUId,
                        BoxCode = group.Key.BoxCode,
                        IsHold = group.First().IsHold,
                        Items = group.Select(x => new WeekCodeRecalculateItemFact
                        {
                            ItemId = x.ItemId,
                            PackCreatedTime = x.PackCreatedTime,
                            DateCode = x.DateCode,
                            OldRuleSource = x.OldRuleSource,
                            OldRuleValue = x.OldRuleValue,
                            OldWeekFormatCode = x.OldWeekFormatCode,
                            OldWeekYear = x.OldWeekYear,
                            OldWeekNo = x.OldWeekNo,
                            OldWeekSortKey = x.OldWeekSortKey
                        }).ToList()
                    }).ToList();
            }
        }

        public WeekCodeRecalculateBoxResultDto ApplyBox(WeekCodeRecalculateBoxPlan plan, WeekCodeRecalculateRequest request)
        {
            using (SqlConnection connection = Open())
            using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
            {
                try
                {
                    List<long> currentItemIds = connection.Query<long>(RevalidateBoxSql, new
                    {
                        plan.BoxHUId,
                        request.PlantCode,
                        request.MaterialCode,
                        request.MaterialRevision
                    }, transaction).ToList();

                    if (currentItemIds.Count != plan.Items.Count || plan.Items.Any(x => !currentItemIds.Contains(x.ItemId)))
                    {
                        throw new InvalidOperationException("Box库存事实已变化，请重新查询后再试。");
                    }

                    DateTime now = DateTime.Now;
                    foreach (WeekCodeRecalculateItemPlan item in plan.Items)
                    {
                        int affected = connection.Execute(@"
UPDATE dbo.WMS_FG_HU_Item
SET WeekRuleSource = @WeekRuleSource,
    WeekRuleValue = @WeekRuleValue,
    WeekFormatCode = @WeekFormatCode,
    WeekYear = @WeekYear,
    WeekNo = @WeekNo,
    WeekSortKey = @WeekSortKey,
    WeekResolvedTime = @Now,
    LastEditBy = @Operator,
    LastEditDt = @Now
WHERE Id = @ItemId AND IsActive = 1;", new
                        {
                            item.ItemId,
                            item.WeekCode.WeekRuleSource,
                            item.WeekCode.WeekRuleValue,
                            item.WeekCode.WeekFormatCode,
                            item.WeekCode.WeekYear,
                            item.WeekCode.WeekNo,
                            item.WeekCode.WeekSortKey,
                            Now = now,
                            Operator = request.Operator
                        }, transaction);

                        if (affected != 1)
                        {
                            throw new InvalidOperationException("WeekCode Item并发更新失败。");
                        }
                    }

                    ActiveHoldRow activeHold = connection.Query<ActiveHoldRow>(@"
SELECT Id, HoldReasonCode
FROM dbo.WMS_FG_HU_HoldLog WITH (UPDLOCK, HOLDLOCK)
WHERE HUId = @BoxHUId AND IsActive = 1;", new { plan.BoxHUId }, transaction).SingleOrDefault();

                    bool releasedHold = false;
                    if (activeHold != null && string.Equals(activeHold.HoldReasonCode, "WEEKCODE_UNRESOLVED", StringComparison.OrdinalIgnoreCase))
                    {
                        int unresolvedCount = connection.ExecuteScalar<int>(@"
SELECT COUNT(1)
FROM dbo.WMS_FG_HU_Relation r WITH (UPDLOCK, HOLDLOCK)
INNER JOIN dbo.WMS_FG_HU p WITH (UPDLOCK, HOLDLOCK) ON p.Id = r.ChildHUId
    AND p.HUType = 'INNER_PACK'
    AND p.LifecycleStatus = 'ACTIVE'
INNER JOIN dbo.WMS_FG_HU_Item i WITH (UPDLOCK, HOLDLOCK) ON i.HUId = p.Id AND i.IsActive = 1
WHERE r.ParentHUId = @BoxHUId
  AND r.IsActive = 1
  AND (i.WeekFormatCode IS NULL OR i.WeekYear IS NULL OR i.WeekNo IS NULL
       OR i.WeekSortKey IS NULL OR i.WeekResolvedTime IS NULL);", new { plan.BoxHUId }, transaction);

                        if (unresolvedCount == 0)
                        {
                            string holdReleaseRequestId = BuildHoldReleaseRequestId(request.RequestId, activeHold.Id);
                            connection.Execute(@"
UPDATE dbo.WMS_FG_HU_HoldLog
SET IsActive = 0,
    ReleaseDt = @Now,
    ReleaseBy = @Operator,
    ReleaseReason = N'解除冻结：周码重算后全箱解析完成',
    ReleaseRequestId = @HoldReleaseRequestId
WHERE Id = @HoldId AND IsActive = 1;

UPDATE dbo.WMS_FG_HU
SET IsHold = 0,
    LastEditBy = @Operator,
    LastEditDt = @Now
WHERE Id = @BoxHUId AND IsHold = 1;", new
                            {
                                Now = now,
                                Operator = request.Operator,
                                HoldReleaseRequestId = holdReleaseRequestId,
                                HoldId = activeHold.Id,
                                plan.BoxHUId
                            }, transaction);
                            releasedHold = true;
                        }
                    }

                    string detailJson = JsonConvert.SerializeObject(plan.Items.Select(x => new
                    {
                        x.ItemId,
                        x.DateCode,
                        OldWeekRuleSource = x.OldRuleSource,
                        OldWeekRuleValue = x.OldRuleValue,
                        OldWeekFormatCode = x.OldWeekFormatCode,
                        OldWeekYear = x.OldWeekYear,
                        OldWeekNo = x.OldWeekNo,
                        OldWeekSortKey = x.OldWeekSortKey,
                        NewWeekRuleSource = x.WeekCode.WeekRuleSource,
                        NewWeekRuleValue = x.WeekCode.WeekRuleValue,
                        NewWeekFormatCode = x.WeekCode.WeekFormatCode,
                        NewWeekYear = x.WeekCode.WeekYear,
                        NewWeekNo = x.WeekCode.WeekNo,
                        NewWeekSortKey = x.WeekCode.WeekSortKey
                    }));
                    HuOperationLogWriter.Insert(connection, transaction,
                        "WCR-" + Guid.NewGuid().ToString("N"), "WEEKCODE_RECALCULATE", plan.BoxHUId, null,
                        plan.BoxCode, request.RequestId, request.Operator, now, detailJson, null);

                    transaction.Commit();
                    return new WeekCodeRecalculateBoxResultDto
                    {
                        BoxHUId = plan.BoxHUId,
                        BoxCode = plan.BoxCode,
                        Result = "UPDATED",
                        BeforeIsHold = plan.BeforeIsHold,
                        AfterIsHold = releasedHold ? false : plan.BeforeIsHold,
                        UpdatedItemCount = plan.Items.Count,
                        ReleasedWeekCodeHold = releasedHold,
                        ItemResults = plan.Items.Select(x => new WeekCodeRecalculateItemResultDto
                        {
                            DateCode = x.DateCode,
                            OldWeekRuleSource = x.OldRuleSource,
                            OldWeekRuleValue = x.OldRuleValue,
                            OldWeekFormatCode = x.OldWeekFormatCode,
                            OldWeekYear = x.OldWeekYear,
                            OldWeekNo = x.OldWeekNo,
                            OldWeekSortKey = x.OldWeekSortKey,
                            NewWeekRuleSource = x.WeekCode.WeekRuleSource,
                            NewWeekRuleValue = x.WeekCode.WeekRuleValue,
                            NewWeekFormatCode = x.WeekCode.WeekFormatCode,
                            NewWeekYear = x.WeekCode.WeekYear,
                            NewWeekNo = x.WeekCode.WeekNo,
                            NewWeekSortKey = x.WeekCode.WeekSortKey,
                            Result = "UPDATED"
                        }).ToList()
                    };
                }
                catch
                {
                    if (transaction.Connection != null)
                    {
                        transaction.Rollback();
                    }
                    throw;
                }
            }
        }

        private SqlConnection Open()
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                throw new InvalidOperationException("缺少 BaseDb 连接字符串。");
            }

            SqlConnection connection = new SqlConnection(_connectionString);
            connection.Open();
            return connection;
        }

        public static string BuildHoldReleaseRequestId(string batchRequestId, long holdLogId)
        {
            string suffix = ":HOLD:" + holdLogId.ToString(CultureInfo.InvariantCulture);
            string readable = batchRequestId + suffix;
            if (readable.Length <= HoldReleaseRequestIdMaxLength)
            {
                return readable;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(batchRequestId);
            string hash;
            using (SHA256 sha256 = SHA256.Create())
            {
                hash = BitConverter.ToString(sha256.ComputeHash(bytes)).Replace("-", string.Empty);
            }

            return "WCR:" + hash + suffix;
        }

        private sealed class CurrentItemRow
        {
            public long BoxHUId { get; set; }
            public string BoxCode { get; set; }
            public bool IsHold { get; set; }
            public long ItemId { get; set; }
            public DateTime? PackCreatedTime { get; set; }
            public string DateCode { get; set; }
            public string OldRuleSource { get; set; }
            public string OldRuleValue { get; set; }
            public string OldWeekFormatCode { get; set; }
            public short? OldWeekYear { get; set; }
            public byte? OldWeekNo { get; set; }
            public int? OldWeekSortKey { get; set; }
        }

        private sealed class ActiveHoldRow
        {
            public long Id { get; set; }
            public string HoldReasonCode { get; set; }
        }
    }
}
