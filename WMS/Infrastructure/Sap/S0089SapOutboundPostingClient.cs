using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using WMS_HU_Model.Models.Outbound.Integration;
using WMS_HU_Model.Services;

namespace WMS.Infrastructure.Sap
{
    public class S0089SapOutboundPostingClient : ISapOutboundPostingClient
    {
        private const string EndpointKey = "Sap.S0089.Endpoint";
        private const string TimeoutKey = "Sap.S0089.TimeoutMs";
        private const string UsernameKey = "ApiAccountSAP";
        private const string PasswordKey = "ApiPasswordSAP";
        private const int DefaultTimeoutMs = 100000;

        public SapOutboundTransportConfigurationResult ValidateConfiguration()
        {
            string endpoint = Normalize(ConfigurationManager.AppSettings[EndpointKey]);
            string timeoutText = Normalize(ConfigurationManager.AppSettings[TimeoutKey]);
            string username = Normalize(ConfigurationManager.AppSettings[UsernameKey]);
            string password = Normalize(ConfigurationManager.AppSettings[PasswordKey]);
            Uri endpointUri;
            int timeout = DefaultTimeoutMs;
            if (endpoint == null || !Uri.TryCreate(endpoint, UriKind.Absolute, out endpointUri)
                || (endpointUri.Scheme != Uri.UriSchemeHttp && endpointUri.Scheme != Uri.UriSchemeHttps))
                return Invalid("Sap.S0089.Endpoint必须为有效的HTTP/HTTPS绝对地址。");
            if (timeoutText != null && (!int.TryParse(timeoutText, out timeout) || timeout <= 0))
                return Invalid("Sap.S0089.TimeoutMs必须为正整数。");
            if (username == null) return Invalid("ApiAccountSAP不能为空。");
            if (password == null) return Invalid("ApiPasswordSAP不能为空。");
            return new SapOutboundTransportConfigurationResult
            {
                IsValid = true,
                Configuration = new SapOutboundTransportConfiguration
                {
                    Endpoint = endpointUri.AbsoluteUri,
                    Username = username,
                    Password = password,
                    TimeoutMs = timeout
                }
            };
        }

        public SapOutboundTransportResult Post(string payloadJson, SapOutboundTransportConfiguration configuration)
        {
            DateTime requestTime = DateTime.Now;
            Stopwatch stopwatch = Stopwatch.StartNew();
            SapOutboundTransportResult result = new SapOutboundTransportResult
            {
                InvocationStarted = false,
                RequestTime = requestTime,
                ResultStatus = SapOutboundPostingStatuses.Unknown
            };
            try
            {
                byte[] body = Encoding.UTF8.GetBytes(payloadJson ?? string.Empty);
                HttpWebRequest request = WebRequest.CreateHttp(configuration.Endpoint);
                request.Method = "POST";
                request.ContentType = "application/json; charset=utf-8";
                request.Accept = "application/json";
                string credentials = configuration.Username + ":" + configuration.Password;
                request.Headers[HttpRequestHeader.Authorization] = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));
                request.Timeout = configuration.TimeoutMs;
                request.ReadWriteTimeout = configuration.TimeoutMs;
                request.ContentLength = body.Length;
                using (Stream stream = request.GetRequestStream())
                {
                    result.InvocationStarted = true;
                    stream.Write(body, 0, body.Length);
                }
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    result.HttpStatusCode = (int)response.StatusCode;
                    using (StreamReader reader = new StreamReader(response.GetResponseStream() ?? Stream.Null, Encoding.UTF8))
                        result.ResponseJson = reader.ReadToEnd();
                }
                ParseBusinessResponse(result);
            }
            catch (WebException ex)
            {
                result.ExceptionType = ex.GetType().FullName;
                result.ExceptionMessage = ex.Message;
                HttpWebResponse response = ex.Response as HttpWebResponse;
                if (response != null)
                {
                    using (response)
                    {
                        result.HttpStatusCode = (int)response.StatusCode;
                        using (StreamReader reader = new StreamReader(response.GetResponseStream() ?? Stream.Null, Encoding.UTF8))
                            result.ResponseJson = reader.ReadToEnd();
                    }
                }
                if (IsAuthenticationRejected(result.HttpStatusCode))
                {
                    result.ResultStatus = SapOutboundPostingStatuses.Failed;
                    result.ExceptionType = "SAP_AUTHENTICATION_REJECTED";
                    result.ExceptionMessage = "SAP认证失败，HTTP状态码" + result.HttpStatusCode.Value + "。";
                }
                else if (result.InvocationStarted && !string.IsNullOrWhiteSpace(result.ResponseJson))
                    ParseBusinessResponse(result);
                if (result.ResultStatus != SapOutboundPostingStatuses.Success
                    && result.ResultStatus != SapOutboundPostingStatuses.Failed)
                {
                    result.ResultStatus = result.InvocationStarted
                        ? SapOutboundPostingStatuses.Unknown
                        : SapOutboundPostingStatuses.Failed;
                }
            }
            catch (Exception ex)
            {
                result.ExceptionType = ex.GetType().FullName;
                result.ExceptionMessage = ex.Message;
                result.ResultStatus = result.InvocationStarted
                    ? SapOutboundPostingStatuses.Unknown
                    : SapOutboundPostingStatuses.Failed;
            }
            finally
            {
                stopwatch.Stop();
                result.DurationMs = stopwatch.ElapsedMilliseconds;
                result.ResponseTime = DateTime.Now;
            }
            return result;
        }

        private static bool IsAuthenticationRejected(int? httpStatusCode)
        {
            return httpStatusCode == (int)HttpStatusCode.Unauthorized || httpStatusCode == (int)HttpStatusCode.Forbidden;
        }

        private static void ParseBusinessResponse(SapOutboundTransportResult result)
        {
            if (string.IsNullOrWhiteSpace(result.ResponseJson))
            {
                result.ResultStatus = SapOutboundPostingStatuses.Unknown;
                result.ExceptionType = "EMPTY_RESPONSE";
                result.ExceptionMessage = "SAP响应为空。";
                return;
            }
            try
            {
                SapResponse response = JsonConvert.DeserializeObject<SapResponse>(result.ResponseJson);
                result.SapCode = response == null ? null : response.Code;
                result.SapMessage = response == null ? null : response.Info;
                if (string.Equals(result.SapCode, "S", StringComparison.Ordinal))
                    result.ResultStatus = SapOutboundPostingStatuses.Success;
                else if (string.Equals(result.SapCode, "E", StringComparison.Ordinal))
                    result.ResultStatus = SapOutboundPostingStatuses.Failed;
                else
                    result.ResultStatus = SapOutboundPostingStatuses.Unknown;
            }
            catch (JsonException ex)
            {
                result.ResultStatus = SapOutboundPostingStatuses.Unknown;
                result.ExceptionType = ex.GetType().FullName;
                result.ExceptionMessage = ex.Message;
            }
        }

        private static SapOutboundTransportConfigurationResult Invalid(string message)
        {
            return new SapOutboundTransportConfigurationResult { IsValid = false, Message = message };
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private class SapResponse
        {
            [JsonProperty("code")]
            public string Code { get; set; }

            [JsonProperty("info")]
            public string Info { get; set; }
        }
    }
}
