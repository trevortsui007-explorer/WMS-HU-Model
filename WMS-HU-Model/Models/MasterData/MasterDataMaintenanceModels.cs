using System;
using System.Collections.Generic;

namespace WMS_HU_Model.Models.MasterData
{
    public class WarehouseQuery
    {
        public string WarehouseCode { get; set; }
        public string WarehouseName { get; set; }
        public bool? IsActive { get; set; }
    }

    public class WarehouseDto
    {
        public long Id { get; set; }
        public string WarehouseCode { get; set; }
        public string WarehouseName { get; set; }
        public string WarehouseType { get; set; }
        public bool IsActive { get; set; }
        public string Remark { get; set; }
        public int ActiveLocationCount { get; set; }
        public int CurrentHuCount { get; set; }
        public int ActiveMappingCount { get; set; }
        public DateTime CreateTime { get; set; }
        public string ModifyUser { get; set; }
        public DateTime? ModifyTime { get; set; }
    }

    public class WarehouseCreateRequest
    {
        public string WarehouseCode { get; set; }
        public string WarehouseName { get; set; }
        public string WarehouseType { get; set; }
        public string Remark { get; set; }
        public string Operator { get; set; }
    }

    public class WarehouseUpdateRequest
    {
        public string WarehouseName { get; set; }
        public string WarehouseType { get; set; }
        public string Remark { get; set; }
        public string Operator { get; set; }
    }

    public class WarehousePlantQuery
    {
        public string WarehouseCode { get; set; }
        public string PlantCode { get; set; }
        public bool? IsActive { get; set; }
    }

    public class WarehousePlantDto
    {
        public long Id { get; set; }
        public string WarehouseCode { get; set; }
        public string WarehouseName { get; set; }
        public string PlantCode { get; set; }
        public string SapStorageLocation { get; set; }
        public bool IsActive { get; set; }
        public string Remark { get; set; }
        public DateTime CreateTime { get; set; }
    }

    public class WarehousePlantCreateRequest
    {
        public string WarehouseCode { get; set; }
        public string PlantCode { get; set; }
        public string SapStorageLocation { get; set; }
        public string Remark { get; set; }
        public string Operator { get; set; }
    }

    public class WarehousePlantUpdateRequest
    {
        public string SapStorageLocation { get; set; }
        public string Remark { get; set; }
        public string Operator { get; set; }
    }

    public class MasterDataStatusRequest
    {
        public string Operator { get; set; }
    }

    public class MasterDataOptionDto
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; }
    }

    public class MasterDataOptionsDto
    {
        public List<string> WarehouseTypes { get; set; } = new List<string>();
        public List<string> LocationTypes { get; set; } = new List<string>();
        public List<MasterDataOptionDto> Warehouses { get; set; } = new List<MasterDataOptionDto>();
        public List<MasterDataOptionDto> Zones { get; set; } = new List<MasterDataOptionDto>();
    }

    public class LocationMaintenanceQuery
    {
        public string WarehouseCode { get; set; }
        public string LocationCode { get; set; }
        public string LocationTypeCode { get; set; }
        public string MasterStatus { get; set; }
    }
}
