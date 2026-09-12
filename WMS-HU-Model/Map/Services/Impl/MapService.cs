using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WMS_HU_Model.Map.Dtos;
using WMS_HU_Model.Map.Import;
using WMS_HU_Model.Map.Models;
using WMS_HU_Model.Map.Repositories;
using WMS_HU_Model.Map.Validation;
using WMS_HU_Model.Models.Common;

namespace WMS_HU_Model.Map.Services.Impl
{
    public class MapService : IMapService
    {
        private readonly IMapRepository _repository;
        private readonly MapValidationService _validation;
        private readonly MapExcelImportService _import;

        public MapService(IMapRepository repository, MapValidationService validation, MapExcelImportService import)
        {
            _repository = repository;
            _validation = validation;
            _import = import;
        }

        public List<MapLayoutDto> GetLayouts(string warehouseCode)
        {
            string normalized = NormalizeCode(warehouseCode);
            return _repository.GetLayouts(normalized).Select(ToDto).ToList();
        }

        public OperationResult<MapLayoutDetailDto> GetLayout(long layoutId)
        {
            MapLayout layout = _repository.GetLayout(layoutId);
            if (layout == null) return OperationResult<MapLayoutDetailDto>.Fail("地图布局不存在。");
            return OperationResult<MapLayoutDetailDto>.Success(ToDetail(layout, _repository.GetObjects(layoutId)));
        }

        public OperationResult<MapLayoutDetailDto> GetRuntimeLayout(string warehouseCode)
        {
            string normalized = NormalizeCode(warehouseCode);
            if (normalized == null) return OperationResult<MapLayoutDetailDto>.Fail("WarehouseCode 不能为空。");
            MapLayout layout = _repository.GetRuntimeLayout(normalized);
            if (layout == null) return OperationResult<MapLayoutDetailDto>.Fail("仓库没有 Active PUBLISHED 地图。");
            return OperationResult<MapLayoutDetailDto>.Success(ToDetail(layout, _repository.GetObjects(layout.Id)));
        }

        public OperationResult<MapLayoutDto> CreateDraft(CreateMapLayoutRequest request)
        {
            if (request == null) return OperationResult<MapLayoutDto>.Fail("请求不能为空。");
            request.WarehouseCode = NormalizeCode(request.WarehouseCode);
            request.LayoutCode = NormalizeCode(request.LayoutCode);
            request.Operator = Trim(request.Operator);
            request.LayoutName = Trim(request.LayoutName);
            if (request.WarehouseCode == null || request.LayoutCode == null || request.LayoutName == null || request.Operator == null)
                return OperationResult<MapLayoutDto>.Fail("WarehouseCode、LayoutCode、LayoutName、Operator 不能为空。");
            if (request.FloorNo == 0) request.FloorNo = 1;
            if (request.FloorNo != 1) return OperationResult<MapLayoutDto>.Fail("Phase 1 仅支持 FloorNo=1。");
            if (request.MaxX <= request.MinX) { request.MinX = 0; request.MaxX = 1200; }
            if (request.MaxY <= request.MinY) { request.MinY = 0; request.MaxY = 800; }
            long id = _repository.CreateDraftLayout(request);
            return OperationResult<MapLayoutDto>.Success(ToDto(_repository.GetLayout(id)), "DRAFT 地图已就绪。");
        }

        public OperationResult<MapValidationResultDto> Validate(long layoutId)
        {
            MapLayout layout = _repository.GetLayout(layoutId);
            if (layout == null) return OperationResult<MapValidationResultDto>.Fail("地图布局不存在。");
            MapValidationResultDto result = _validation.Validate(layout, _repository.GetObjects(layoutId), _repository.GetLocationBindings(layout.WarehouseId));
            return OperationResult<MapValidationResultDto>.Success(result);
        }

