using System;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using WMS_HU_Model.Models.Outbound.Integration;

namespace WMS_HU_Model.Infrastructure.Sap
{
    public static class SapOutboundPayloadSerializer
    {
        public static string Serialize(SapOutboundPostingPayload payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));
            return JsonConvert.SerializeObject(payload, Formatting.None);
        }

        public static string ComputeHash(string payloadJson)
        {
            if (payloadJson == null)
                throw new ArgumentNullException(nameof(payloadJson));
            using (SHA256 sha256 = SHA256.Create())
            {
                return BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes(payloadJson)))
                    .Replace("-", string.Empty);
            }
        }
    }
}
