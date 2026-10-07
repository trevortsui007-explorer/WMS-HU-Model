using System;

namespace WMS_HU_Model.Rules
{
    public static class OutboundGiftRule
    {
        public static decimal ToPcs(decimal quantity, string uom, int? pcsPerSet, int? pcsPerPnl)
        {
            decimal factor;
            switch ((uom ?? "").Trim().ToUpperInvariant())
            {
                case "PCS": factor = 1; break;
                case "SET":
                    if (!pcsPerSet.HasValue || pcsPerSet.Value < 1)
                        throw new InvalidOperationException("MD主数据的每SET片数无效，无法换算PCS。");
                    factor = pcsPerSet.Value;
                    break;
                case "PNL":
                    if (!pcsPerPnl.HasValue || pcsPerPnl.Value < 1)
                        throw new InvalidOperationException("MD主数据的每PNL片数无效，无法换算PCS。");
                    factor = pcsPerPnl.Value;
                    break;
                default:
                    throw new InvalidOperationException("SAP交货单单位只支持PCS、SET、PNL。");
            }
            decimal pcs = quantity * factor;
            if (pcs != decimal.Truncate(pcs))
                throw new InvalidOperationException("交货单数量换算后不是整数PCS。");
            return pcs;
        }

        public static decimal GiftPcs(decimal totalPcs, int? pcsPerSet)
        {
            return !pcsPerSet.HasValue || pcsPerSet.Value <= 1 ? 0 : totalPcs % pcsPerSet.Value;
        }
    }
}
