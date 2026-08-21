using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
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
    }
}