        public OperationResult<MapLayoutDetailDto> SaveObjects(long layoutId, SaveMapObjectsRequest request)
        {
            if (request == null) return OperationResult<MapLayoutDetailDto>.Fail("请求不能为空。");
            request.Upserts = request.Upserts ?? new List<MapObjectDto>();
            request.DeletedIds = request.DeletedIds ?? new List<long>();
            request.Operator = Trim(request.Operator);
            if (request.Operator == null || request.Operator.Length > 50) return OperationResult<MapLayoutDetailDto>.Fail("Operator 不能为空且长度不能超过 50。");
            MapLayout layout = _repository.GetLayout(layoutId);
            if (layout == null) return OperationResult<MapLayoutDetailDto>.Fail("地图布局不存在。");
            if (!string.Equals(layout.Status, "DRAFT", StringComparison.OrdinalIgnoreCase)) return OperationResult<MapLayoutDetailDto>.Fail("只有 DRAFT 地图允许编辑。");

            List<MapObject> existing = _repository.GetObjects(layoutId);
            Dictionary<long, MapObject> byId = existing.ToDictionary(x => x.Id);
            HashSet<long> deleted = new HashSet<long>(request.DeletedIds);
            if (deleted.Any(id => !byId.ContainsKey(id))) return OperationResult<MapLayoutDetailDto>.Fail("deletedIds 包含不属于当前地图的对象。");
            List<MapObject> upserts = request.Upserts.Select(ToModel).ToList();
            foreach (MapObject item in upserts)
            {
                if (item.Id > 0 && !byId.ContainsKey(item.Id)) return OperationResult<MapLayoutDetailDto>.Fail("upserts 包含不属于当前地图的对象。");
                if (item.Id > 0) item.StyleJson = byId[item.Id].StyleJson;
            }
            List<MapObject> proposed = existing.Where(x => !deleted.Contains(x.Id) && !upserts.Any(y => y.Id > 0 && y.Id == x.Id)).Concat(upserts).ToList();
            MapValidationResultDto validation = _validation.Validate(layout, proposed, _repository.GetLocationBindings(layout.WarehouseId));
            if (validation.Summary.ErrorCount > 0) return OperationResult<MapLayoutDetailDto>.Fail(string.Join("；", validation.Issues.Where(x => x.Severity == "ERROR").Select(x => x.Code + ": " + x.Message)));
            _repository.SaveObjects(layoutId, upserts, request.DeletedIds, request.Operator);
            return GetLayout(layoutId);
        }

