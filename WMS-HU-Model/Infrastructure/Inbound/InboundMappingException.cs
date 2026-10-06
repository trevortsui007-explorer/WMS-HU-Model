using System;

namespace WMS_HU_Model.Infrastructure.Inbound
{
    public sealed class InboundMappingException : Exception
    {
        public InboundMappingException(string message) : base(message)
        {
        }
    }
}
