using System;

namespace WMS_HU_Model.Infrastructure.Database
{
    /// <summary>
    /// 数据库参数值转换工具，统一处理空白字符串。
    /// </summary>
    public static class DbParameterValue
    {
        /// <summary>
        /// 将空白字符串转换为 null，适用于 Dapper 参数。
        /// </summary>
        /// <param name="value">原始字符串。</param>
        /// <returns>非空白字符串或 null。</returns>
        public static string StringOrNull(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value;
        }

        /// <summary>
        /// 将数据库读取值转换为字符串，DBNull.Value 返回 null。
        /// </summary>
        /// <param name="value">数据库读取值。</param>
        /// <returns>字符串值或 null。</returns>
        public static string StringOrNull(object value)
        {
            return value == null || value == DBNull.Value
                ? null
                : Convert.ToString(value);
        }

        /// <summary>
        /// 将空白字符串转换为 DBNull.Value，适用于 ADO.NET 参数。
        /// </summary>
        /// <param name="value">原始字符串。</param>
        /// <returns>非空白字符串或 DBNull.Value。</returns>
        public static object StringOrDbNull(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? (object)DBNull.Value
                : value;
        }
    }
}
