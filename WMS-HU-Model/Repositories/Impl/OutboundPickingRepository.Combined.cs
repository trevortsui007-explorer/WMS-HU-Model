using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Newtonsoft.Json;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Models.Outbound.Formal;
using WMS_HU_Model.Rules;

namespace WMS_HU_Model.Repositories
{
    // Same repository/transaction boundary as legacy picking; no separate reservation system.
    public partial class OutboundPickingRepository
    {
        internal const string MemberOrdersSql = @"
SELECT a.OutboundOrderId FROM dbo.WMS_FG_OutboundAllocation a WHERE a.Id=@AllocationId AND a.OutboundOrderId IS NOT NULL
UNION
SELECT l.OutboundOrderId FROM dbo.WMS_FG_OutboundPickBatch b
JOIN dbo.WMS_FG_OutboundPickBatchDetail d ON d.PickBatchId=b.Id
JOIN dbo.WMS_FG_OutboundPickDetailItem i ON i.PickDetailId=d.Id
JOIN dbo.WMS_FG_OutboundOrderLine l ON l.Id=i.OutboundOrderLineId WHERE b.AllocationId=@AllocationId";

        public OutboundCombinedDto PreviewCombined(OutboundCombinedRequest request)
        {
            using (var conn = DbConnectionFactory.Create())
            {
                conn.Open();
                // No writes, including warehouse correction, in preview.
                using (var tran = conn.BeginTransaction(IsolationLevel.Serializable))
                {
                    var result = BuildCombined(conn, tran, request, false);
                    tran.Commit(); return result;
                }
            }
        }

