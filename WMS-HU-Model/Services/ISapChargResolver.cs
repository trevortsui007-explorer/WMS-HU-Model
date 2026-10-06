namespace WMS_HU_Model.Services
{
    public interface ISapChargResolver
    {
        string Resolve(string plantCode, string packId, bool isSpecialOrderType);
    }
}
