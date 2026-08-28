using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Inbound;

namespace WMS_HU_Model.Services
{
    public class MesFinishedGoodsInboundSnapshotBuilder
    {
        public OperationResult<FinishedGoodsInboundReceiveRequest> Build(MesFinishedGoodsInboundSourceGraph source)
        {
            string error = Validate(source);
            if (error != null) return OperationResult<FinishedGoodsInboundReceiveRequest>.Fail(error);
            MesPlanStockReadModel plan = source.PlanStock;
            FinishedGoodsInboundReceiveRequest request = new FinishedGoodsInboundReceiveRequest
            {
                RequestId = "MES-FGI-" + plan.PK_PlanStock_Id.Trim(),
                Operator = plan.Stockby.Trim(),
                Remark = "Finished goods inbound from MES PlanStock.",
                Snapshot = new FinishedGoodsInboundSnapshot
                {
                    SourceSystem = "MES",
                    SourceType = "PLAN_STOCK",
                    SourceEntityId = plan.PK_PlanStock_Id.Trim(),
                    PlantCode = plan.PlantCode.Trim(),
                    Pallet = new FinishedGoodsInboundPalletSnapshot { SourceEntityId = plan.PK_PlanStock_Id.Trim(), PalletCode = "PLT-" + plan.Id.ToString(CultureInfo.InvariantCulture) },
                    Boxes = new List<FinishedGoodsInboundBoxSnapshot>()
                }
            };
            foreach (MesBoxReadModel box in source.Boxes.OrderBy(x => x.PK_Box_Id.Trim(), StringComparer.Ordinal))
            {
                FinishedGoodsInboundBoxSnapshot target = new FinishedGoodsInboundBoxSnapshot
                {
                    SourceEntityId = box.PK_Box_Id.Trim(),
                    BoxCode = box.BoxBarCode.Trim(),
                    AlternateBoxCode = AlternateBoxCode(box.CustboxID, box.BoxBarCode),
                    MaterialCode = box.ProdNo.Trim(),
                    MaterialRevision = TrimOrNull(box.ProdRev),
                    Uom = "PCS",
                    WeekRuleSource = null,
                    WeekRuleValue = null,
                    InnerPacks = new List<FinishedGoodsInboundPackSnapshot>()
                };
                foreach (MesPackmasterReadModel pack in source.Packs.Where(x => Same(x.BoxID, box.BoxID)).OrderBy(x => x.PK_Packmaster_Id.Trim(), StringComparer.Ordinal))
                    target.InnerPacks.Add(new FinishedGoodsInboundPackSnapshot
                    {
                        SourceEntityId = pack.PK_Packmaster_Id.Trim(),
                        PackId = pack.PackID.Trim(),
                        PackCode = pack.CustPackID.Trim(),
                        IsSpecialSapCharg = pack.IsSpecialSapCharg,
                        Qty = pack.PCSQty,
                        DateCode = pack.DateCode,
                        PackCreatedTime = pack.Createdt
                    });
                request.Snapshot.Boxes.Add(target);
            }
            return OperationResult<FinishedGoodsInboundReceiveRequest>.Success(request);
        }

        private static string Validate(MesFinishedGoodsInboundSourceGraph source)
        {
            if (source == null || source.PlanStock == null) return "MES PlanStock不存在。";
            MesPlanStockReadModel plan = source.PlanStock;
            if (!Same(plan.Status, "入库")) return "MES PlanStock状态不是入库。";
            if (Empty(plan.PK_PlanStock_Id)) return "MES PlanStock.PK_PlanStock_Id不能为空。";
            if (plan.Id <= 0) return "MES PlanStock.id无效。";
            if (Empty(plan.PlantCode)) return "MES PlanStock.plantCode不能为空。";
            if (Empty(plan.Stockby)) return "MES PlanStock.Stockby不能为空。";
            if (source.Boxes == null || source.Boxes.Count == 0) return "MES PlanStock不存在Box。";
            if (source.Packs == null) return "MES PlanStock不存在Pack。";
            foreach (MesPackmasterReadModel pack in source.Packs)
            {
                if (pack == null || Empty(pack.BoxID)) return "MES Pack必填字段缺失。";
                if (!source.Boxes.Any(x => x != null && Same(x.BoxID, pack.BoxID))) return "MES Pack[" + pack.CustPackID + "]不存在对应Box。";
            }
            foreach (MesBoxReadModel box in source.Boxes)
            {
                if (box == null || Empty(box.PK_Box_Id) || Empty(box.BoxID) || Empty(box.BoxBarCode) || Empty(box.ProdNo)) return "MES Box必填字段缺失。";
                if (!Same(box.PK_PlanStock_Id, plan.PK_PlanStock_Id)) return "MES Box[" + box.BoxBarCode + "]与PlanStock关系不一致。";
                if (!Same(box.PlantCode, plan.PlantCode)) return "MES Box[" + box.BoxBarCode + "]与PlanStock厂别不一致。";
                List<MesPackmasterReadModel> packs = source.Packs == null ? new List<MesPackmasterReadModel>() : source.Packs.Where(x => x != null && Same(x.BoxID, box.BoxID)).ToList();
                if (packs.Count == 0) return "MES Box[" + box.BoxBarCode + "]不存在InnerPack。";
                decimal qty = 0m;
                foreach (MesPackmasterReadModel pack in packs)
                {
                    if (Empty(pack.PK_Packmaster_Id) || Empty(pack.PackID) || Empty(pack.CustPackID) || pack.PCSQty <= 0) return "MES Box[" + box.BoxBarCode + "]存在无效InnerPack。";
                    if (!Same(pack.PlantCode, plan.PlantCode)) return "MES Pack[" + pack.CustPackID + "]与PlanStock厂别不一致。";
                    if (!Same(pack.ProdNo, box.ProdNo) || !Same(pack.ProdRev, box.ProdRev)) return "MES Box[" + box.BoxBarCode + "]与Pack[" + pack.CustPackID + "]料号版本不一致。";
                    qty += pack.PCSQty;
                }
                if (qty != box.UnitQty) return "MES Box[" + box.BoxBarCode + "]数量不一致：UnitQty=" + box.UnitQty + ", PackPCSQty=" + qty + "。";
            }
            return null;
        }

        private static string AlternateBoxCode(string customerCode, string boxCode)
        {
            if (Empty(customerCode) || Same(customerCode, boxCode)) return null;
            return customerCode.Trim();
        }

        private static string TrimOrNull(string value) { return Empty(value) ? null : value.Trim(); }
        private static bool Empty(string value) { return string.IsNullOrWhiteSpace(value); }
        private static bool Same(string left, string right) { return string.Equals(TrimOrNull(left), TrimOrNull(right), StringComparison.OrdinalIgnoreCase); }
    }
}
