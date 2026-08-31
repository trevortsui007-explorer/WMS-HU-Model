using System;
using System.Collections.Generic;
using WMS_HU_Model.Models.WeekCode;

namespace WMS_HU_Model.Models.Inbound
{
    public static class FinishedGoodsInboundSnapshotValidator
    {
        public static string ValidateStructural(FinishedGoodsInboundSnapshot snapshot)
        {
            if (snapshot == null) return "Snapshot不能为空。";
            Normalize(snapshot);
            if (!Required(snapshot.SourceSystem, 30, "SourceSystem", out string error)) return error;
            if (!Required(snapshot.SourceType, 50, "SourceType", out error)) return error;
            if (!Required(snapshot.SourceEntityId, 100, "SourceEntityId", out error)) return error;
            if (!Required(snapshot.PlantCode, 20, "PlantCode", out error)) return error;
            if (!Optional(snapshot.SourceReferenceNo, 100, "SourceReferenceNo", out error)) return error;
            if (snapshot.Boxes == null || snapshot.Boxes.Count == 0) return "Boxes至少需要一箱。";
            if (snapshot.Pallet != null)
            {
                if (!Required(snapshot.Pallet.SourceEntityId, 100, "Pallet.SourceEntityId", out error)) return error;
                if (!Optional(snapshot.Pallet.PalletCode, 100, "Pallet.PalletCode", out error)) return error;
            }

            HashSet<string> boxIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> boxCodes = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> packIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> packCodes = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> scannableCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (snapshot.Pallet != null && !string.IsNullOrWhiteSpace(snapshot.Pallet.PalletCode)
                && !scannableCodes.Add(snapshot.Pallet.PalletCode.Trim())) return "Snapshot内扫码码冲突。";
            foreach (FinishedGoodsInboundBoxSnapshot box in snapshot.Boxes)
            {
                if (box == null) return "Boxes不能包含空对象。";
                if (!Required(box.SourceEntityId, 100, "Box.SourceEntityId", out error) || !boxIds.Add(box.SourceEntityId.Trim())) return error ?? "Box.SourceEntityId重复。";
                if (!Required(box.BoxCode, 100, "Box.BoxCode", out error) || !boxCodes.Add(box.BoxCode.Trim())) return error ?? "Box.BoxCode重复。";
                if (!scannableCodes.Add(box.BoxCode.Trim())) return "Snapshot内扫码码冲突。";
                if (!Optional(box.AlternateBoxCode, 100, "Box.AlternateBoxCode", out error)) return error;
                if (!string.IsNullOrWhiteSpace(box.AlternateBoxCode)
                    && !string.Equals(box.AlternateBoxCode, box.BoxCode, StringComparison.OrdinalIgnoreCase)
                    && !scannableCodes.Add(box.AlternateBoxCode.Trim())) return "Snapshot内扫码码冲突。";
                if (!Required(box.MaterialCode, 50, "Box.MaterialCode", out error)) return error;
                if (!Optional(box.MaterialRevision, 20, "Box.MaterialRevision", out error)) return error;
                if (!Required(box.Uom, 20, "Box.Uom", out error)) return error;
                if (!string.Equals(box.Uom.Trim(), "PCS", StringComparison.Ordinal)) return "Box.Uom必须为PCS。";
                if (!Optional(box.WeekRuleSource, 100, "Box.WeekRuleSource", out error)) return error;
                if (!Optional(box.WeekRuleValue, 500, "Box.WeekRuleValue", out error)) return error;
                if (box.InnerPacks == null || box.InnerPacks.Count == 0) return "每个Box至少需要一个InnerPack。";
                foreach (FinishedGoodsInboundPackSnapshot pack in box.InnerPacks)
                {
                    if (pack == null) return "InnerPacks不能包含空对象。";
                    if (!Required(pack.SourceEntityId, 100, "InnerPack.SourceEntityId", out error) || !packIds.Add(pack.SourceEntityId.Trim())) return error ?? "InnerPack.SourceEntityId重复。";
                    if (!Optional(pack.PackId, 50, "InnerPack.PackId", out error)) return error;
                    if (!string.IsNullOrWhiteSpace(pack.PackId) && !string.Equals(snapshot.SourceSystem, "MES", StringComparison.OrdinalIgnoreCase)) return "InnerPack.PackId只允许MES来源。";
                    if (pack.IsSpecialSapCharg && string.IsNullOrWhiteSpace(pack.PackId)) return "InnerPack.IsSpecialSapCharg需要PackId。";
                    if (!Required(pack.PackCode, 100, "InnerPack.PackCode", out error) || !packCodes.Add(pack.PackCode.Trim())) return error ?? "InnerPack.PackCode重复。";
                    if (!scannableCodes.Add(pack.PackCode.Trim())) return "Snapshot内扫码码冲突。";
                    if (!Optional(pack.DateCode, 50, "InnerPack.DateCode", out error)) return error;
                    if (pack.Qty <= 0 || pack.Qty != decimal.Round(pack.Qty, 4) || pack.Qty > 99999999999999.9999m) return "InnerPack.Qty必须符合DECIMAL(18,4)正数范围。";
                }
            }
            return null;
        }

