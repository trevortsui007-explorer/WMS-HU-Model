using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using WMS_HU_Model.Models.Inbound;

namespace WMS_HU_Model.Infrastructure.Inbound
{
    public static class FinishedGoodsInboundSnapshotHasher
    {
        public const string HashVersion = "FGI_V2";
        public const string LegacyHashVersion = "FGI_V1";

        public static string GetHashVersion(FinishedGoodsInboundSnapshot snapshot)
        {
            return UsesSapChargFacts(snapshot) ? HashVersion : LegacyHashVersion;
        }

        public static string CreateHash(FinishedGoodsInboundSnapshot snapshot)
        {
            StringBuilder text = new StringBuilder();
            Begin(text, "RECEIPT", 4); AddString(text, snapshot.SourceSystem); AddString(text, snapshot.SourceType); AddString(text, snapshot.SourceEntityId); AddString(text, snapshot.PlantCode);
            if (snapshot.Pallet == null) Begin(text, "PALLET", 0); else { Begin(text, "PALLET", 2); AddString(text, snapshot.Pallet.SourceEntityId); AddString(text, snapshot.Pallet.PalletCode); }
            FinishedGoodsInboundBoxSnapshot[] boxes = snapshot.Boxes.OrderBy(x => x.SourceEntityId, StringComparer.Ordinal).ToArray();
            Begin(text, "BOX_COLLECTION", boxes.Length);
            foreach (FinishedGoodsInboundBoxSnapshot box in boxes)
            {
                Begin(text, "BOX", 8);
                AddString(text, box.SourceEntityId); AddString(text, box.BoxCode); AddString(text, box.AlternateBoxCode); AddString(text, box.MaterialCode); AddString(text, box.MaterialRevision); AddString(text, box.Uom); AddString(text, box.WeekRuleSource); AddString(text, box.WeekRuleValue);
                FinishedGoodsInboundPackSnapshot[] packs = box.InnerPacks.OrderBy(x => x.SourceEntityId, StringComparer.Ordinal).ToArray();
                Begin(text, "INNER_COLLECTION", packs.Length);
                foreach (FinishedGoodsInboundPackSnapshot pack in packs)
                {
                    bool chargFacts = !string.IsNullOrWhiteSpace(pack.PackId) || !string.IsNullOrWhiteSpace(pack.SapBatchNo) || pack.IsSpecialSapCharg;
                    Begin(text, "INNER", chargFacts ? 8 : 5);
                    AddString(text, pack.SourceEntityId);
                    if (chargFacts) { AddString(text, pack.PackId); AddString(text, pack.SapBatchNo); AddBool(text, pack.IsSpecialSapCharg); }
                    AddString(text, pack.PackCode); AddDecimal(text, pack.Qty); AddRawString(text, pack.DateCode); AddDateTime(text, pack.PackCreatedTime);
                    Begin(text, "ITEM_COLLECTION", 1);
                    Begin(text, "ITEM", 0);
                }
            }
            using (SHA256 sha256 = SHA256.Create()) return BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", string.Empty);
        }

        private static bool UsesSapChargFacts(FinishedGoodsInboundSnapshot snapshot)
        {
            return snapshot != null && snapshot.Boxes != null
                && snapshot.Boxes.Where(x => x != null && x.InnerPacks != null)
                    .SelectMany(x => x.InnerPacks)
                    .Any(x => x != null && (!string.IsNullOrWhiteSpace(x.PackId) || !string.IsNullOrWhiteSpace(x.SapBatchNo) || x.IsSpecialSapCharg));
        }

        public static string CreateMoveRequestId(FinishedGoodsInboundSnapshot snapshot, string rootHuType, string rootSourceEntityId)
        {
            return "FGI-MOVE-" + CreateMoveDigest(snapshot, rootHuType, rootSourceEntityId);
        }

        public static string CreateMoveNo(FinishedGoodsInboundSnapshot snapshot, string rootHuType, string rootSourceEntityId)
        {
            return "FGI-MOVE-" + CreateMoveDigest(snapshot, rootHuType, rootSourceEntityId).Substring(0, 41);
        }

        private static string CreateMoveDigest(FinishedGoodsInboundSnapshot snapshot, string rootHuType, string rootSourceEntityId)
        {
            StringBuilder input = new StringBuilder();
            Begin(input, "MOVE_REQUEST", 6);
            AddString(input, snapshot.SourceSystem);
            AddString(input, snapshot.SourceType);
            AddString(input, snapshot.SourceEntityId);
            AddString(input, rootHuType);
            AddString(input, rootSourceEntityId);
            AddString(input, "RECEIVE");
            using (SHA256 sha256 = SHA256.Create())
                return BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes(input.ToString()))).Replace("-", string.Empty);
        }

        private static void Begin(StringBuilder text, string type, int count)
        {
            text.Append('[').Append(type).Append(':').Append(count.ToString(CultureInfo.InvariantCulture)).Append(']');
        }

        private static void AddString(StringBuilder text, string value)
        {
            AddTypedString(text, value == null ? null : value.Trim());
        }

        private static void AddRawString(StringBuilder text, string value)
        {
            AddTypedString(text, value);
        }

        private static void AddTypedString(StringBuilder text, string value)
        {
            if (value == null)
            {
                text.Append("S:N;");
                return;
            }
            text.Append("S:V:").Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value).Append(';');
        }

        private static void AddDecimal(StringBuilder text, decimal value)
        {
            string normalized = value.ToString("0.#############################", CultureInfo.InvariantCulture);
            text.Append("D:").Append(normalized.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(normalized).Append(';');
        }

        private static void AddBool(StringBuilder text, bool value)
        {
            text.Append(value ? "B:1;" : "B:0;");
        }

        private static void AddDateTime(StringBuilder text, DateTime? value)
        {
            if (!value.HasValue)
            {
                text.Append("T:N;");
                return;
            }
            string normalized = value.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture);
            text.Append("T:V:").Append(normalized.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(normalized).Append(';');
        }
    }
}
