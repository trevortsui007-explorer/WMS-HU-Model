using System;
using System.Collections.Generic;

namespace WMS_HU_Model.Models.Inbound
{
    public class MesPlanStockReadModel
    {
        public string PK_PlanStock_Id { get; set; }
        public long Id { get; set; }
        public string PlantCode { get; set; }
        public string Stockby { get; set; }
        public string Status { get; set; }
        public DateTime? Stockdt { get; set; }
    }

    public class MesBoxReadModel
    {
        public string PK_Box_Id { get; set; }
        public string PK_PlanStock_Id { get; set; }
        public string BoxID { get; set; }
        public string BoxBarCode { get; set; }
        public string CustboxID { get; set; }
        public string ProdNo { get; set; }
        public string ProdRev { get; set; }
        public decimal UnitQty { get; set; }
        public string PlantCode { get; set; }
    }

    public class MesPackmasterReadModel
    {
        public string PK_Packmaster_Id { get; set; }
        public string BoxID { get; set; }
        public string CustPackID { get; set; }
        public decimal PCSQty { get; set; }
        public string DateCode { get; set; }
        public DateTime? Createdt { get; set; }
        public string ProdNo { get; set; }
        public string ProdRev { get; set; }
        public string PlantCode { get; set; }
    }

    public class MesFinishedGoodsInboundSourceGraph
    {
        public MesPlanStockReadModel PlanStock { get; set; }
        public List<MesBoxReadModel> Boxes { get; set; }
        public List<MesPackmasterReadModel> Packs { get; set; }

        public MesFinishedGoodsInboundSourceGraph()
        {
            Boxes = new List<MesBoxReadModel>();
            Packs = new List<MesPackmasterReadModel>();
        }
    }
}
