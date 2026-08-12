using System.Collections.Generic;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Outbound;

namespace WMS_HU_Model.Services
{
    /// <summary>
    /// 出库与拣货服务接口
    /// </summary>
    public interface IOutboundService
    {
        /// <summary>
        /// 查询符合出库条件的待拣货队列列表
        /// </summary>
        /// <param name="query">队列查询条件</param>
        /// <returns>待拣货队列明细</returns>
        OperationResult<List<OutboundQueueItemDto>> GetQueue(OutboundQueueQuery query);

        /// <summary>
        /// 按 FIFO 规则自动拣选下一个符合条件的外箱
        /// </summary>
        /// <param name="request">拣货请求参数</param>
        /// <returns>拣货执行结果 DTO</returns>
        OperationResult<PickResultDto> PickNext(PickNextRequest request);

        OperationResult<OutboundBatchPlanDto> PlanBatch(OutboundBatchPlanRequest request);

        OperationResult<PickBatchResultDto> PickBatch(PickBatchRequest request);
    }
}