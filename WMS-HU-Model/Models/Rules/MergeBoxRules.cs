using System;
using System.Collections.Generic;
using System.Linq;
using WMS_HU_Model.Models.Repack;

namespace WMS_HU_Model.Rules
{
    public static class MergeBoxRules
    {
        public static string Validate(IList<string> requestedCodes, IList<MergeBoxPackFact> facts, string warehouseCode)
        {
            if (facts == null || facts.Count != requestedCodes.Count) return "存在未找到的INNER_PACK。";

            Dictionary<string, MergeBoxPackFact> byCode = facts.ToDictionary(x => x.HuCode, StringComparer.OrdinalIgnoreCase);
            foreach (string requestedCode in requestedCodes)
            {
                MergeBoxPackFact fact;
                if (!byCode.TryGetValue(requestedCode, out fact)) return "内包[" + requestedCode + "]不存在。";
                if (fact.HuType != "INNER_PACK") return "HU[" + requestedCode + "]不是INNER_PACK。";
                if (fact.LifecycleStatus != "ACTIVE") return "内包[" + requestedCode + "]不是ACTIVE状态。";
                if (fact.IsHold) return "内包[" + requestedCode + "]当前已被冻结。";
                if (fact.HasActiveParent) return "内包[" + requestedCode + "]已经存在有效父HU。";
                if (!fact.LocationId.HasValue || fact.LocationStatus != "ACTIVE" || fact.IsVirtual
                    || fact.ZoneCode != "STORAGE" || fact.ZoneStatus != "ACTIVE" || fact.WarehouseStatus != "ACTIVE")
                    return "内包[" + requestedCode + "]必须位于有效的实体STORAGE货位。";
                if (!string.Equals(fact.WarehouseCode, warehouseCode, StringComparison.OrdinalIgnoreCase))
                    return "内包[" + requestedCode + "]不属于指定仓库。";
                if (fact.ActiveItemCount == 0) return "内包[" + requestedCode + "]没有有效库存明细。";
                if (fact.MaterialCodeCount != 1 || fact.MaterialRevisionCount != 1)
                    return "INNER_PACK_ITEM_CONFLICT: 内包[" + requestedCode + "]存在不一致的物料或版本，无法执行合箱。";
            }

            MergeBoxPackFact first = byCode[requestedCodes[0]];
            if (facts.Any(x => !string.Equals(x.PlantCode, first.PlantCode, StringComparison.OrdinalIgnoreCase))) return "所选内包厂别不一致。";
            if (facts.Any(x => !string.Equals(x.WarehouseCode, first.WarehouseCode, StringComparison.OrdinalIgnoreCase))) return "所选内包仓库不一致。";
            if (facts.Any(x => !string.Equals(x.MaterialCode, first.MaterialCode, StringComparison.OrdinalIgnoreCase))) return "所选内包物料编码不一致。";
            if (facts.Any(x => !string.Equals(x.MaterialRevision ?? string.Empty, first.MaterialRevision ?? string.Empty, StringComparison.OrdinalIgnoreCase))) return "所选内包物料版本不一致。";

            try
            {
                CanonicalOuterBoxCode.GetPlantSuffix(first.PlantCode);
            }
            catch (ArgumentException)
            {
                return "当前厂别[" + first.PlantCode + "]不支持生成OUTER_BOX标准箱码。";
            }

            return null;
        }
    }
}
