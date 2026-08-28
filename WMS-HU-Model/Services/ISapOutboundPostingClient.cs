using WMS_HU_Model.Models.Outbound.Integration;

namespace WMS_HU_Model.Services
{
    public interface ISapOutboundPostingClient
    {
        SapOutboundTransportConfigurationResult ValidateConfiguration();
        SapOutboundTransportResult Post(string payloadJson, SapOutboundTransportConfiguration configuration);
    }
}