        private static OutboundCombinedDto BuildCombined(SqlConnection conn, SqlTransaction tran,
            OutboundCombinedRequest request, bool persist)
        {
            var result = new OutboundCombinedDto { AllocationMode = request.DeliveryNos.Count > 1 ? "COMBINED" : "SINGLE" };
            foreach (string no in request.DeliveryNos.OrderBy(x => x, StringComparer.Ordinal))
            {
                var order = ReadOrderForUpdate(conn, tran, no);
                if (order == null) throw new InvalidOperationException("交货单不存在：" + no);
                if (order.OrderStatus != "WAIT_PICK" || order.Lines.Any(x => x.AllocatedQty != 0 || x.EffectivePickedQty != 0))
                    throw new InvalidOperationException("仅未分配的待拣交货单可生成新方案；已锁单请读取原任务。");
                if (string.IsNullOrWhiteSpace(order.SAPStorageLocation)) throw new InvalidOperationException("交货单缺少SAP库存地点。");
                string error = EnsureOrderWarehouse(conn, tran, order, request.Operator, persist);
                if (error != null) throw new InvalidOperationException(error);
                int active = conn.ExecuteScalar<int>(@"SELECT COUNT(*) FROM dbo.WMS_FG_OutboundAllocation a
WHERE a.AllocationStatus IN ('ACTIVE','PICKING','COMPLETED') AND (a.OutboundOrderId=@Id OR EXISTS
(SELECT 1 FROM dbo.WMS_FG_OutboundPickBatch b JOIN dbo.WMS_FG_OutboundPickBatchDetail d ON d.PickBatchId=b.Id
 JOIN dbo.WMS_FG_OutboundPickDetailItem i ON i.PickDetailId=d.Id JOIN dbo.WMS_FG_OutboundOrderLine l ON l.Id=i.OutboundOrderLineId
 WHERE b.AllocationId=a.Id AND l.OutboundOrderId=@Id))", new { order.Id }, tran);
                if (active != 0) throw new InvalidOperationException("交货单已有未释放分配。");
                result.Orders.Add(order);
            }
            var first = result.Orders.First();
            if (string.IsNullOrWhiteSpace(first.CustomerCode) || result.Orders.Any(o => o.PlantCode != first.PlantCode
                || o.WarehouseId != first.WarehouseId || o.CustomerCode != first.CustomerCode || o.SAPStorageLocation != first.SAPStorageLocation))
                throw new InvalidOperationException("联合出货必须同厂别、同仓库、同收货客户、同SAP库存地点。");
            var lines = result.Orders.SelectMany(x => x.Lines).ToList();
            if (lines.Count == 0 || lines.Select(x => BuildGroupKey(x.MaterialCode, x.Uom)).Distinct().Count() != 1)
                throw new InvalidOperationException("本次锁定必须同料号、同单位。");
            SetCombinedGift(result, lines);
            var candidates = GetAllocationCandidatesInternal(conn, tran, first, lines[0].MaterialCode, lines[0].Uom);
            if (candidates.Count > 100) throw new InvalidOperationException("候选过多，暂无法生成推荐。");
            foreach (var candidate in candidates)
            {
                var items = ReadScanItems(conn, tran, candidate.BoxHuId, candidate.MaterialCode, candidate.Uom);
                result.Boxes.Add(new OutboundCombinedBoxDto {
                    BoxHuId = candidate.BoxHuId, BoxCode = candidate.BoxHuCode,
                    LocationId = candidate.LocationId, LocationCode = candidate.LocationCode,
                    WeekSortKey = candidate.MinWeekSortKey, Qty = candidate.Qty,
                    MaterialCode = candidate.MaterialCode, Uom = candidate.Uom,
                    Items = items.Select(x => new OutboundCombinedItemDto { HuItemId=x.HuItemId, InnerHuId=x.InnerHuId,
                        MaterialCode=x.MaterialCode, MaterialRevision=x.MaterialRevision, DateCode=x.DateCode,
                        WeekSortKey=x.WeekSortKey, BatchNo=x.BatchNo, SAPBatchNo=x.SAPBatchNo, Qty=x.Qty, Uom=x.Uom }).ToList()
                });
            }
            result.RequiredQty = lines.Sum(x => x.EffectiveRequiredQty);
            result.AvailableQty = result.Boxes.Sum(x => x.Qty);
            result.ResultType = OutboundCombinedPlanner.Build(result.Boxes, result.Orders);
            result.CanLock = result.ResultType == "EXACT_WHOLE_BOX";
            result.Recommendation = new OutboundPrepickGroup { MaterialCode=lines[0].MaterialCode,Uom=lines[0].Uom,RequiredQty=result.RequiredQty,
                Boxes=result.Boxes.Select(b=>new OutboundPrepickBox { BoxHuId=b.BoxHuId,BoxCode=b.BoxCode,LocationCode=b.LocationCode,
                    MaterialCode=b.MaterialCode,Uom=b.Uom,Qty=b.Qty,WeekSortKey=b.WeekSortKey,Eligible=true,
                    InnerPacks=b.Items.GroupBy(i=>i.InnerHuId).Select(g=>new OutboundPrepickPack { InnerHuId=g.Key,
                        Qty=g.Sum(i=>i.Qty),WeekSortKey=g.Min(i=>i.WeekSortKey),DateCode=string.Join("/",g.Select(i=>i.DateCode).Distinct()),
                        MaterialRevision=string.Join("/",g.Select(i=>i.MaterialRevision).Distinct()) }).ToList() }).ToList() };
            var masters=conn.Query<PrepickPackMaster>(OutboundPrepickRepository.PackMasterSql,
                new { plantCode=first.PlantCode, materials=new[] { lines[0].MaterialCode } },tran).ToList();
            OutboundPrepickPlanner.SetPackConversion(result.Recommendation,lines,masters);
            new OutboundPrepickPlanner().Build(result.Recommendation);
            using (var sha = SHA256.Create())
                result.PreviewVersion = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(result)))).Replace("-", "");
            return result;
        }

        public OutboundCombinedDto LockCombined(OutboundCombinedRequest request)
        {
            using (var conn = DbConnectionFactory.Create())
            {
                conn.Open();
                using (var tran = conn.BeginTransaction(IsolationLevel.Serializable))
                {
                    string prior = conn.QueryFirstOrDefault<string>(@"SELECT AllocationNo FROM dbo.WMS_FG_OutboundAllocation WITH(UPDLOCK,HOLDLOCK)
WHERE RequestId=@RequestId", new { request.RequestId }, tran);
                    if (prior != null)
                    {
                        var original = ReadCombined(conn, tran, prior);
                        if (!original.Orders.Select(x => x.SAPDeliveryNo).OrderBy(x=>x).SequenceEqual(request.DeliveryNos.OrderBy(x=>x)))
                            throw new InvalidOperationException("RequestId已被其他交货单使用。");
                        if (original.AllocationStatus == "RELEASED") throw new InvalidOperationException("原任务已解锁，请重新预览并使用新请求号。");
                        original.IsDuplicate = true; tran.Commit(); return original;
                    }
                    if (IsRequestIdUsedByOtherOperation(conn, tran, request.RequestId)) throw new InvalidOperationException("RequestId已被其他业务使用。");
                    var plan = BuildCombined(conn, tran, request, true);
                    if (!plan.CanLock) throw new InvalidOperationException("只能锁定整箱精确满足需求的方案。");
                    if (plan.PreviewVersion != request.PreviewVersion) throw new InvalidOperationException("订单或库存已变化，请重新预览后锁定。");
                    string token = Guid.NewGuid().ToString("N").ToUpperInvariant();
                    string no = "OA" + token;
                    long id = conn.ExecuteScalar<long>(@"INSERT dbo.WMS_FG_OutboundAllocation
(AllocationNo,OutboundOrderId,RequestId,AllocationStatus,AllocateDt,AllocateBy,ExpireDt,Remark,AllocationMode,LockSource,
 GiftQtyPcs,MdProdRevSnapshot,PcsPerSetQtySnapshot,PcsPerPnlQtySnapshot)
OUTPUT INSERTED.Id VALUES(@No,@OrderId,@RequestId,'ACTIVE',SYSDATETIME(),@Operator,NULL,@Remark,@Mode,'WEB',
 @GiftQtyPcs,@MdProdRevSnapshot,@PcsPerSetQtySnapshot,@PcsPerPnlQtySnapshot)",
                        new { No=no, OrderId=plan.Orders.Count==1 ? (long?)plan.Orders[0].Id : null,
                            request.RequestId, request.Operator, request.Remark, Mode=plan.AllocationMode,
                            plan.GiftQtyPcs,plan.MdProdRevSnapshot,plan.PcsPerSetQtySnapshot,plan.PcsPerPnlQtySnapshot }, tran);
                    var selected = plan.Boxes.Where(x => x.Selected).OrderBy(x=>x.WeekSortKey).ThenBy(x=>x.BoxHuId).ToList();
                    var group = new PlannedGroup { MaterialCode=selected[0].MaterialCode, Uom=selected[0].Uom, RequiredQty=plan.RequiredQty };
                    long batchId = InsertPickBatch(conn, tran, "PB"+token, id, group,
                        new StartPickingCommand { Operator=request.Operator, Remark=request.Remark });
                    int sequence = 0;
                    foreach (var box in selected)
                    {
                        sequence++;
                        var candidate = new OutboundAllocationCandidateDto { BoxHuId=box.BoxHuId, LocationId=box.LocationId, Qty=box.Qty,
                            MinWeekSortKey=box.Items.Min(x=>x.WeekSortKey), MaxWeekSortKey=box.Items.Max(x=>x.WeekSortKey),
                            DistinctWeekCount=box.Items.Select(x=>x.WeekSortKey).Distinct().Count() };
                        long reservationId = InsertReservation(conn, tran, "RS"+token+sequence.ToString("D3"),id,batchId,candidate,box.Uom,null,request.Operator);
                        long detailId = conn.ExecuteScalar<long>(@"INSERT dbo.WMS_FG_OutboundPickBatchDetail
(PickBatchId,ReservationId,SequenceNo,BoxHUId,BoxQty,EffectiveQty,Uom,MoveLogId,DetailStatus,MinWeekSortKey)
OUTPUT INSERTED.Id VALUES(@BatchId,@ReservationId,@Sequence,@BoxHuId,@Qty,0,@Uom,NULL,'PLANNED',@WeekSortKey)",
                            new { BatchId=batchId, ReservationId=reservationId, Sequence=sequence, box.BoxHuId, box.Qty, box.Uom, box.WeekSortKey },tran);
                        InsertPickDetailItems(conn,tran,detailId,box.Shares.Select(x=>new ScanItemAllocation {
                            LineId=x.LineId, AllocatedQty=x.Qty, Item=new ScanItemRow { InnerHuId=x.InnerHuId, HuItemId=x.HuItemId,
                                MaterialCode=x.MaterialCode, MaterialRevision=x.MaterialRevision, DateCode=x.DateCode,
                                WeekSortKey=x.WeekSortKey, BatchNo=x.BatchNo, SAPBatchNo=x.SAPBatchNo, Uom=x.Uom } }).ToList());
                    }
                    foreach (var order in plan.Orders)
                    {
                        UpdateAllocatedLines(conn,tran,order.Id,group.MaterialCode,group.Uom);
                        if (conn.Execute(@"UPDATE dbo.WMS_FG_OutboundOrder SET OrderStatus='ALLOCATED',StartPickDt=SYSDATETIME(),
LastEditBy=@Operator,LastEditDt=SYSDATETIME() WHERE Id=@Id AND OrderStatus='WAIT_PICK'",new { order.Id,request.Operator },tran)!=1)
                            throw new InvalidOperationException("订单状态已变化。");
                    }
                    var result = ReadCombined(conn,tran,no); tran.Commit(); return result;
                }
            }
        }

        public OutboundCombinedDto GetAllocation(string allocationNo)
        {
            using (var conn=DbConnectionFactory.Create())
            {
                conn.Open();
                using(var tran=conn.BeginTransaction(IsolationLevel.RepeatableRead))
                { var result=ReadCombined(conn,tran,allocationNo); tran.Commit(); return result; }
            }
        }

        private static OutboundCombinedDto ReadCombined(SqlConnection conn,SqlTransaction tran,string allocationNo)
        {
            var header=conn.QueryFirstOrDefault<CombinedHeader>(@"SELECT Id,AllocationNo,AllocationMode,LockSource,AllocationStatus,
GiftQtyPcs,MdProdRevSnapshot,PcsPerSetQtySnapshot,PcsPerPnlQtySnapshot,
FirstPickDt FROM dbo.WMS_FG_OutboundAllocation WHERE AllocationNo=@allocationNo",new { allocationNo },tran);
            if(header==null) throw new InvalidOperationException("锁定任务不存在。");
            var result=new OutboundCombinedDto { AllocationNo=header.AllocationNo,AllocationMode=header.AllocationMode,
                LockSource=header.LockSource,AllocationStatus=header.AllocationStatus,
                GiftQtyPcs=header.GiftQtyPcs,MdProdRevSnapshot=header.MdProdRevSnapshot,
                PcsPerSetQtySnapshot=header.PcsPerSetQtySnapshot,PcsPerPnlQtySnapshot=header.PcsPerPnlQtySnapshot,
                CanRelease=header.AllocationStatus=="ACTIVE" && !header.FirstPickDt.HasValue,ResultType="SELECTED" };
            var nos=conn.Query<string>("SELECT SAPDeliveryNo FROM dbo.WMS_FG_OutboundOrder WHERE Id IN ("+MemberOrdersSql+") ORDER BY SAPDeliveryNo",
                new { AllocationId=header.Id },tran).ToList();
            foreach(var no in nos) result.Orders.Add(ReadOrderForUpdate(conn,tran,no));
            var rows=conn.Query<CombinedDetailRow>(@"SELECT d.Id, d.BoxHUId,hu.HUCode AS BoxCode,r.SourceLocationId AS LocationId,
loc.LocationCode,b.PickBatchNo,b.ProdNo AS MaterialCode,d.Uom,d.BoxQty AS Qty,d.DetailStatus,d.MinWeekSortKey AS WeekSortKey
FROM dbo.WMS_FG_OutboundPickBatch b JOIN dbo.WMS_FG_OutboundPickBatchDetail d ON d.PickBatchId=b.Id
JOIN dbo.WMS_FG_HU hu ON hu.Id=d.BoxHUId JOIN dbo.WMS_FG_HU_Reservation r ON r.Id=d.ReservationId
JOIN dbo.WMS_FG_Location loc ON loc.Id=r.SourceLocationId WHERE b.AllocationId=@AllocationId
ORDER BY d.MinWeekSortKey,d.BoxHUId",new { AllocationId=header.Id },tran).ToList();
            foreach(var row in rows)
            {
                row.Selected=true;
                row.Shares=conn.Query<OutboundCombinedShareDto>(@"SELECT i.OutboundOrderLineId AS LineId,o.SAPDeliveryNo,l.SAPItemNo,
i.HUItemId,i.InnerHUId,i.ProdNo AS MaterialCode,i.ProdRev AS MaterialRevision,i.DateCode,i.WeekSortKey,
i.LotNo AS BatchNo,i.SAPBatchNo,i.Uom,i.AllocatedQty AS Qty FROM dbo.WMS_FG_OutboundPickDetailItem i
JOIN dbo.WMS_FG_OutboundOrderLine l ON l.Id=i.OutboundOrderLineId JOIN dbo.WMS_FG_OutboundOrder o ON o.Id=l.OutboundOrderId
WHERE i.PickDetailId=@Id ORDER BY o.SAPDeliveryNo,l.SAPItemNo,i.HUItemId",new { row.Id },tran).ToList();
                result.Boxes.Add(row);
            }
            result.RequiredQty=result.Orders.SelectMany(x=>x.Lines).Sum(x=>x.EffectiveRequiredQty);
            result.AvailableQty=result.Boxes.Sum(x=>x.Qty);
            return result;
        }
        private class CombinedHeader { public long Id {get;set;} public string AllocationNo {get;set;}
            public string AllocationMode {get;set;} public string LockSource {get;set;} public string AllocationStatus {get;set;}
            public decimal GiftQtyPcs {get;set;} public string MdProdRevSnapshot {get;set;}
            public int? PcsPerSetQtySnapshot {get;set;} public int? PcsPerPnlQtySnapshot {get;set;}
            public DateTime? FirstPickDt {get;set;} }

        private static void SetCombinedGift(OutboundCombinedDto result, List<OutboundOrderLineDto> lines)
        {
            if (lines.All(x => !x.GiftRuleApplied)) return;
            if (lines.Any(x => !x.GiftRuleApplied))
                throw new InvalidOperationException("历史单与新规则单不能联合锁定，请分别处理。");
            var first = lines[0];
            if (lines.Any(x => x.PcsPerSetQtySnapshot != first.PcsPerSetQtySnapshot
                || x.PcsPerPnlQtySnapshot != first.PcsPerPnlQtySnapshot
                || x.MdProdRevSnapshot != first.MdProdRevSnapshot))
                throw new InvalidOperationException("联合单MD换算快照不一致，请联系IT查明原因。");
            result.MdProdRevSnapshot = first.MdProdRevSnapshot;
            result.PcsPerSetQtySnapshot = first.PcsPerSetQtySnapshot;
            result.PcsPerPnlQtySnapshot = first.PcsPerPnlQtySnapshot;
            if (!first.PcsPerSetQtySnapshot.HasValue || first.PcsPerSetQtySnapshot.Value <= 1) return;
            decimal totalPcs = lines.Sum(x => OutboundGiftRule.ToPcs(x.RequiredQty, x.Uom,
                x.PcsPerSetQtySnapshot, x.PcsPerPnlQtySnapshot));
            result.GiftQtyPcs = OutboundGiftRule.GiftPcs(totalPcs, first.PcsPerSetQtySnapshot);
        }
        private class CombinedDetailRow : OutboundCombinedBoxDto { public long Id {get;set;} }

        private static List<ScanItemAllocation> ReadFrozenShares(SqlConnection conn,SqlTransaction tran,long detailId,List<ScanItemRow> items)
        {
            var frozen=conn.Query<OutboundCombinedShareDto>(@"SELECT OutboundOrderLineId AS LineId,HUItemId,InnerHUId,
ProdNo AS MaterialCode,ProdRev AS MaterialRevision,DateCode,WeekSortKey,LotNo AS BatchNo,SAPBatchNo,Uom,AllocatedQty AS Qty
FROM dbo.WMS_FG_OutboundPickDetailItem WITH(UPDLOCK,HOLDLOCK) WHERE PickDetailId=@detailId",new { detailId },tran).ToList();
            if(frozen.Count==0 || frozen.Select(x=>x.HuItemId).Distinct().Count()!=items.Count)
                throw new InvalidOperationException("锁定库存明细已变化，请联系IT核查。");
            foreach(var item in items)
            {
                var shares=frozen.Where(x=>x.HuItemId==item.HuItemId).ToList();
                if(shares.Sum(x=>x.Qty)!=item.Qty || shares.Any(x=>x.InnerHuId!=item.InnerHuId || x.MaterialCode!=item.MaterialCode
                    || x.MaterialRevision!=item.MaterialRevision || x.DateCode!=item.DateCode || x.WeekSortKey!=item.WeekSortKey
                    || x.BatchNo!=item.BatchNo || x.SAPBatchNo!=item.SAPBatchNo || x.Uom!=item.Uom))
                    throw new InvalidOperationException("库存与锁定快照不一致，禁止重新分摊。");
            }
            foreach(var line in frozen.GroupBy(x=>x.LineId))
            {
                decimal remaining=conn.ExecuteScalar<decimal>(@"SELECT EffectiveRequiredQty-EffectivePickedQty
FROM dbo.WMS_FG_OutboundOrderLine WITH(UPDLOCK,HOLDLOCK) WHERE Id=@Id",new { Id=line.Key },tran);
                if(line.Sum(x=>x.Qty)>remaining) throw new InvalidOperationException("订单剩余数量与冻结计划不一致。");
            }
            return frozen.Select(x=>new ScanItemAllocation { LineId=x.LineId,AllocatedQty=x.Qty,
                Item=items.Single(i=>i.HuItemId==x.HuItemId) }).ToList();
        }
    }
}
