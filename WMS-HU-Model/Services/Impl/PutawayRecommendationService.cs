using System;
using System.Collections.Generic;
using System.Linq;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Putaway;
using WMS_HU_Model.Repositories;

namespace WMS_HU_Model.Services
{
    public class PutawayRecommendationService : IPutawayRecommendationService
    {
        public const string Version = "V1.1.1_DYNAMIC_CLUSTER";
        public const int TopCount = 3;
        public const int MinClusterObservationCount = 3;

        private readonly IPutawayRecommendationRepository _repository;

        public PutawayRecommendationService(IPutawayRecommendationRepository repository)
        {
            _repository = repository;
        }

        public OperationResult<PutawayRecommendationResultDto> GetRecommendations(string warehouseCode, string boxHuCode, bool itMode)
        {
            string normalizedWarehouse = Normalize(warehouseCode);
            string normalizedBox = Normalize(boxHuCode);
            if (normalizedWarehouse == null) return OperationResult<PutawayRecommendationResultDto>.Fail("WarehouseCode不能为空。");
            if (normalizedBox == null) return OperationResult<PutawayRecommendationResultDto>.Fail("BoxHuCode不能为空。");
            if (normalizedWarehouse.Length > 30) return OperationResult<PutawayRecommendationResultDto>.Fail("WarehouseCode长度不能超过30。");
            if (normalizedBox.Length > 100) return OperationResult<PutawayRecommendationResultDto>.Fail("BoxHuCode长度不能超过100。");

            PutawayRecommendationSnapshot snapshot = _repository.LoadSnapshot(normalizedWarehouse, normalizedBox);
            PutawayRecommendationBoxSnapshot box = snapshot == null ? null : snapshot.Box;
            if (box == null) return OperationResult<PutawayRecommendationResultDto>.Fail("外箱HU不存在。");
            if (!Same(box.HuType, "OUTER_BOX")) return OperationResult<PutawayRecommendationResultDto>.Fail("当前HU不是OUTER_BOX。");
            if (!Same(box.LifecycleStatus, "ACTIVE")) return OperationResult<PutawayRecommendationResultDto>.Fail("只有ACTIVE状态外箱可获取上架推荐。");
            if (box.IsHold) return OperationResult<PutawayRecommendationResultDto>.Fail("外箱当前已冻结，不能获取上架推荐。");
            if (!box.EffectiveLocationId.HasValue) return OperationResult<PutawayRecommendationResultDto>.Fail("外箱没有有效来源货位。");
            if (!Same(box.EffectiveWarehouseCode, normalizedWarehouse)) return OperationResult<PutawayRecommendationResultDto>.Fail("外箱当前有效货位不属于指定仓库。");
            if (!Same(box.EffectiveZoneCode, "RECEIVE")) return OperationResult<PutawayRecommendationResultDto>.Fail("外箱当前不在RECEIVE收货区域，无法生成上架推荐。");
            if (!Same(box.LocationSource, "SELF") && !Same(box.LocationSource, "PARENT_PALLET"))
                return OperationResult<PutawayRecommendationResultDto>.Fail("外箱当前来源模式不支持上架推荐。");
            if (!snapshot.WarehouseAllowsPlant) return OperationResult<PutawayRecommendationResultDto>.Fail("仓库不允许存储该厂别库存。");

            List<PutawayRecommendationItemSnapshot> items = snapshot.IncomingItems ?? new List<PutawayRecommendationItemSnapshot>();
            if (items.Count == 0) return OperationResult<PutawayRecommendationResultDto>.Fail("外箱没有有效INNER_PACK物料事实，无法生成上架推荐。");

            List<string> materials = items.Select(x => Text(x.MaterialCode)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            List<string> revisions = items.Select(x => Text(x.MaterialRevision) ?? string.Empty).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (materials.Count != 1 || materials[0] == null || revisions.Count != 1)
                return OperationResult<PutawayRecommendationResultDto>.Fail("PUTAWAY_RECOMMENDATION_ITEM_CONFLICT: 外箱[" + box.HuCode + "]存在不一致的物料或版本，无法生成上架推荐。");

            string materialCode = materials[0];
            string materialRevision = revisions[0];
            DateTime? incomingWeek = EarliestWeek(items);
            PutawayRecommendationResultDto result = new PutawayRecommendationResultDto
            {
                BoxHuCode = box.HuCode,
                WarehouseCode = normalizedWarehouse,
                MaterialCode = materialCode,
                MaterialRevision = materialRevision,
                RecommendationVersion = Version,
                ITMode = itMode
            };

            List<LocationState> locations = BuildLocationStates(snapshot, box.PlantCode, materialCode, materialRevision);
            ClusterState mainCluster = SelectMainCluster(locations);
            List<CandidateState> candidates = BuildCandidates(locations, mainCluster, incomingWeek);
            List<CandidateState> ranked = Rank(candidates).Take(TopCount).ToList();
            for (int index = 0; index < ranked.Count; index++)
            {
                CandidateState candidate = ranked[index];
                result.Recommendations.Add(ToDto(candidate, mainCluster, index + 1, itMode));
            }
            result.RecommendedLocationCode = result.Recommendations.Count == 0 ? null : result.Recommendations[0].LocationCode;

            string message = result.Recommendations.Count == 0
                ? "暂无推荐，请扫描现场实际货架。"
                : "success";
            return OperationResult<PutawayRecommendationResultDto>.Success(result, message);
        }

        private static List<LocationState> BuildLocationStates(
            PutawayRecommendationSnapshot snapshot,
            string incomingPlant,
            string materialCode,
            string materialRevision)
        {
            List<PutawayRecommendationInventorySnapshot> inventory = snapshot.Inventory ?? new List<PutawayRecommendationInventorySnapshot>();
            Dictionary<long, List<InventoryBoxState>> boxesByLocation = inventory
                .GroupBy(x => x.BoxHuId)
                .Select(group => new InventoryBoxState(group.ToList(), incomingPlant, materialCode, materialRevision))
                .GroupBy(x => x.LocationId)
                .ToDictionary(group => group.Key, group => group.ToList());

            List<LocationState> results = new List<LocationState>();
            foreach (PutawayRecommendationLocationSnapshot location in snapshot.Locations ?? new List<PutawayRecommendationLocationSnapshot>())
            {
                List<InventoryBoxState> boxes;
                if (!boxesByLocation.TryGetValue(location.LocationId, out boxes)) boxes = new List<InventoryBoxState>();
                if (Same(location.PlantPolicy, "DEDICATED") && !Same(location.DedicatedPlantCode, incomingPlant)) continue;
                if (Same(location.PlantPolicy, "SAME_PLANT") && boxes.Any(x => !Same(x.PlantCode, incomingPlant))) continue;
                results.Add(new LocationState(location, boxes));
            }
            return results;
        }

        private static ClusterState SelectMainCluster(List<LocationState> locations)
        {
            List<LocationState> occupied = locations.Where(x => x.MatchingBoxCount > 0).ToList();
            if (occupied.Count == 0) return null;

            List<ClusterState> clusters = new List<ClusterState>();
            List<LocationState> withoutTopology = occupied
                .Where(x => string.IsNullOrWhiteSpace(x.Location.PhysicalRowCode) || !x.Location.PositionSequence.HasValue)
                .ToList();
            clusters.AddRange(withoutTopology.Select(x => new ClusterState(new List<LocationState> { x })));

            IEnumerable<IGrouping<string, LocationState>> rowGroups = occupied
                .Where(x => !string.IsNullOrWhiteSpace(x.Location.PhysicalRowCode) && x.Location.PositionSequence.HasValue)
                .GroupBy(x => Key(x.Location.PhysicalRackGroupCode) + "|" + Key(x.Location.PhysicalRowCode), StringComparer.OrdinalIgnoreCase);

            foreach (IGrouping<string, LocationState> rowGroup in rowGroups)
            {
                List<LocationState> ordered = rowGroup
                    .OrderBy(x => x.Location.PositionSequence.Value)
                    .ThenBy(x => x.Location.LocationCode, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                List<LocationState> current = new List<LocationState>();
                int? previousPosition = null;
                foreach (LocationState location in ordered)
                {
                    int position = location.Location.PositionSequence.Value;
                    if (previousPosition.HasValue && position != previousPosition.Value + 1)
                    {
                        clusters.Add(new ClusterState(current));
                        current = new List<LocationState>();
                    }
                    current.Add(location);
                    previousPosition = position;
                }
                if (current.Count > 0) clusters.Add(new ClusterState(current));
            }

            return clusters
                .OrderByDescending(x => x.TotalMatchingBoxCount)
                .ThenByDescending(x => x.Locations.Count)
                .ThenByDescending(x => x.ContiguousLength)
                .ThenBy(x => x.PutawayPriority)
                .ThenBy(x => x.PhysicalRowCode, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.MinPosition)
                .ThenBy(x => x.FirstLocationCode, StringComparer.OrdinalIgnoreCase)
                .First();
        }

        private static List<CandidateState> BuildCandidates(
            List<LocationState> locations,
            ClusterState mainCluster,
            DateTime? incomingWeek)
        {
            Dictionary<long, CandidateState> candidates = new Dictionary<long, CandidateState>();
            if (mainCluster == null)
            {
                AddFallbackCandidates(candidates, locations, new HashSet<long>(), incomingWeek);
            }
            else if (mainCluster.Locations.Count < MinClusterObservationCount)
            {
                if (mainCluster.Locations.Count == 1)
                    AddCandidate(candidates, Candidate(mainCluster.Locations[0], 2, "EXISTING_CLUSTER", 0, 0, incomingWeek));
                else
                {
                    foreach (LocationState location in mainCluster.Locations)
                    {
                        bool requiresFill = mainCluster.FillGap > 0 && location.MatchingBoxCount < mainCluster.MaxBoxCount;
                        AddCandidate(candidates, Candidate(location, requiresFill ? 1 : 2, requiresFill ? "CLUSTER_FILL" : "CLUSTER_OBSERVE", 0, 0, incomingWeek));
                    }
                }
                AddOperationBackupCandidates(candidates, locations, mainCluster, incomingWeek);
            }
            else if (mainCluster.FillGap > 0)
            {
                foreach (LocationState location in mainCluster.Locations)
                {
                    bool requiresFill = location.MatchingBoxCount < mainCluster.MaxBoxCount;
                    AddCandidate(candidates, Candidate(location, requiresFill ? 1 : 2, requiresFill ? "CLUSTER_FILL" : "CLUSTER_OBSERVE", 0, 0, incomingWeek));
                }
                AddOperationBackupCandidates(candidates, locations, mainCluster, incomingWeek);
            }
            else
            {
                HashSet<long> mainLocationIds = new HashSet<long>(mainCluster.Locations.Select(x => x.Location.LocationId));

                if (mainCluster.HasTopology)
                {
                    int minimum = mainCluster.MinPosition;
                    int maximum = mainCluster.MaxPosition;
                    foreach (LocationState location in locations.Where(x => x.CurrentBoxCount == 0 && SameRow(x, mainCluster)))
                    {
                        int position = location.Location.PositionSequence.Value;
                        if (position == maximum + 1)
                            AddCandidate(candidates, Candidate(location, 3, "CLUSTER_EXPANSION", 1, 0, incomingWeek));
                        else if (position == minimum - 1)
                            AddCandidate(candidates, Candidate(location, 3, "CLUSTER_EXPANSION", 1, 1, incomingWeek));
                    }

                    foreach (LocationState location in locations.Where(x => x.CurrentBoxCount == 0 && PairedRow(x, mainCluster)))
                    {
                        int distance = mainCluster.Locations.Min(x => Math.Abs(x.Location.PositionSequence.Value - location.Location.PositionSequence.Value));
                        int direction = location.Location.PositionSequence.Value >= mainCluster.ReferencePosition ? 0 : 1;
                        AddCandidate(candidates, Candidate(location, 4, "PAIRED_ROW_NEARBY", distance, direction, incomingWeek));
                    }
                }

                AddFallbackCandidates(candidates, locations, mainLocationIds, incomingWeek);
            }

            foreach (CandidateState candidate in candidates.Values)
                candidate.Reason = Reason(candidate);
            return candidates.Values.ToList();
        }

        private static void AddOperationBackupCandidates(
            Dictionary<long, CandidateState> candidates,
            List<LocationState> locations,
            ClusterState mainCluster,
            DateTime? incomingWeek)
        {
            HashSet<long> mainLocationIds = new HashSet<long>(mainCluster.Locations.Select(x => x.Location.LocationId));
            if (mainCluster.HasTopology)
            {
                int minimum = mainCluster.MinPosition;
                int maximum = mainCluster.MaxPosition;
                foreach (LocationState location in locations.Where(x => x.CurrentBoxCount == 0 && SameRow(x, mainCluster)))
                {
                    int position = location.Location.PositionSequence.Value;
                    if (position == maximum + 1)
                        AddCandidate(candidates, Candidate(location, 3, "OPERATION_BACKUP", 1, 0, incomingWeek));
                    else if (position == minimum - 1)
                        AddCandidate(candidates, Candidate(location, 3, "OPERATION_BACKUP", 1, 1, incomingWeek));
                }

                foreach (LocationState location in locations.Where(x => x.CurrentBoxCount == 0 && PairedRow(x, mainCluster)))
                {
                    int distance = mainCluster.Locations.Min(x => Math.Abs(x.Location.PositionSequence.Value - location.Location.PositionSequence.Value));
                    int direction = location.Location.PositionSequence.Value >= mainCluster.ReferencePosition ? 0 : 1;
                    AddCandidate(candidates, Candidate(location, 4, "OPERATION_BACKUP", distance, direction, incomingWeek));
                }
            }

            foreach (LocationState location in locations.Where(x => x.CurrentBoxCount == 0 && !mainLocationIds.Contains(x.Location.LocationId)))
                AddCandidate(candidates, Candidate(location, 5, "OPERATION_BACKUP", int.MaxValue, 0, incomingWeek));
        }

        private static void AddFallbackCandidates(
            Dictionary<long, CandidateState> candidates,
            List<LocationState> locations,
            HashSet<long> excludedLocationIds,
            DateTime? incomingWeek)
        {
            foreach (LocationState location in locations)
            {
                if (candidates.ContainsKey(location.Location.LocationId) || excludedLocationIds.Contains(location.Location.LocationId)) continue;
                if (location.CurrentBoxCount == 0)
                {
                    AddCandidate(candidates, Candidate(location, 5, "EMPTY", int.MaxValue, 0, incomingWeek));
                }
                else if (location.MatchingBoxCount == 0 && location.HasOnlyIncomingMaterial)
                {
                    AddCandidate(candidates, Candidate(location, 6, "SAME_MATERIAL_OTHER_REV", int.MaxValue, 0, incomingWeek));
                }
                else if (location.Location.AllowMixedMaterial && !location.HasOnlyIncomingMaterial)
                {
                    AddCandidate(candidates, Candidate(location, 7, "MIXED_FALLBACK", int.MaxValue, 0, incomingWeek));
                }
            }
        }

        private static IEnumerable<CandidateState> Rank(List<CandidateState> candidates)
        {
            return candidates
                .OrderBy(x => x.LevelPriority)
                .ThenBy(x => x.LevelPriority == 1 ? x.Location.MatchingBoxCount : int.MaxValue)
                .ThenBy(x => x.PhysicalDistance)
                .ThenBy(x => x.DirectionPriority)
                .ThenBy(x => x.Level == "CLUSTER_OBSERVE" ? x.Location.Location.PutawayPriority : int.MaxValue)
                .ThenBy(x => x.Level == "CLUSTER_OBSERVE" ? x.Location.Location.PositionSequence ?? int.MaxValue : int.MaxValue)
                .ThenBy(x => x.WeekDistance)
                .ThenByDescending(x => x.Location.Purity)
                .ThenBy(x => x.Location.Location.PutawayPriority)
                .ThenBy(x => x.Location.Location.PositionSequence ?? int.MaxValue)
                .ThenBy(x => x.Location.Location.LocationCode, StringComparer.OrdinalIgnoreCase);
        }

        private static PutawayLocationRecommendationDto ToDto(CandidateState candidate, ClusterState mainCluster, int rank, bool itMode)
        {
            return new PutawayLocationRecommendationDto
            {
                Rank = rank,
                LocationCode = candidate.Location.Location.LocationCode,
                RecommendationLevel = candidate.Level,
                Reason = candidate.Reason,
                ITReason = itMode ? DiagnosticReason(candidate, mainCluster) : null,
                CurrentBoxCount = candidate.Location.MatchingBoxCount,
                ClusterMinBoxCount = mainCluster == null ? (int?)null : mainCluster.MinBoxCount,
                ClusterMaxBoxCount = mainCluster == null ? (int?)null : mainCluster.MaxBoxCount,
                ClusterFillGap = mainCluster == null ? (int?)null : mainCluster.FillGap,
                ClusterLocationCount = mainCluster == null ? (int?)null : mainCluster.Locations.Count,
                ClusterInferenceStatus = InferenceStatus(mainCluster),
                PhysicalRackGroupCode = candidate.Location.Location.PhysicalRackGroupCode,
                PhysicalRowCode = candidate.Location.Location.PhysicalRowCode,
                PositionSequence = candidate.Location.Location.PositionSequence,
                WeekAffinity = candidate.WeekAffinity
            };
        }

        private static CandidateState Candidate(LocationState location, int priority, string level, int physicalDistance, int directionPriority, DateTime? incomingWeek)
        {
            int weekDistance = WeekDistance(incomingWeek, location.MatchingWeeks);
            return new CandidateState
            {
                Location = location,
                LevelPriority = priority,
                Level = level,
                PhysicalDistance = physicalDistance,
                DirectionPriority = directionPriority,
                WeekDistance = weekDistance,
                WeekAffinity = weekDistance == 0 ? "SAME" : weekDistance == int.MaxValue ? "UNKNOWN" : "NEAR"
            };
        }

        private static void AddCandidate(Dictionary<long, CandidateState> candidates, CandidateState candidate)
        {
            CandidateState existing;
            if (!candidates.TryGetValue(candidate.Location.Location.LocationId, out existing) || candidate.LevelPriority < existing.LevelPriority)
                candidates[candidate.Location.Location.LocationId] = candidate;
        }

        private static string Reason(CandidateState candidate)
        {
            if (candidate.Level == "CLUSTER_FILL")
                return "同料号，现存" + candidate.Location.MatchingBoxCount + "箱，优先补齐";
            if (candidate.Level == "EXISTING_CLUSTER")
                return "同料号，现存" + candidate.Location.MatchingBoxCount + "箱";
            if (candidate.Level == "CLUSTER_OBSERVE")
                return "同料号，现存" + candidate.Location.MatchingBoxCount + "箱";
            if (candidate.Level == "OPERATION_BACKUP")
                return candidate.LevelPriority == 3 ? "同排相邻空位，备用" : candidate.LevelPriority == 4 ? "邻近空位，备用" : "空货位，备用";
            if (candidate.Level == "CLUSTER_EXPANSION")
                return "同排相邻空位，优先扩展";
            if (candidate.Level == "PAIRED_ROW_NEARBY")
                return "邻近空位，备用";
            if (candidate.Level == "EMPTY") return "空货位，备用";
            if (candidate.Level == "SAME_MATERIAL_OTHER_REV") return "同料号邻近货位，备用";
            return "可用货位，备用";
        }

        private static string DiagnosticReason(CandidateState candidate, ClusterState mainCluster)
        {
            if (mainCluster == null)
                return "MainCluster=NONE; InferenceStatus=NONE; CandidateLevel=" + candidate.Level + ".";

            string prefix = "MainCluster=" + mainCluster.RangeDisplay
                + "; ClusterLocationCount=" + mainCluster.Locations.Count
                + "; MinBoxCount=" + mainCluster.MinBoxCount
                + "; MaxBoxCount=" + mainCluster.MaxBoxCount
                + "; FillGap=" + mainCluster.FillGap
                + "; InferenceStatus=" + InferenceStatus(mainCluster) + "; ";
            if (candidate.Level == "OPERATION_BACKUP")
                return prefix + "当前货位为空，仅作为OPERATION_BACKUP返回，不计入ClusterLocationCount，不表示库存簇已满。";
            if (candidate.Level == "CLUSTER_FILL")
                return prefix + "当前货位箱数低于主簇最大值，优先补齐；补齐前不进行扩展推断。";
            if (candidate.Level == "CLUSTER_EXPANSION")
                return prefix + "真实簇位不少于" + MinClusterObservationCount + "个且当前平衡，允许作为可能簇满后的相邻扩展候选。";
            if (mainCluster.Locations.Count < MinClusterObservationCount)
                return prefix + "当前真实簇位不足" + MinClusterObservationCount + "个，不进行容量推断。";
            return prefix + "CandidateLevel=" + candidate.Level + ".";
        }

        private static string InferenceStatus(ClusterState mainCluster)
        {
            if (mainCluster == null) return "NONE";
            if (mainCluster.FillGap > 0) return "FILL_REQUIRED";
            if (mainCluster.Locations.Count < MinClusterObservationCount) return "INSUFFICIENT_SAMPLE";
            return "POSSIBLY_FULL";
        }

        private static bool SameRow(LocationState location, ClusterState cluster)
        {
            return location.Location.PositionSequence.HasValue
                && Same(location.Location.PhysicalRackGroupCode, cluster.PhysicalRackGroupCode)
                && Same(location.Location.PhysicalRowCode, cluster.PhysicalRowCode);
        }

        private static bool PairedRow(LocationState location, ClusterState cluster)
        {
            return location.Location.PositionSequence.HasValue
                && !string.IsNullOrWhiteSpace(cluster.PhysicalRackGroupCode)
                && Same(location.Location.PhysicalRackGroupCode, cluster.PhysicalRackGroupCode)
                && !Same(location.Location.PhysicalRowCode, cluster.PhysicalRowCode);
        }

        private static DateTime? EarliestWeek(List<PutawayRecommendationItemSnapshot> items)
        {
            return items
                .Select(x => new { Item = x, Week = WeekStart(x.WeekYear, x.WeekNo) })
                .Where(x => x.Week.HasValue)
                .OrderBy(x => x.Item.WeekSortKey ?? int.MaxValue)
                .ThenBy(x => x.Week.Value)
                .Select(x => x.Week)
                .FirstOrDefault();
        }

        private static int WeekDistance(DateTime? incomingWeek, List<DateTime> candidateWeeks)
        {
            if (!incomingWeek.HasValue || candidateWeeks == null || candidateWeeks.Count == 0) return int.MaxValue;
            return candidateWeeks.Min(x => Math.Abs((int)(x - incomingWeek.Value).TotalDays) / 7);
        }

        private static DateTime? WeekStart(short? year, byte? week)
        {
            if (!year.HasValue || !week.HasValue || year.Value < 1 || week.Value < 1 || week.Value > 53) return null;
            DateTime januaryFourth = new DateTime(year.Value, 1, 4);
            int day = (int)januaryFourth.DayOfWeek;
            if (day == 0) day = 7;
            DateTime monday = januaryFourth.AddDays(1 - day + (week.Value - 1) * 7);
            return monday.AddDays(3).Year == year.Value ? monday : (DateTime?)null;
        }

        private static string Normalize(string value)
        {
            string text = Text(value);
            return text == null ? null : text.ToUpperInvariant();
        }

        private static string Text(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string Key(string value)
        {
            return Text(value) ?? string.Empty;
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private class InventoryBoxState
        {
            public long LocationId { get; private set; }
            public string PlantCode { get; private set; }
            public bool IsExactMatch { get; private set; }
            public bool HasOnlyIncomingMaterial { get; private set; }
            public List<DateTime> MatchingWeeks { get; private set; }

            public InventoryBoxState(List<PutawayRecommendationInventorySnapshot> rows, string incomingPlant, string materialCode, string materialRevision)
            {
                PutawayRecommendationInventorySnapshot first = rows[0];
                LocationId = first.LocationId;
                PlantCode = first.PlantCode;
                List<string> materials = rows.Select(x => Text(x.MaterialCode)).Where(x => x != null).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                List<string> dimensions = rows
                    .Where(x => Text(x.MaterialCode) != null)
                    .Select(x => Key(x.MaterialCode) + "\u001f" + Key(x.MaterialRevision))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                string incomingDimension = Key(materialCode) + "\u001f" + Key(materialRevision);
                IsExactMatch = Same(PlantCode, incomingPlant) && dimensions.Count == 1 && Same(dimensions[0], incomingDimension);
                HasOnlyIncomingMaterial = Same(PlantCode, incomingPlant) && materials.Count == 1 && Same(materials[0], materialCode);
                MatchingWeeks = IsExactMatch
                    ? rows.Select(x => WeekStart(x.WeekYear, x.WeekNo)).Where(x => x.HasValue).Select(x => x.Value).Distinct().ToList()
                    : new List<DateTime>();
            }
        }

        private class LocationState
        {
            public PutawayRecommendationLocationSnapshot Location { get; private set; }
            public List<InventoryBoxState> Boxes { get; private set; }
            public int CurrentBoxCount { get { return Boxes.Count; } }
            public int MatchingBoxCount { get { return Boxes.Count(x => x.IsExactMatch); } }
            public bool HasOnlyIncomingMaterial { get { return Boxes.Count > 0 && Boxes.All(x => x.HasOnlyIncomingMaterial); } }
            public double Purity { get { return CurrentBoxCount == 0 ? 0d : (double)MatchingBoxCount / CurrentBoxCount; } }
            public List<DateTime> MatchingWeeks { get { return Boxes.Where(x => x.IsExactMatch).SelectMany(x => x.MatchingWeeks).Distinct().ToList(); } }

            public LocationState(PutawayRecommendationLocationSnapshot location, List<InventoryBoxState> boxes)
            {
                Location = location;
                Boxes = boxes;
            }
        }

        private class ClusterState
        {
            public List<LocationState> Locations { get; private set; }
            public int TotalMatchingBoxCount { get; private set; }
            public int MinBoxCount { get; private set; }
            public int MaxBoxCount { get; private set; }
            public int FillGap { get { return MaxBoxCount - MinBoxCount; } }
            public int ContiguousLength { get; private set; }
            public int PutawayPriority { get; private set; }
            public string PhysicalRackGroupCode { get; private set; }
            public string PhysicalRowCode { get; private set; }
            public int MinPosition { get; private set; }
            public int MaxPosition { get; private set; }
            public int ReferencePosition { get; private set; }
            public string FirstLocationCode { get; private set; }
            public string RangeDisplay { get; private set; }
            public bool HasTopology { get; private set; }

            public ClusterState(List<LocationState> locations)
            {
                Locations = locations;
                TotalMatchingBoxCount = locations.Sum(x => x.MatchingBoxCount);
                MinBoxCount = locations.Min(x => x.MatchingBoxCount);
                MaxBoxCount = locations.Max(x => x.MatchingBoxCount);
                PutawayPriority = locations.Min(x => x.Location.PutawayPriority);
                PhysicalRackGroupCode = locations[0].Location.PhysicalRackGroupCode;
                PhysicalRowCode = locations[0].Location.PhysicalRowCode;
                HasTopology = locations.All(x => x.Location.PositionSequence.HasValue && !string.IsNullOrWhiteSpace(x.Location.PhysicalRowCode));
                MinPosition = HasTopology ? locations.Min(x => x.Location.PositionSequence.Value) : int.MaxValue;
                MaxPosition = HasTopology ? locations.Max(x => x.Location.PositionSequence.Value) : int.MaxValue;
                ContiguousLength = HasTopology ? MaxPosition - MinPosition + 1 : locations.Count;
                ReferencePosition = HasTopology ? (MinPosition + MaxPosition + 1) / 2 : int.MaxValue;
                FirstLocationCode = locations.Select(x => x.Location.LocationCode).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).First();
                LocationState first = locations.OrderBy(x => x.Location.PositionSequence ?? int.MaxValue).ThenBy(x => x.Location.LocationCode, StringComparer.OrdinalIgnoreCase).First();
                LocationState last = locations.OrderByDescending(x => x.Location.PositionSequence ?? int.MinValue).ThenByDescending(x => x.Location.LocationCode, StringComparer.OrdinalIgnoreCase).First();
                RangeDisplay = first.Location.LocationCode == last.Location.LocationCode
                    ? first.Location.LocationCode
                    : first.Location.LocationCode + "-" + last.Location.LocationCode;
            }
        }

        private class CandidateState
        {
            public LocationState Location { get; set; }
            public int LevelPriority { get; set; }
            public string Level { get; set; }
            public string Reason { get; set; }
            public int PhysicalDistance { get; set; }
            public int DirectionPriority { get; set; }
            public int WeekDistance { get; set; }
            public string WeekAffinity { get; set; }
        }
    }
}
