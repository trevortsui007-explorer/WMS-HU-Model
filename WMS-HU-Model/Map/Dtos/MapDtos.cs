using System.Collections.Generic;

namespace WMS_HU_Model.Map.Dtos
{
    public class MapLayoutDto
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
        public int VersionNo { get; set; }
        public string Status { get; set; }
        public bool IsActive { get; set; }
        public string Remark { get; set; }
    }

    public class MapObjectDto
    {
        public long Id { get; set; }
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
        public string LayerCode { get; set; }
        public int SortNo { get; set; }
        public bool IsVisible { get; set; }
        public string Remark { get; set; }
    }

    public class MapLayoutDetailDto
    {
        public MapLayoutDto Layout { get; set; }
        public List<MapObjectDto> Objects { get; set; }
    }

    public class CreateMapLayoutRequest
    {
        public string WarehouseCode { get; set; }
        public string LayoutCode { get; set; }
        public string LayoutName { get; set; }
        public int FloorNo { get; set; }
        public decimal MinX { get; set; }
        public decimal MinY { get; set; }
        public decimal MaxX { get; set; }
        public decimal MaxY { get; set; }
        public string Operator { get; set; }
        public string Remark { get; set; }
    }

    public class SaveMapObjectsRequest
    {
        public List<MapObjectDto> Upserts { get; set; }
        public List<long> DeletedIds { get; set; }
        public string Operator { get; set; }
    }

    public class MapValidationIssueDto
    {
        public string Severity { get; set; }
        public string Code { get; set; }
        public string ObjectCode { get; set; }
        public string BindingCode { get; set; }
        public string Message { get; set; }
    }

    public class MapValidationSummaryDto
    {
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public int InfoCount { get; set; }
    }

    public class MapValidationResultDto
    {
        public MapValidationSummaryDto Summary { get; set; }
        public List<MapValidationIssueDto> Issues { get; set; }
    }

    public class MapImportResultDto
    {
        public int ImportedObjectCount { get; set; }
        public int MatchedLocationCount { get; set; }
        public int WarningCount { get; set; }
        public int ErrorCount { get; set; }
        public List<MapValidationIssueDto> Issues { get; set; }
    }
}
