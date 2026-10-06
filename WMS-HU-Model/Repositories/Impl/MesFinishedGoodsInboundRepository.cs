using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using WMS_HU_Model.Models.Inbound;

namespace WMS_HU_Model.Repositories
{
    public class MesFinishedGoodsInboundRepository : IMesFinishedGoodsInboundRepository
    {
        public MesFinishedGoodsInboundSourceGraph GetPlanStock(string planStockSourceId)
        {
            if (string.IsNullOrWhiteSpace(planStockSourceId)) throw new ArgumentException("MES PlanStock source identity不能为空。", nameof(planStockSourceId));
            ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings["BaseDb"];
            if (settings == null || string.IsNullOrWhiteSpace(settings.ConnectionString)) throw new InvalidOperationException("连接配置BaseDb未找到。");
            using (SqlConnection conn = new SqlConnection(settings.ConnectionString))
            {
                conn.Open();
                MesPlanStockReadModel plan = conn.QueryFirstOrDefault<MesPlanStockReadModel>(@"SELECT PK_PlanStock_Id, id AS Id, plantCode AS PlantCode, Stockby, status AS Status, Stockdt FROM dbo.PK_PlanStock WITH (NOLOCK) WHERE PK_PlanStock_Id=@PlanStockId;", new { PlanStockId = planStockSourceId.Trim() });
                MesFinishedGoodsInboundSourceGraph graph = new MesFinishedGoodsInboundSourceGraph { PlanStock = plan };
                if (plan == null) return graph;
                graph.Boxes = conn.Query<MesBoxReadModel>(@"SELECT PK_Box_Id, PK_PlanStock_Id, boxID AS BoxID, boxBarCode AS BoxBarCode, CustboxID, prodno AS ProdNo, prodrev AS ProdRev, UnitQty, plantCode AS PlantCode FROM dbo.PK_Box WITH (NOLOCK) WHERE PK_PlanStock_Id=@PlanStockId;", new { PlanStockId = planStockSourceId.Trim() }).AsList();
                graph.Packs = conn.Query<MesPackmasterReadModel>(@"
SELECT pm.PK_Packmaster_Id,CAST(pm.PackID AS VARCHAR(50)) AS PackID,pm.boxID AS BoxID,pm.CustPackID,
       pm.PCSQty,pm.datecode AS DateCode,pm.createdt AS Createdt,pm.prodno AS ProdNo,pm.prodrev AS ProdRev,
       pm.plantCode AS PlantCode,lotFact.LotNo,lotFact.LotNoCount,
       CAST(CASE WHEN EXISTS
       (
           SELECT 1
           FROM dbo.PK_PackLot packLot WITH (NOLOCK)
           INNER JOIN dbo.PP_LotCard lotCard WITH (NOLOCK) ON lotCard.LotNo=packLot.LotNo
           WHERE packLot.PK_Packmaster_Id=pm.PK_Packmaster_Id
             AND LTRIM(RTRIM(lotCard.OrderType)) IN ('ZP02','ZP05','ZP06','ZP08')
       ) THEN 1 ELSE 0 END AS BIT) AS IsSpecialSapCharg
FROM dbo.PK_Packmaster pm WITH (NOLOCK)
OUTER APPLY
(
    SELECT
        STUFF((
            SELECT ',' + orderedLot.LotNo
            FROM
            (
                SELECT LTRIM(RTRIM(sourceLot.LotNo)) AS LotNo,
                       MIN(sourceLot.LotSeq) AS FirstLotSeq
                FROM dbo.PK_PackLot sourceLot WITH (NOLOCK)
                WHERE sourceLot.PK_Packmaster_Id=pm.PK_Packmaster_Id
                  AND NULLIF(LTRIM(RTRIM(sourceLot.LotNo)), '') IS NOT NULL
                GROUP BY LTRIM(RTRIM(sourceLot.LotNo))
            ) orderedLot
            ORDER BY orderedLot.FirstLotSeq,orderedLot.LotNo
            FOR XML PATH(''),TYPE
        ).value('.', 'VARCHAR(MAX)'),1,1,'') AS LotNo,
        (
            SELECT COUNT(DISTINCT NULLIF(LTRIM(RTRIM(sourceLot.LotNo)), ''))
            FROM dbo.PK_PackLot sourceLot WITH (NOLOCK)
            WHERE sourceLot.PK_Packmaster_Id=pm.PK_Packmaster_Id
        ) AS LotNoCount
) lotFact
WHERE pm.boxID IN (SELECT boxID FROM dbo.PK_Box WITH (NOLOCK) WHERE PK_PlanStock_Id=@PlanStockId);",
                    new { PlanStockId = planStockSourceId.Trim() }).AsList();
                return graph;
            }
        }

        public Dictionary<string, DateTime?> GetPackCreatedTimes(IList<string> packSourceEntityIds)
        {
            Dictionary<string, DateTime?> result = new Dictionary<string, DateTime?>(StringComparer.OrdinalIgnoreCase);
            if (packSourceEntityIds == null) return result;
            List<string> ids = packSourceEntityIds.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (ids.Count == 0) return result;
            ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings["BaseDb"];
            if (settings == null || string.IsNullOrWhiteSpace(settings.ConnectionString)) throw new InvalidOperationException("连接配置BaseDb未找到。");
            using (SqlConnection conn = new SqlConnection(settings.ConnectionString))
            {
                conn.Open();
                foreach (List<string> batch in ids.Select((value, index) => new { value, index }).GroupBy(x => x.index / 500).Select(x => x.Select(y => y.value).ToList()))
                {
                    List<PackCreatedTimeRow> rows = conn.Query<PackCreatedTimeRow>(@"SELECT PK_Packmaster_Id AS SourceEntityId, createdt AS PackCreatedTime FROM dbo.PK_Packmaster WITH (NOLOCK) WHERE PK_Packmaster_Id IN @Ids;", new { Ids = batch }).AsList();
                    foreach (PackCreatedTimeRow row in rows) result[row.SourceEntityId] = row.PackCreatedTime;
                }
            }
            return result;
        }

        private class PackCreatedTimeRow
        {
            public string SourceEntityId { get; set; }
            public DateTime? PackCreatedTime { get; set; }
        }
    }
}
