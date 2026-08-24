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
            ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings["MesDb"];
            if (settings == null || string.IsNullOrWhiteSpace(settings.ConnectionString)) throw new InvalidOperationException("MES连接配置MesDb未找到。");
            using (SqlConnection conn = new SqlConnection(settings.ConnectionString))
            {
                conn.Open();
                MesPlanStockReadModel plan = conn.QueryFirstOrDefault<MesPlanStockReadModel>(@"SELECT PK_PlanStock_Id, id AS Id, plantCode AS PlantCode, Stockby, status AS Status, Stockdt FROM dbo.PK_PlanStock WITH (NOLOCK) WHERE PK_PlanStock_Id=@PlanStockId;", new { PlanStockId = planStockSourceId.Trim() });
                MesFinishedGoodsInboundSourceGraph graph = new MesFinishedGoodsInboundSourceGraph { PlanStock = plan };
                if (plan == null) return graph;
                graph.Boxes = conn.Query<MesBoxReadModel>(@"SELECT PK_Box_Id, PK_PlanStock_Id, boxID AS BoxID, boxBarCode AS BoxBarCode, CustboxID, prodno AS ProdNo, prodrev AS ProdRev, UnitQty, plantCode AS PlantCode FROM dbo.PK_Box WITH (NOLOCK) WHERE PK_PlanStock_Id=@PlanStockId;", new { PlanStockId = planStockSourceId.Trim() }).AsList();
                graph.Packs = conn.Query<MesPackmasterReadModel>(@"SELECT PK_Packmaster_Id, boxID AS BoxID, CustPackID, PCSQty, datecode AS DateCode, createdt AS Createdt, prodno AS ProdNo, prodrev AS ProdRev, plantCode AS PlantCode FROM dbo.PK_Packmaster WITH (NOLOCK) WHERE boxID IN (SELECT boxID FROM dbo.PK_Box WITH (NOLOCK) WHERE PK_PlanStock_Id=@PlanStockId);", new { PlanStockId = planStockSourceId.Trim() }).AsList();
                return graph;
            }
        }

        public Dictionary<string, DateTime?> GetPackCreatedTimes(IList<string> packSourceEntityIds)
        {
            Dictionary<string, DateTime?> result = new Dictionary<string, DateTime?>(StringComparer.OrdinalIgnoreCase);
            if (packSourceEntityIds == null) return result;
            List<string> ids = packSourceEntityIds.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (ids.Count == 0) return result;
            ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings["MesDb"];
            if (settings == null || string.IsNullOrWhiteSpace(settings.ConnectionString)) throw new InvalidOperationException("MES连接配置MesDb未找到。");
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