        public OperationResult<MapImportResultDto> ImportExcel(long layoutId, Stream stream, string fileName, string operatorName)
        {
            MapLayout layout = _repository.GetLayout(layoutId);
            if (layout == null) return OperationResult<MapImportResultDto>.Fail("地图布局不存在。");
            if (!string.Equals(layout.Status, "DRAFT", StringComparison.OrdinalIgnoreCase)) return OperationResult<MapImportResultDto>.Fail("只有 DRAFT 地图允许导入。");
            operatorName = Trim(operatorName);
            if (operatorName == null || operatorName.Length > 50) return OperationResult<MapImportResultDto>.Fail("Operator 不能为空且长度不能超过 50。");

            List<MapObject> existing = _repository.GetObjects(layoutId);
            if (existing.Count > 0) return OperationResult<MapImportResultDto>.Fail("MAP_ALREADY_HAS_OBJECTS：当前草稿已经存在地图对象，重新导入可能覆盖人工调整。");

            MapExcelParseResult parsed = _import.Parse(stream, fileName);
            List<MapValidationIssueDto> issues = parsed.Issues;
            List<MapLocationBinding> locations = _repository.GetLocationBindings(layout.WarehouseId);
            Dictionary<string, MapLocationBinding> locationByCode = locations.ToDictionary(x => x.LocationCode, StringComparer.OrdinalIgnoreCase);
            List<MapObject> objects = new List<MapObject>();
            Dictionary<string, string> importedSources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int matched = 0;
            long temporaryGroupId = -1;

            foreach (MapImportCandidate candidate in parsed.Candidates)
            {
                List<ExcelSourceRect> childRects = MapExcelImportService.Split(candidate.Source, candidate.LocationCodes.Count);
                List<MapObject> children = new List<MapObject>();
                for (int index = 0; index < candidate.LocationCodes.Count; index++)
                {
                    string code = candidate.LocationCodes[index];
                    string originalSource;
                    if (importedSources.TryGetValue(code, out originalSource))
                    {
                        issues.Add(Issue("WARNING", "SOURCE_DUPLICATE_LOCATION", code, code, "货位来源重复，保留 " + originalSource + "，忽略 " + candidate.Source.Reference + "。"));
                        continue;
                    }
                    MapLocationBinding location;
                    if (!locationByCode.TryGetValue(code, out location)) { issues.Add(Issue("WARNING", "SOURCE_LOCATION_NOT_FOUND", code, code, "Excel 货位在当前仓库 WMS_FG_Location 中不存在：" + candidate.Source.Reference)); continue; }
                    if (location.IsVirtual) { issues.Add(Issue("ERROR", "LOCATION_VIRTUAL", code, code, "虚拟货位不会导入物理地图。")); continue; }
                    if (!location.IsActive) { issues.Add(Issue("ERROR", "LOCATION_INACTIVE", code, code, "停用货位不会导入物理地图。")); continue; }
                    string objectType = MapValidationService.ObjectTypeForLocation(location.LocationTypeCode);
                    if (objectType == null) { issues.Add(Issue("ERROR", "LOCATION_TYPE_UNMAPPED", code, code, "货位类型没有 MapObject 映射。")); continue; }
                    importedSources.Add(code, candidate.Source.Reference);
                    children.Add(CreateImportedObject(layoutId, candidate, childRects[index], location, objectType, index));
                    matched++;
                }

                if (candidate.LocationCodes.Count > 1 && children.Count > 0)
                {
                    long groupId = temporaryGroupId--;
                    objects.Add(CreateGroup(layoutId, groupId, candidate));
                    foreach (MapObject child in children) child.ParentObjectId = groupId;
                }
                objects.AddRange(children);
            }
            if (objects.Count > 0) _repository.ImportObjects(layoutId, objects, parsed.MinX, parsed.MinY, parsed.MaxX, parsed.MaxY, operatorName);
            return OperationResult<MapImportResultDto>.Success(new MapImportResultDto
            {
                ImportedObjectCount = objects.Count,
                MatchedLocationCount = matched,
                ErrorCount = issues.Count(x => x.Severity == "ERROR"),
                WarningCount = issues.Count(x => x.Severity == "WARNING"),
                Issues = issues
            });
        }

        private static MapObject CreateImportedObject(long layoutId, MapImportCandidate candidate, ExcelSourceRect rect, MapLocationBinding location, string objectType, int index)
        {
            return new MapObject
            {
                Id = 0, MapLayoutId = layoutId, ObjectCode = location.LocationCode, ObjectName = location.LocationName,
                ObjectType = objectType, BindingType = "LOCATION", BindingCode = location.LocationCode, GeometryType = "RECT",
                X = rect.X, Y = rect.Y, Z = 0, SizeX = rect.Width, SizeY = rect.Height, SizeZ = 0, RotationZ = 0,
                LayerCode = "LOCATION", SortNo = index, IsVisible = true, StyleKey = objectType,
                Remark = "Imported from Excel: " + candidate.Source.Reference + " " + candidate.Text
            };
        }

