using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Outbound.Formal;
using WMS_HU_Model.Repositories;
using WMS_HU_Model.Rules;

namespace WMS_HU_Model.Services
{
    public class OutboundPrepickService : IOutboundPrepickService
    {
        private readonly IOutboundOrderRepository orders;
        private readonly IOutboundPrepickRepository repository;
        public OutboundPrepickService(IOutboundOrderRepository orders,IOutboundPrepickRepository repository)
        { this.orders=orders; this.repository=repository; }

        public OperationResult<OutboundPrepickDto> Preview(string sapDeliveryNo)
        {
            if(string.IsNullOrWhiteSpace(sapDeliveryNo) || sapDeliveryNo.Length>50)
                return OperationResult<OutboundPrepickDto>.Fail("交货单号无效。");
            try { return Read(sapDeliveryNo.Trim()); }
            catch(Exception ex) {
                Trace.TraceError("OutboundPrepick {0}: {1}",sapDeliveryNo,ex);
                return OperationResult<OutboundPrepickDto>.Fail("预拣货查询失败，请联系IT核查。");
            }
        }
        private OperationResult<OutboundPrepickDto> Read(string delivery)
        {
            var order=orders.GetOrder(delivery);
            if(order==null) return OperationResult<OutboundPrepickDto>.Fail("交货单不存在。");
            var mappings=repository.ReadWarehouses(order.PlantCode);
            if(mappings.Count!=1) return OperationResult<OutboundPrepickDto>.Fail("厂别需配置唯一有效的成品仓映射。");
            var map=mappings[0];
            if(string.IsNullOrWhiteSpace(order.SAPStorageLocation)
                || !Same(map.SAPStorageLocation,order.SAPStorageLocation))
                return OperationResult<OutboundPrepickDto>.Fail("交货单SAP库存地点与厂别仓库映射不一致，请联系IT核查。");
            bool hasWork=repository.HasWork(order.Id) || order.StartPickTime.HasValue
                || order.PickedTime.HasValue || order.ShippedTime.HasValue;
            if(hasWork && map.WarehouseId!=order.WarehouseId)
                return OperationResult<OutboundPrepickDto>.Fail("订单已有作业记录且仓库与厂别映射不一致，禁止生成跨仓建议。");
            var result=new OutboundPrepickDto { SapDeliveryNo=delivery,PlantCode=order.PlantCode,
                WarehouseCode=map.WarehouseCode,Mode="RECOMMENDATION" };
            if(map.WarehouseId!=order.WarehouseId)
                result.Message="订单仓库为"+order.WarehouseCode+"，本次按厂别映射"+map.WarehouseCode+"只读预览，未修改订单。";
            if(!Same(order.OrderStatus,"WAIT_PICK")) {
                var state=orders.GetPickingState(delivery);
                if(!state.IsSuccess) return OperationResult<OutboundPrepickDto>.Fail("原拣货任务状态异常，请联系IT核查。");
                result.Mode="EXISTING_WORK"; result.PickingState=state.Data;
                result.Message="展示既有任务及完成情况，不重新推荐或改变预留方案。";
                return OperationResult<OutboundPrepickDto>.Success(result);
            }
            var stock=repository.ReadStock(order,map.WarehouseId);
            foreach(var lines in order.Lines.GroupBy(x=>new { Material=Key(x.MaterialCode),Unit=Key(x.Uom) })) {
                var first=lines.First();
                var group=new OutboundPrepickGroup { MaterialCode=first.MaterialCode,Uom=first.Uom,
                    RequiredQty=lines.Sum(x=>x.EffectiveRequiredQty) };
                foreach(var boxRows in stock.GroupBy(x=>x.BoxHuId)) {
                    var all=boxRows.ToList();
                    var rows=all.Where(x=>Same(x.MaterialCode,group.MaterialCode) && Same(x.Uom,group.Uom)).ToList();
                    if(rows.Count==0) continue;
                    var r=rows[0];
                    var box=new OutboundPrepickBox { BoxHuId=r.BoxHuId,BoxCode=r.BoxCode,
                        MaterialCode=group.MaterialCode,Uom=group.Uom,LocationCode=r.LocationCode,
                        Qty=rows.Sum(x=>x.Qty),WeekSortKey=rows.Min(x=>x.WeekSortKey??int.MaxValue),
                        MaterialRevision=Join(rows.Select(x=>x.MaterialRevision)),DateCode=Join(rows.Select(x=>x.DateCode)),
                        OwnReservation=all.Any(x=>x.OwnReservation) };
                    if(all.Any(x=>x.Held)) box.Reasons.Add("HELD");
                    if(all.Any(x=>x.NotShelved)) box.Reasons.Add("NOT_SHELVED");
                    if(all.Any(x=>x.OtherReservation)) box.Reasons.Add("OTHER_RESERVED");
                    if(all.Any(x=>x.UnresolvedWeek)) box.Reasons.Add("UNRESOLVED_WEEK");
                    if(all.Any(x=>x.BadStock)) box.Reasons.Add("BAD_STOCK");
                    if(all.Any(x=>x.BadPackaging) || rows.Count!=all.Count || box.Qty<=0)
                        box.Reasons.Add("BAD_PACKAGING");
                    foreach(var pack in rows.Where(x=>x.InnerHuId.HasValue).GroupBy(x=>x.InnerHuId.Value)) {
                        box.InnerPacks.Add(new OutboundPrepickPack { InnerHuId=pack.Key,InnerCode=pack.First().InnerCode,
                            Qty=pack.Sum(x=>x.Qty),WeekSortKey=pack.Min(x=>x.WeekSortKey??int.MaxValue),
                            MaterialRevision=Join(pack.Select(x=>x.MaterialRevision)),DateCode=Join(pack.Select(x=>x.DateCode)) });
                    }
                    if(box.InnerPacks.Sum(x=>x.Qty)!=box.Qty && !box.Reasons.Contains("BAD_PACKAGING")) box.Reasons.Add("BAD_PACKAGING");
                    box.Eligible=box.Reasons.Count==0;
                    group.Boxes.Add(box);
                }
                group.Boxes=group.Boxes.OrderBy(x=>x.WeekSortKey).ThenBy(x=>x.BoxHuId).ToList();
                new OutboundPrepickPlanner().Build(group); result.Groups.Add(group);
            }
            // Do not present a new suggestion if formal picking started during this read.
            var latest=orders.GetOrder(delivery);
            if(latest==null || !Same(latest.OrderStatus,order.OrderStatus) || repository.HasWork(order.Id))
                return OperationResult<OutboundPrepickDto>.Fail("订单作业状态已变化，请重新选择交货单刷新预览。");
            return OperationResult<OutboundPrepickDto>.Success(result);
        }
        private static string Key(string value) { return (value??"").Trim().ToUpperInvariant(); }
        private static bool Same(string a,string b) { return Key(a)==Key(b); }
        private static string Join(IEnumerable<string> values) { return string.Join(" / ",values.Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct()); }
    }
}
