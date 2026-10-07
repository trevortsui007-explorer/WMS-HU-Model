-- Apply before deploying the outbound gift-rule code. Existing rows retain their legacy zero-gift semantics.
IF COL_LENGTH('dbo.WMS_FG_OutboundOrderLine', 'GiftRuleApplied') IS NULL
    ALTER TABLE dbo.WMS_FG_OutboundOrderLine ADD GiftRuleApplied bit NOT NULL
        CONSTRAINT DF_WMS_FG_OutboundOrderLine_GiftRuleApplied DEFAULT (0);
IF COL_LENGTH('dbo.WMS_FG_OutboundOrderLine', 'MdProdRevSnapshot') IS NULL
    ALTER TABLE dbo.WMS_FG_OutboundOrderLine ADD MdProdRevSnapshot nvarchar(50) NULL;
IF COL_LENGTH('dbo.WMS_FG_OutboundOrderLine', 'PcsPerSetQtySnapshot') IS NULL
    ALTER TABLE dbo.WMS_FG_OutboundOrderLine ADD PcsPerSetQtySnapshot int NULL;
IF COL_LENGTH('dbo.WMS_FG_OutboundOrderLine', 'PcsPerPnlQtySnapshot') IS NULL
    ALTER TABLE dbo.WMS_FG_OutboundOrderLine ADD PcsPerPnlQtySnapshot int NULL;

IF COL_LENGTH('dbo.WMS_FG_OutboundAllocation', 'GiftQtyPcs') IS NULL
    ALTER TABLE dbo.WMS_FG_OutboundAllocation ADD GiftQtyPcs decimal(18,4) NOT NULL
        CONSTRAINT DF_WMS_FG_OutboundAllocation_GiftQtyPcs DEFAULT (0);
IF COL_LENGTH('dbo.WMS_FG_OutboundAllocation', 'MdProdRevSnapshot') IS NULL
    ALTER TABLE dbo.WMS_FG_OutboundAllocation ADD MdProdRevSnapshot nvarchar(50) NULL;
IF COL_LENGTH('dbo.WMS_FG_OutboundAllocation', 'PcsPerSetQtySnapshot') IS NULL
    ALTER TABLE dbo.WMS_FG_OutboundAllocation ADD PcsPerSetQtySnapshot int NULL;
IF COL_LENGTH('dbo.WMS_FG_OutboundAllocation', 'PcsPerPnlQtySnapshot') IS NULL
    ALTER TABLE dbo.WMS_FG_OutboundAllocation ADD PcsPerPnlQtySnapshot int NULL;
