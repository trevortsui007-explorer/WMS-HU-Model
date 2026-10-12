using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
namespace Map.Application
{
    public static class LayoutRules
    {
        private static void Fail(string message) { throw new MapException("INVALID_LAYOUT", message, 422); }
        private static string Text(JToken value, string label, int max = 150)
        { if (value?.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)value) || ((string)value).Length > max) Fail(label + "无效。"); return (string)value; }
        private static double Number(JToken value, string label, double min, double max)
        { if (value == null || (value.Type != JTokenType.Integer && value.Type != JTokenType.Float)) Fail(label + "必须是数值。"); double n = (double)value; if (double.IsNaN(n) || double.IsInfinity(n) || n < min || n > max) Fail(label + "超出范围。"); return n; }
        private static int Integer(JToken value, string label, int min, int max)
        { double n = Number(value, label, min, max); if (Math.Floor(n) != n) Fail(label + "必须为整数。"); return (int)n; }
        private static JArray Array(JToken value, string label, int limit = 2000)
        { var a = value as JArray; if (a == null || a.Count > limit || a.Any(v => !(v is JObject))) Fail(label + "集合无效或超过限制。"); return a; }
        public static void Validate(string code, JObject layout)
        {
            DraftValidation.Validate(code, new SaveDraftRequest(0, null, 6, layout));
            Text(layout["layoutId"], "布局ID"); Text(layout["layoutCode"], "布局编码",64);
            if (layout["warehouseCode"] != null && layout["warehouseCode"].Type != JTokenType.Null) Text(layout["warehouseCode"],"仓库编码",20);
            var ids = new HashSet<string>(StringComparer.Ordinal); var names = new HashSet<string>(StringComparer.Ordinal); var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Action<JToken> identity = item => { string id = Text(item["id"],"对象ID",120); if (!Regex.IsMatch(id,"^[A-Za-z0-9_.:-]+$") || !ids.Add(id)) Fail("对象ID重复或格式无效。"); Text(item["name"],"对象名称"); };
            Action<JToken,double> geometry = (item,min) => { Number(item["x"],"X",-200,200); Number(item["y"],"Y",-200,200); Number(item["width"],"宽度",min,400); Number(item["depth"],"深度",min,400); Number(item["rotation"],"角度",-360000,360000); };
            Action<JToken> doors = owner =>
            {
                var list = owner["doors"] == null ? new JArray() : Array(owner["doors"],"门");
                foreach (var door in list)
                {
                    identity(door); string side = (string)door["side"], kind = (string)door["type"];
                    if (!new[]{"top","bottom","left","right"}.Contains(side) || !new[]{"inbound","outbound","general"}.Contains(kind)) Fail("门边或类型无效。");
                    double offset = Number(door["offset"],"门偏移",0,400), width = Number(door["width"],"门宽",0.5,400);
                    if (offset + width > (double)owner[side == "top" || side == "bottom" ? "width" : "depth"] + 1e-8) Fail("门超出边界。");
                }
                foreach (var group in list.GroupBy(d => (string)d["side"]))
                { double end = -1; foreach (var d in group.OrderBy(d => (double)d["offset"])) { if ((double)d["offset"] < end - 1e-8) Fail("同边的门重叠。"); end = (double)d["offset"] + (double)d["width"]; } }
            };
            var boundary = layout["boundary"]; identity(boundary); geometry(boundary,0.5);
            if ((string)boundary["id"] != "boundary" || (double)boundary["rotation"] != 0) Fail("仓库边界无效。"); doors(boundary);
            int total = 0;
            foreach (var rack in Array(layout["racks"],"货架"))
            {
                identity(rack); geometry(rack,0.3);
                if (!names.Add((string)rack["name"])) Fail("货架名称重复。");
                if ((string)rack["spaceId"] != "main" || !new[]{"FG01","FG02"}.Contains((string)rack["fgCode"])) Fail("货架空间或仓库归属无效。");
                Number(rack["width"],"列宽",0.3,5); Number(rack["depth"],"架深",0.3,5); Number(rack["levelHeight"],"层高",0.3,5);
                int cols = Integer(rack["cols"],"列数",1,30), levels = Integer(rack["levels"],"层数",1,3);
                var slots = Array(rack["slots"],"货位",90); if (slots.Count != cols * levels) Fail("货位数量与层列数不一致。");
                var cells = new HashSet<string>();
                foreach (var slot in slots)
                {
                    string id = Text(slot["id"],"货位ID",120); if (!Regex.IsMatch(id,"^[A-Za-z0-9_.:-]+$") || !ids.Add(id)) Fail("货位ID重复或格式无效。");
                    string slotCode = Text(slot["code"],"货位编码",50); if (!codes.Add(slotCode)) Fail("货位编码重复。");
                    int l = Integer(slot["level"],"货位层",1,levels), col = Integer(slot["col"],"货位列",1,cols);
                    if (!cells.Add(l + ":" + col) || ++total > 20000) Fail("货位层列重复或数量超限。");
                }
            }
            foreach (var a in Array(layout["areas"],"区域")) { identity(a); geometry(a,0.5); if (!new[]{"aisle","inbound","outbound","general"}.Contains((string)a["type"])) Fail("区域类型无效。"); }
            foreach (var room in Array(layout["rooms"],"房间")) { identity(room); geometry(room,0.5); if ((double)room["rotation"] != 0) Fail("房间暂不支持旋转。"); doors(room); }
            foreach (var fixture in Array(layout["fixtures"],"设施")) { identity(fixture); geometry(fixture,0.05); Number(fixture["height"],"设施高度",0.05,400); if (!new[]{"cabinet","pillar","other"}.Contains((string)fixture["type"])) Fail("设施类型无效。"); }
            var bindingIds = new HashSet<string>(); var virtualCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var areaIds = new HashSet<string>(layout["areas"].Select(a => (string)a["id"]));
            foreach (var binding in Array(layout["areaBindings"],"区域绑定",20000))
            {
                if (!bindingIds.Add(Text(binding["id"],"绑定ID",120)) || !areaIds.Contains(Text(binding["areaId"],"绑定区域ID",120)) || !virtualCodes.Add(Text(binding["virtualLocationCode"],"虚拟库位编码",50))) Fail("区域绑定重复或引用不存在。");
                Text(binding["displayName"],"绑定名称"); Text(binding["purpose"],"用途",50);
                if (binding["isActive"]?.Type != JTokenType.Boolean) Fail("绑定启用标志无效。");
            }
        }
        public static void CheckPublishedIdentity(JObject oldLayout, JObject next)
        {
            if ((string)oldLayout["layoutId"] != (string)next["layoutId"] || (string)oldLayout["warehouseCode"] != (string)next["warehouseCode"]) throw new MapException("LAYOUT_IDENTITY_MISMATCH","已发布布局身份和仓库范围不能改变。",409);
            var slots = next["racks"].SelectMany(r => r["slots"].Select(s => new { id=(string)s["id"], code=(string)s["code"], warehouse=(string)r["fgCode"] })).ToDictionary(s=>s.id);
            foreach (var rack in oldLayout["racks"]) foreach (var slot in rack["slots"])
            {
                string id=(string)slot["id"];
                if (!slots.ContainsKey(id) || slots[id].code != (string)slot["code"] || slots[id].warehouse != (string)rack["fgCode"])
                    throw new MapException("PUBLISHED_LOCATION_RENAME_FORBIDDEN","已发布货位不能改码、换仓或删除；位置移动和尺寸修改可以发布。",409);
            }
        }
    }
}
