using System;
using System.Collections.Generic;

namespace WMS_HU_Model.Models.Outbound
{
    public class PickBatchResultDto
    {
        public string PickBatchNo { get; set; }

        public string RequestId { get; set; }

        public string WarehouseCode { get; set; }

        public string PlantCode { get; set; }

        public string MaterialCode { get; set; }

        public decimal RequiredQty { get; set; }

        public decimal PickedQty { get; set; }

        public decimal DifferenceQty { get; set; }

        public string Uom { get; set; }

        public string ResultType { get; set; }

        public int BoxCount { get; set; }

        public DateTime PickTime { get; set; }

        public bool IsDuplicate { get; set; }


        public List<PickBatchItemResultDto>
            Boxes
        { get; set; }


        public PickBatchResultDto()
        {
            Boxes =
                new List<PickBatchItemResultDto>();
        }
    }
}