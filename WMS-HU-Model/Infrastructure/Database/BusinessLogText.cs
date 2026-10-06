using System;

namespace WMS_HU_Model.Infrastructure.Database
{
    /// <summary>业务日志摘要与集成错误文案，不参与业务状态判断。</summary>
    public static class BusinessLogText
    {
        public static string Compose(string summary, string note = null)
        {
            summary = Clean(summary);
            note = Clean(note);
            if (note == summary || (note != null && note.StartsWith(summary + "；备注：", StringComparison.Ordinal)))
                return Limit(note, 500);
            return Limit(summary + (note == null ? string.Empty : "；备注：" + note), 500);
        }

        public static string Operation(string type, string note)
        {
            string summary;
            switch (type)
            {
                case "CREATE_HU": summary = "创建HU：完成"; break;
                case "ADD_ITEM": summary = "库存明细：添加完成"; break;
                case "ATTACH_HU": summary = "包装绑定：完成"; break;
                case "DETACH_HU": summary = "包装解绑：完成"; break;
                case "CLOSE_HU": summary = "上架完成：自动关闭运输托盘"; break;
                case "RECEIVE": summary = "成品入库：收货完成"; break;
                case "INBOUND_CONFIRM_REQUIRED": summary = "入库确认：等待逐箱确认"; break;
                case "INBOUND_CONFIRM": summary = "入库确认：外箱确认完成"; break;
                case "INBOUND_CONFIRM_COMPLETED": summary = "入库确认：整托确认完成，等待上架"; break;
                case "PUTAWAY": summary = "单箱上架：完成"; break;
                case "PUTAWAY_PALLET": summary = "整托上架：完成"; break;
                case "MOVE": summary = "移库：完成"; break;
                case "UNPACK_INNER": case "UNPACK_BOX": summary = "拆包：完成"; break;
                case "PACK_INNER": summary = "装箱：完成"; break;
                case "MERGE_BOX": summary = "合箱：完成"; break;
                case "REPACK_TRANSFER": summary = "转箱：完成"; break;
                case "HU_HOLD": return Reason("冻结", note);
                case "HU_RELEASE": return Reason("解除冻结", note);
                case "WEEKCODE_RECALCULATE": summary = "周码重算：完成"; break;
                case "OUTBOUND_PICK": summary = "出库拣货：完成"; break;
                case "OUTBOUND_RETURN": summary = "出库退回：完成"; break;
                case "OUTBOUND_RETURN_TO_STOCK": summary = "退回上架：完成"; break;
                case "OUTBOUND_SHIP": summary = "正式出库：完成"; break;
                default: return Limit(Clean(note), 500);
            }
            return Compose(summary, note);
        }

        public static string Move(string type, string note)
        {
            string summary;
            switch (type)
            {
                case "RECEIVE": summary = "收货定位：建立初始库位"; break;
                case "INBOUND_CONFIRM": summary = "入库确认：外箱由入库待确认转为已确认待上架"; break;
                case "PUTAWAY": summary = "上架移动：移入正式存储货位"; break;
                case "RELOCATION": summary = "移库：移入目标库位"; break;
                case "UNPACK_INNER": summary = "拆包定位：内包移入目标库位"; break;
                case "PACK_INNER": summary = "装箱定位：内包随外箱定位"; break;
                case "REPACK_TRANSFER": summary = "转箱定位：内包随目标外箱定位"; break;
                case "OUTBOUND_PICK": summary = "出库拣货：移入出库待发库位"; break;
                case "OUTBOUND_RETURN": summary = "出库退回：移入异常退回库位"; break;
                case "OUTBOUND_RETURN_TO_STOCK": summary = "退回上架：移入正式存储货位"; break;
                case "OUTBOUND_SHIP": summary = "正式出库：移入已出库库位"; break;
                case "RETURN": summary = "退回：移入目标库位"; break;
                case "STAGE": summary = "暂存：移入目标库位"; break;
                case "CORRECTION": summary = "位置修正：完成"; break;
                default: return Limit(Clean(note), 500);
            }
            return Compose(summary, note);
        }

        public static string Reason(string action, string reason)
        {
            reason = Clean(reason);
            string prefix = action + "：";
            if (reason != null && (reason.StartsWith("[MES]", StringComparison.Ordinal) || reason.StartsWith("[SAP]", StringComparison.Ordinal)))
                return Limit(reason, 500);
            return Limit(reason != null && reason.StartsWith(prefix, StringComparison.Ordinal)
                ? reason : prefix + (reason ?? "未提供原因"), 500);
        }

        public static string MesError(string reason)
        {
            return Error("MES", "成品入库失败", reason);
        }

        public static string SapReceiveError(string reason)
        {
            return Error("SAP", "出库订单接收失败", reason);
        }

        public static string SapPostingError(string reason, bool unknown = false)
        {
            return Error("SAP", unknown ? "出库过账结果未知" : "出库过账失败", reason);
        }

        private static string Error(string system, string action, string reason)
        {
            reason = Clean(reason) ?? "未提供错误原因";
            string prefix = "[" + system + "]";
            while (reason.StartsWith(prefix, StringComparison.Ordinal))
                reason = reason.Substring(prefix.Length).TrimStart();
            string heading = action + "：";
            return Limit(prefix + (reason.StartsWith(heading, StringComparison.Ordinal) ? reason : heading + reason), 1000);
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim().TrimEnd('。', '.');
        }

        private static string Limit(string value, int length)
        {
            if (value == null || value.Length <= length) return value;
            int end = length - 1;
            if (char.IsHighSurrogate(value[end - 1])) end--;
            return value.Substring(0, end) + "…";
        }
    }
}
