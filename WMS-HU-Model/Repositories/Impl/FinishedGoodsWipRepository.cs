using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using Dapper;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Models.Inventory;

namespace WMS_HU_Model.Repositories
{
    public class FinishedGoodsWipRepository : IFinishedGoodsWipRepository
    {
        public FinishedGoodsWipDto ReadBox(long boxHUId)
        {
            using (var connection = DbConnectionFactory.Create())
            using (var results = connection.QueryMultiple(@"
SELECT Id AS BoxHUId, HUCode AS BoxCode, PlantCode FROM dbo.WMS_FG_HU
WHERE Id=@boxHUId AND HUType='OUTER_BOX' AND LifecycleStatus='ACTIVE';
SELECT DISTINCT i.ProdNo AS MaterialCode, i.ProdRev AS MaterialRevision
FROM dbo.WMS_FG_HU_Relation r
JOIN dbo.WMS_FG_HU p ON p.Id=r.ChildHUId AND p.HUType='INNER_PACK' AND p.LifecycleStatus='ACTIVE'
JOIN dbo.WMS_FG_HU_Item i ON i.HUId=p.Id AND i.IsActive=1
WHERE r.ParentHUId=@boxHUId AND r.IsActive=1
ORDER BY i.ProdNo, i.ProdRev;", new { boxHUId }))
            {
                var box = results.ReadSingleOrDefault<FinishedGoodsWipDto>();
                var groups = results.Read<FinishedGoodsWipGroup>().AsList();
                if (box != null) box.Groups = groups;
                return box;
            }
        }

        public List<string> ReadFlows(string plantCode, string materialCode, string revision)
        {
            using (var connection = DbConnectionFactory.Create())
                return connection.Query<string>(@"
SELECT CONVERT(varchar(200),md_flow_id) FROM dbo.MD_Flow
WHERE plantcode=@plantCode AND prodno=@materialCode AND prodrev=@revision
AND status='on' AND ISNULL(isdeleted,0)=0;", new { plantCode, materialCode, revision }).AsList();
        }

        public List<FinishedGoodsWipStation> ReadStations(string plantCode, string flowId)
        {
            using (var connection = DbConnectionFactory.Create())
                return connection.Query<FinishedGoodsWipStation>(@"
SELECT CONVERT(varchar(200),md_flowstation_id) AS StationId, flowitemno AS FlowItemNo,
stationcode AS StationCode, stationname AS StationName, CONVERT(varchar(100),sort) AS Sort
FROM dbo.MD_FlowStation WHERE flowid=@flowId AND plantcode=@plantCode;", new { plantCode, flowId }).AsList();
        }

        // Extracted from PP_GetWIP_FLOW's normal-flow / SearchByWeb branch.
        // Deliberately omit rework, APP HTML, unused totals and synthetic stations.
        // PK_03 stock stays plant + material (no revision) to preserve the MES stock definition.
        internal const string WipSql = @"
IF (SELECT COUNT_BIG(*) FROM dbo.MD_ProdMaster
    WHERE plantcode=@PLANTCODE AND prodno=@prodno AND prodrev=@prodrev) <> 1
    THROW 50001, N'MES 料号版本主数据缺失或重复，无法计算流程/WIP 数量。', 1;

;WITH SelectedStations AS
(
    SELECT b.md_flowstation_id, b.flowitemno, b.stationcode, b.sort,
           CASE WHEN c.iswipstation=1 THEN '*' ELSE '' END + c.stationname AS stationname
    FROM dbo.MD_Flow a
    JOIN dbo.MD_FlowStation b ON b.flowid=a.md_flow_id AND b.plantcode=a.plantcode
    JOIN dbo.MD_Station c ON c.stationcode=b.stationcode AND c.plantcode=a.plantcode
    WHERE a.md_flow_id=@flowId AND a.plantcode=@PLANTCODE
      AND a.prodno=@prodno AND a.prodrev=@prodrev AND a.status='on' AND ISNULL(a.isdeleted,0)=0
      AND TRY_CONVERT(decimal(28,8),NULLIF(LTRIM(RTRIM(CONVERT(varchar(100),b.sort))),''))>=@startSort
),
WipTotals AS
(
    SELECT a.currentStationcode AS stationcode, a.Unit,
           SUM(CONVERT(decimal(28,8),a.WIPQTY)) AS Qty,
           SUM(CONVERT(decimal(28,8),a.ScrapSETQty)) AS ScrapSETQty,
           SUM(CONVERT(decimal(28,8),a.ScrapPCSQty)) AS ScrapPCSQty
    FROM dbo.PP_LotWip a
    JOIN dbo.PP_LOTCARD b ON b.PP_LotCard_ID=a.PP_LotCard_ID AND b.PLANTCODE=a.PLANTCODE
    WHERE a.PLANTCODE=@PLANTCODE AND a.prodno=@prodno AND a.prodrev=@prodrev
      AND a.WIPQTY>0 AND ISNULL(b.IsRework,0)=0
      AND EXISTS (SELECT 1 FROM SelectedStations s WHERE s.stationcode=a.currentStationcode)
    GROUP BY a.currentStationcode, a.Unit
),
PackRows AS
(
    SELECT CONVERT(decimal(28,8),setqty) AS SetQty, CONVERT(decimal(28,8),PCSQty) AS PcsQty
    FROM dbo.PK_Packmaster
    WHERE plantcode=@PLANTCODE AND prodno=@prodno AND prodrev=@prodrev AND Status IN (N'打印',N'生成')
      AND EXISTS (SELECT 1 FROM SelectedStations WHERE stationcode='PK_01')
    UNION ALL
    SELECT CONVERT(decimal(28,8),setqty), CONVERT(decimal(28,8),unitqty)
    FROM dbo.PK_BOX
    WHERE plantcode=@PLANTCODE AND prodno=@prodno AND prodrev=@prodrev AND Status IN (N'打印',N'生成',N'预入库')
      AND EXISTS (SELECT 1 FROM SelectedStations WHERE stationcode='PK_01')
),
PackTotals AS
(
    SELECT ISNULL(SUM(SetQty),0) AS SetQty, ISNULL(SUM(PcsQty),0) AS PcsQty FROM PackRows
),
FinishedStock AS
(
    SELECT ISNULL(SUM(CONVERT(decimal(28,8),stockqty)),0) AS PcsQty
    FROM dbo.mm_prodstock
    WHERE plantcode=@PLANTCODE AND material=@prodno
      AND EXISTS (SELECT 1 FROM SelectedStations WHERE stationcode='PK_03')
),
BaseRows AS
(
    SELECT s.md_flowstation_id, s.flowitemno, s.stationcode, s.stationname, s.sort,
           ISNULL(w.Qty,0) AS WIP, ISNULL(w.Unit,'') AS UNIT,
           CASE WHEN w.Unit='PNL' THEN z.setperpnlqty*ISNULL(w.Qty,0)-ISNULL(w.ScrapSETQty,0)
                WHEN w.Unit='SET' THEN ISNULL(w.Qty,0) ELSE 0 END AS SETqty,
           CASE WHEN w.Unit='PNL' THEN z.pcsperpnlqty*ISNULL(w.Qty,0)-ISNULL(w.ScrapPCSQty,0)
                WHEN w.Unit='SET' THEN z.pcspersetqty*ISNULL(w.Qty,0)-ISNULL(w.ScrapPCSQty,0)
                ELSE ISNULL(w.Qty,0) END
             + CASE WHEN s.stationcode='PK_03' THEN fg.PcsQty ELSE 0 END AS PCSqty
    FROM SelectedStations s
    LEFT JOIN WipTotals w ON w.stationcode=s.stationcode
    JOIN dbo.MD_ProdMaster z ON z.plantcode=@PLANTCODE AND z.prodno=@prodno AND z.prodrev=@prodrev
    CROSS JOIN FinishedStock fg
)
SELECT r.md_flowstation_id, r.flowitemno, r.stationcode, r.stationname, r.UNIT,
       r.WIP + CASE WHEN r.stationcode='PK_01' AND p.SetQty>0 THEN p.SetQty ELSE 0 END AS WIP,
       CASE WHEN r.stationcode='PK_01' AND p.SetQty>0 THEN ISNULL(r.SETqty,0)+p.SetQty ELSE r.SETqty END AS SETqty,
       CASE WHEN r.stationcode='PK_01' AND p.SetQty>0 THEN ISNULL(r.PCSqty,0)+p.PcsQty ELSE r.PCSqty END AS PCSqty
FROM BaseRows r CROSS JOIN PackTotals p
ORDER BY TRY_CONVERT(decimal(28,8),r.sort), r.flowitemno, r.md_flowstation_id, r.UNIT;";

        public List<FinishedGoodsWipStation> ReadWip(string plantCode, string materialCode, string revision, string flowId, decimal startSort)
        {
            using (var connection = DbConnectionFactory.Create())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = WipSql;
                command.CommandType = CommandType.Text;
                command.Parameters.Add("@prodno", SqlDbType.VarChar, materialCode.Length).Value = materialCode;
                command.Parameters.Add("@prodrev", SqlDbType.VarChar, revision.Length).Value = revision;
                command.Parameters.Add("@PLANTCODE", SqlDbType.VarChar, plantCode.Length).Value = plantCode;
                command.Parameters.Add("@flowId", SqlDbType.VarChar, flowId.Length).Value = flowId;
                var sortParameter = command.Parameters.Add("@startSort", SqlDbType.Decimal);
                sortParameter.Precision = 28;
                sortParameter.Scale = 8;
                sortParameter.Value = startSort;
                connection.Open();
                var rows = new List<FinishedGoodsWipStation>();
                using (var reader = command.ExecuteReader())
                    while (reader.Read()) rows.Add(ReadWipRow(reader));
                return rows;
            }
        }

        internal static FinishedGoodsWipStation ReadWipRow(IDataRecord reader)
        {
            return new FinishedGoodsWipStation
            {
                StationId = Convert.ToString(reader["md_flowstation_id"]).Trim(),
                FlowItemNo = Convert.ToString(reader["flowitemno"]).Trim(),
                StationCode = Convert.ToString(reader["stationcode"]).Trim(),
                StationName = Convert.ToString(reader["stationname"]),
                WipQty = ReadQuantity(reader["WIP"]),
                Unit = Convert.ToString(reader["UNIT"]),
                SetQty = ReadQuantity(reader["SETqty"]),
                PcsQty = ReadQuantity(reader["PCSqty"])
            };
        }

        private static decimal? ReadQuantity(object value)
        {
            if (value == null || value == DBNull.Value || string.IsNullOrWhiteSpace(Convert.ToString(value)))
                return null;
            return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        }
    }
}