        public static string ValidatePrepared(FinishedGoodsInboundSnapshot snapshot)
        {
            foreach (FinishedGoodsInboundBoxSnapshot box in snapshot.Boxes)
            foreach (FinishedGoodsInboundPackSnapshot pack in box.InnerPacks)
            {
                WeekCodeSnapshot week = pack.WeekCode;
                if (!pack.WeekResolutionSucceeded) continue;
                if (week == null || string.IsNullOrWhiteSpace(week.WeekFormatCode)) return "InnerPack未得到WeekFormatCode。";
                if (week.WeekYear < 2000 || week.WeekYear > 2199) return "InnerPack.WeekYear无效。";
                if (week.WeekNo < 1 || week.WeekNo > 53) return "InnerPack.WeekNo无效。";
                if (week.WeekSortKey != week.WeekYear * 100 + week.WeekNo) return "InnerPack.WeekSortKey无效。";
            }
            return null;
        }

        private static bool Required(string value, int length, string name, out string error)
        {
            if (string.IsNullOrWhiteSpace(value)) { error = name + "不能为空。"; return false; }
            return Optional(value, length, name, out error);
        }

        private static bool Optional(string value, int length, string name, out string error)
        {
            error = value != null && value.Trim().Length > length ? name + "长度不能超过" + length + "。" : null;
            return error == null;
        }

        private static void Normalize(FinishedGoodsInboundSnapshot snapshot)
        {
            snapshot.SourceSystem = Trim(snapshot.SourceSystem);
            snapshot.SourceType = Trim(snapshot.SourceType);
            snapshot.SourceEntityId = Trim(snapshot.SourceEntityId);
            snapshot.SourceReferenceNo = Trim(snapshot.SourceReferenceNo);
            snapshot.PlantCode = Trim(snapshot.PlantCode);
            if (snapshot.Pallet != null)
            {
                snapshot.Pallet.SourceEntityId = Trim(snapshot.Pallet.SourceEntityId);
                snapshot.Pallet.PalletCode = Trim(snapshot.Pallet.PalletCode);
            }
            if (snapshot.Boxes == null) return;
            foreach (FinishedGoodsInboundBoxSnapshot box in snapshot.Boxes)
            {
                if (box == null) continue;
                box.SourceEntityId = Trim(box.SourceEntityId);
                box.BoxCode = Trim(box.BoxCode);
                box.AlternateBoxCode = Trim(box.AlternateBoxCode);
                box.MaterialCode = Trim(box.MaterialCode);
                box.MaterialRevision = Trim(box.MaterialRevision);
                box.Uom = Trim(box.Uom);
                box.WeekRuleSource = Trim(box.WeekRuleSource);
                box.WeekRuleValue = Trim(box.WeekRuleValue);
                if (box.InnerPacks == null) continue;
                foreach (FinishedGoodsInboundPackSnapshot pack in box.InnerPacks)
                {
                    if (pack == null) continue;
                    pack.SourceEntityId = Trim(pack.SourceEntityId);
                    pack.PackId = Trim(pack.PackId);
                    pack.PackCode = Trim(pack.PackCode);
                    // DateCode is a source fact. Preserve null, empty, and whitespace for canonical hashing and audit.
                }
            }
        }

        private static string Trim(string value)
        {
            return value == null ? null : value.Trim();
        }
    }
}
