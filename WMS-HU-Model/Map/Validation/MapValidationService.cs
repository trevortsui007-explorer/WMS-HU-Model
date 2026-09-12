using System;
using System.Collections.Generic;
using System.Linq;
using WMS_HU_Model.Map.Dtos;
using WMS_HU_Model.Map.Models;

namespace WMS_HU_Model.Map.Validation
{
    public class MapValidationService
    {
        public MapValidationResultDto Validate(MapLayout layout, IList<MapObject> objects, IList<MapLocationBinding> locations)
        {
            List<MapValidationIssueDto> issues = new List<MapValidationIssueDto>();
            if (layout == null)
            {
                Add(issues, "ERROR", "LAYOUT_NOT_FOUND", null, null, "地图布局不存在。");
                return Result(issues);
            }

            objects = objects ?? new List<MapObject>();
            locations = locations ?? new List<MapLocationBinding>();
            Dictionary<string, MapLocationBinding> locationByCode = locations
                .GroupBy(x => x.LocationCode, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.Single(), StringComparer.OrdinalIgnoreCase);
            HashSet<long> objectIds = new HashSet<long>(objects.Where(x => x.Id > 0).Select(x => x.Id));

            foreach (IGrouping<string, MapObject> duplicate in objects.Where(x => !string.IsNullOrWhiteSpace(x.ObjectCode)).GroupBy(x => x.ObjectCode, StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1))
                Add(issues, "ERROR", "OBJECT_CODE_DUPLICATE", duplicate.Key, null, "同一地图内 ObjectCode 重复。");

            foreach (IGrouping<string, MapObject> duplicate in objects.Where(IsLocationBinding).GroupBy(x => x.BindingCode, StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1))
                Add(issues, "ERROR", "LOCATION_DUPLICATE_BINDING", duplicate.First().ObjectCode, duplicate.Key, "同一地图内一个货位只能绑定一个对象。");

            foreach (MapObject item in objects)
            {
                ValidateObject(item, objectIds, locationByCode, issues);
            }

            HashSet<string> boundCodes = new HashSet<string>(objects.Where(IsLocationBinding).Select(x => x.BindingCode), StringComparer.OrdinalIgnoreCase);
            foreach (MapLocationBinding location in locations.Where(x => x.IsActive && !x.IsVirtual && !boundCodes.Contains(x.LocationCode)))
                Add(issues, "WARNING", "UNMAPPED_PHYSICAL_LOCATION", null, location.LocationCode, "启用的物理货位尚未映射到当前地图。");

            return Result(issues);
        }

        public static string ObjectTypeForLocation(string locationTypeCode)
        {
            if (string.Equals(locationTypeCode, "DOUBLE_RACK", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(locationTypeCode, "TRIPLE_TIER_REMAINDER_RACK", StringComparison.OrdinalIgnoreCase))
                return "RACK";
            if (string.Equals(locationTypeCode, "FLOOR_STORAGE", StringComparison.OrdinalIgnoreCase)) return "FLOOR_SLOT";
            return null;
        }

        private static void ValidateObject(MapObject item, HashSet<long> objectIds, IDictionary<string, MapLocationBinding> locationByCode, IList<MapValidationIssueDto> issues)
        {
            if (string.IsNullOrWhiteSpace(item.ObjectCode)) Add(issues, "ERROR", "OBJECT_CODE_REQUIRED", null, item.BindingCode, "ObjectCode 不能为空。");
            if (item.ParentObjectId.HasValue && (!objectIds.Contains(item.ParentObjectId.Value) || item.ParentObjectId.Value == item.Id))
                Add(issues, "ERROR", "INVALID_PARENT", item.ObjectCode, item.BindingCode, "ParentObjectId 不属于当前地图或指向自身。");
            if (item.SizeX < 0 || item.SizeY < 0 || item.SizeZ < 0)
                Add(issues, "ERROR", "INVALID_GEOMETRY", item.ObjectCode, item.BindingCode, "Geometry 尺寸不能为负数。");

            if (!IsLocationBinding(item)) return;
            MapLocationBinding location;
            if (string.IsNullOrWhiteSpace(item.BindingCode) || !locationByCode.TryGetValue(item.BindingCode, out location))
            {
                Add(issues, "ERROR", "LOCATION_NOT_FOUND", item.ObjectCode, item.BindingCode, "绑定货位不存在于地图所属仓库。");
                return;
            }
            if (location.IsVirtual) Add(issues, "ERROR", "LOCATION_VIRTUAL", item.ObjectCode, item.BindingCode, "虚拟货位不能进入物理地图。");
            if (!location.IsActive) Add(issues, "ERROR", "LOCATION_INACTIVE", item.ObjectCode, item.BindingCode, "停用货位不能绑定到地图对象。");
            string expectedType = ObjectTypeForLocation(location.LocationTypeCode);
            if (expectedType != null && !string.Equals(expectedType, item.ObjectType, StringComparison.OrdinalIgnoreCase))
                Add(issues, "ERROR", "LOCATION_OBJECT_TYPE_MISMATCH", item.ObjectCode, item.BindingCode, "ObjectType 与货位类型映射不一致。");
        }

        private static bool IsLocationBinding(MapObject item)
        {
            return item != null && string.Equals(item.BindingType, "LOCATION", StringComparison.OrdinalIgnoreCase);
        }

        private static MapValidationResultDto Result(List<MapValidationIssueDto> issues)
        {
            return new MapValidationResultDto
            {
                Issues = issues,
                Summary = new MapValidationSummaryDto
                {
                    ErrorCount = issues.Count(x => x.Severity == "ERROR"),
                    WarningCount = issues.Count(x => x.Severity == "WARNING"),
                    InfoCount = issues.Count(x => x.Severity == "INFO")
                }
            };
        }

        private static void Add(IList<MapValidationIssueDto> issues, string severity, string code, string objectCode, string bindingCode, string message)
        {
            issues.Add(new MapValidationIssueDto { Severity = severity, Code = code, ObjectCode = objectCode, BindingCode = bindingCode, Message = message });
        }
    }
}