        private static MapObject CreateGroup(long layoutId, long temporaryId, MapImportCandidate candidate)
        {
            string stableCode = "GROUP-" + candidate.Expression.Replace("/", "-") + "-S" + SheetCode(candidate.Source.SheetName) + "-R" + (candidate.Source.StartRow + 1) + "C" + (candidate.Source.StartColumn + 1);
            return new MapObject
            {
                Id = temporaryId, MapLayoutId = layoutId, ObjectCode = stableCode, ObjectName = candidate.Expression,
                ObjectType = "GROUP", BindingType = "NONE", BindingCode = null, GeometryType = "RECT",
                X = candidate.X, Y = candidate.Y, Z = 0, SizeX = candidate.SizeX, SizeY = candidate.SizeY, SizeZ = 0, RotationZ = 0,
                LayerCode = "GROUP", SortNo = -1, IsVisible = true, StyleKey = "LOCATION_RANGE_GROUP",
                Remark = "Imported range group from Excel: " + candidate.Source.Reference
            };
        }

        private static string SheetCode(string sheetName)
        {
            int checksum = 17;
            foreach (char value in sheetName ?? string.Empty) checksum = (checksum * 31 + value) % 100000;
            return checksum.ToString("D5");
        }

        private static MapValidationIssueDto Issue(string severity, string code, string objectCode, string bindingCode, string message)
        {
            return new MapValidationIssueDto { Severity = severity, Code = code, ObjectCode = objectCode, BindingCode = bindingCode, Message = message };
        }

        private static MapLayoutDetailDto ToDetail(MapLayout layout, List<MapObject> objects) { return new MapLayoutDetailDto { Layout = ToDto(layout), Objects = objects.Select(ToDto).ToList() }; }
        private static MapLayoutDto ToDto(MapLayout x) { return new MapLayoutDto { Id = x.Id, WarehouseId = x.WarehouseId, WarehouseCode = x.WarehouseCode, LayoutCode = x.LayoutCode, LayoutName = x.LayoutName, FloorNo = x.FloorNo, CoordinateMode = x.CoordinateMode, CoordinateUnit = x.CoordinateUnit, MinX = x.MinX, MinY = x.MinY, MaxX = x.MaxX, MaxY = x.MaxY, SourceType = x.SourceType, VersionNo = x.VersionNo, Status = x.Status, IsActive = x.IsActive, Remark = x.Remark }; }
        private static MapObjectDto ToDto(MapObject x) { return new MapObjectDto { Id = x.Id, ParentObjectId = x.ParentObjectId, ObjectCode = x.ObjectCode, ObjectName = x.ObjectName, ObjectType = x.ObjectType, BindingType = x.BindingType, BindingCode = x.BindingCode, GeometryType = x.GeometryType, X = x.X, Y = x.Y, Z = x.Z, SizeX = x.SizeX, SizeY = x.SizeY, SizeZ = x.SizeZ, RotationZ = x.RotationZ, GeometryJson = x.GeometryJson, StyleKey = x.StyleKey, LayerCode = x.LayerCode, SortNo = x.SortNo, IsVisible = x.IsVisible, Remark = x.Remark }; }
        private static MapObject ToModel(MapObjectDto x) { return new MapObject { Id = x.Id, ParentObjectId = x.ParentObjectId, ObjectCode = Trim(x.ObjectCode), ObjectName = Trim(x.ObjectName), ObjectType = NormalizeCode(x.ObjectType), BindingType = NormalizeCode(x.BindingType), BindingCode = Trim(x.BindingCode), GeometryType = NormalizeCode(x.GeometryType), X = x.X, Y = x.Y, Z = x.Z, SizeX = x.SizeX, SizeY = x.SizeY, SizeZ = x.SizeZ, RotationZ = x.RotationZ, GeometryJson = x.GeometryJson, StyleKey = Trim(x.StyleKey), LayerCode = NormalizeCode(x.LayerCode), SortNo = x.SortNo, IsVisible = x.IsVisible, Remark = Trim(x.Remark) }; }
        private static string NormalizeCode(string value) { string result = Trim(value); return result == null ? null : result.ToUpperInvariant(); }
        private static string Trim(string value) { return string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }
    }
}
