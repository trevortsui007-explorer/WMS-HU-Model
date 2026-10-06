using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WMS_HU_Model.Infrastructure.Sap
{
    public static class SapPayloadCanonicalizer
    {
        public static bool TryCreate(string rawJson, out JObject payload, out string hash, out string error)
        {
            payload = null;
            hash = null;
            error = null;
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                error = "SAP请求不能为空。";
                return false;
            }
            try
            {
                using (JsonTextReader reader = new JsonTextReader(new System.IO.StringReader(rawJson)))
                {
                    reader.FloatParseHandling = FloatParseHandling.Decimal;
                    reader.DateParseHandling = DateParseHandling.None;
                    JToken token = JToken.ReadFrom(reader);
                    if (reader.Read())
                        throw new FormatException("SAP请求只能包含一个根JSON值。");
                    payload = token as JObject;
                    if (payload == null)
                    {
                        error = "SAP请求必须是JSON Object。";
                        return false;
                    }
                    string canonical = WriteToken(payload, true);
                    using (SHA256 sha256 = SHA256.Create())
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(canonical);
                        hash = BitConverter.ToString(sha256.ComputeHash(bytes)).Replace("-", string.Empty);
                    }
                    return true;
                }
            }
            catch (Exception ex) when (ex is JsonReaderException || ex is FormatException || ex is OverflowException)
            {
                error = "SAP请求JSON或数值格式无效。";
                return false;
            }
        }

        public static string ExtractDeliveryNo(JObject payload)
        {
            JToken token = payload.GetValue("VBELN", StringComparison.Ordinal);
            string value = token == null || token.Type != JTokenType.String ? null : token.Value<string>();
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string WriteToken(JToken token, bool isRootObject)
        {
            if (token.Type == JTokenType.Object)
            {
                JObject obj = (JObject)token;
                return "{" + string.Join(",", obj.Properties()
                    .OrderBy(x => x.Name, StringComparer.Ordinal)
                    .Select(x => JsonConvert.ToString(x.Name) + ":" + WriteToken(x.Value, isRootObject && x.Name == "T_ITEM"))) + "}";
            }
            if (token.Type == JTokenType.Array)
            {
                JArray array = (JArray)token;
                JToken[] values = isRootObject
                    ? array.OrderBy(ExtractPosnr, StringComparer.Ordinal).ToArray()
                    : array.ToArray();
                return "[" + string.Join(",", values.Select(x => WriteToken(x, false))) + "]";
            }
            if (token.Type == JTokenType.Integer)
                return Convert.ToString(((JValue)token).Value, CultureInfo.InvariantCulture);
            if (token.Type == JTokenType.Float)
            {
                object value = ((JValue)token).Value;
                decimal number;
                if (!(value is decimal))
                    throw new FormatException("浮点数必须以Decimal解析。");
                number = (decimal)value;
                return number.ToString("0.#############################", CultureInfo.InvariantCulture);
            }
            if (token.Type == JTokenType.String)
                return JsonConvert.ToString(token.Value<string>());
            if (token.Type == JTokenType.Boolean)
                return token.Value<bool>() ? "true" : "false";
            if (token.Type == JTokenType.Null)
                return "null";
            throw new FormatException("SAP请求包含不支持的JSON Token。");
        }

        private static string ExtractPosnr(JToken token)
        {
            JObject item = token as JObject;
            JToken posnr = item == null ? null : item.GetValue("POSNR", StringComparison.Ordinal);
            return posnr != null && posnr.Type == JTokenType.String ? posnr.Value<string>() : string.Empty;
        }
    }
}
