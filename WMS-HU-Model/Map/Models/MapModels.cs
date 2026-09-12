using System;

namespace WMS_HU_Model.Map.Models
{
    public class MapLayout
    {
        public long Id { get; set; }
        public long WarehouseId { get; set; }
        public string WarehouseCode { get; set; }
        public string LayoutCode { get; set; }
        public string LayoutName { get; set; }
        public int FloorNo { get; set; }
        public string CoordinateMode { get; set; }
        public string CoordinateUnit { get; set; }
        public decimal MinX { get; set; }
        public decimal MinY { get; set; }
        public decimal MaxX { get; set; }
        public decimal MaxY { get; set; }
        public string SourceType { get; set; }
        public string BackgroundAsset { get; set; }
        public int VersionNo { get; set; }
        public string Status { get; set; }
        public bool IsActive { get; set; }
        public string Remark { get; set; }
        public DateTime CreateDt { get; set; }
        public string CreateBy { get; set; }
        public DateTime? LastEditDt { get; set; }
        public string LastEditBy { get; set; }
    }

    public class MapObject
    {
        public long Id { get; set; }
        public long MapLayoutId { get; set; }
        public long? ParentObjectId { get; set; }
        public string ObjectCode { get; set; }
        public string ObjectName { get; set; }
        public string ObjectType { get; set; }
        public string BindingType { get; set; }
        public string BindingCode { get; set; }
        public string GeometryType { get; set; }
        public decimal X { get; set; }
        public decimal Y { get; set; }
        public decimal Z { get; set; }
        public decimal SizeX { get; set; }
        public decimal SizeY { get; set; }
        public decimal SizeZ { get; set; }
        public decimal RotationZ { get; set; }
        public string GeometryJson { get; set; }
        public string StyleKey { get; set; }
        public string StyleJson { get; set; }
        public string LayerCode { get; set; }
        public int SortNo { get; set; }
        public bool IsVisible { get; set; }
        public string Remark { get; set; }
    }

    public class MapLocationBinding
    {
        public long Id { get; set; }
        public string LocationCode { get; set; }
        public string LocationName { get; set; }
        public string LocationTypeCode { get; set; }
        public bool IsVirtual { get; set; }
        public bool IsActive { get; set; }
    }
}
