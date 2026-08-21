SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.WMS_FinishedGoodsInboundReceipt', N'U') IS NOT NULL
       AND EXISTS
       (
           SELECT ColumnName, DataType, MaxLength, ColumnPrecision, ColumnScale, Nullable, IsIdentity FROM
           (
               VALUES
               ('Id','bigint',8,19,0,0,1),('SourceSystem','varchar',30,0,0,0,0),('SourceType','varchar',30,0,0,0,0),
               ('SourceEntityId','varchar',100,0,0,0,0),('SnapshotHash','char',64,0,0,0,0),('HashVersion','varchar',20,0,0,0,0),
               ('RequestId','varchar',100,0,0,1,0),('Operator','varchar',50,0,0,1,0),('Remark','nvarchar',2000,0,0,1,0),
               ('ReceiveTime','datetime2',7,23,3,0,0),('CreateTime','datetime2',7,23,3,0,0)
           ) e(ColumnName, DataType, MaxLength, ColumnPrecision, ColumnScale, Nullable, IsIdentity)
           EXCEPT
           SELECT c.name, t.name, c.max_length, c.precision, c.scale, c.is_nullable, c.is_identity
           FROM sys.columns c INNER JOIN sys.types t ON t.user_type_id = c.user_type_id
           WHERE c.object_id = OBJECT_ID(N'dbo.WMS_FinishedGoodsInboundReceipt')
       )
    BEGIN
        THROW 51015, N'HUMAN_REVIEW_REQUIRED: WMS_FinishedGoodsInboundReceipt metadata differs from 015.', 1;
    END;

    IF OBJECT_ID(N'dbo.WMS_FinishedGoodsInboundReceipt', N'U') IS NOT NULL
       AND EXISTS
       (
           SELECT c.name, t.name, c.max_length, c.precision, c.scale, c.is_nullable, c.is_identity
           FROM sys.columns c INNER JOIN sys.types t ON t.user_type_id = c.user_type_id
           WHERE c.object_id = OBJECT_ID(N'dbo.WMS_FinishedGoodsInboundReceipt')
           EXCEPT
           SELECT ColumnName, DataType, MaxLength, ColumnPrecision, ColumnScale, Nullable, IsIdentity FROM
           (VALUES ('Id','bigint',8,19,0,0,1),('SourceSystem','varchar',30,0,0,0,0),('SourceType','varchar',30,0,0,0,0),('SourceEntityId','varchar',100,0,0,0,0),('SnapshotHash','char',64,0,0,0,0),('HashVersion','varchar',20,0,0,0,0),('RequestId','varchar',100,0,0,1,0),('Operator','varchar',50,0,0,1,0),('Remark','nvarchar',2000,0,0,1,0),('ReceiveTime','datetime2',7,23,3,0,0),('CreateTime','datetime2',7,23,3,0,0)) e(ColumnName,DataType,MaxLength,ColumnPrecision,ColumnScale,Nullable,IsIdentity)
       )
    BEGIN
        THROW 51015, N'HUMAN_REVIEW_REQUIRED: WMS_FinishedGoodsInboundReceipt has unexpected metadata.', 1;
    END;

    IF OBJECT_ID(N'dbo.WMS_FinishedGoodsInboundReceipt', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.WMS_FinishedGoodsInboundReceipt
        (
            Id BIGINT IDENTITY(1,1) NOT NULL
                CONSTRAINT PK_WMS_FinishedGoodsInboundReceipt PRIMARY KEY,
            SourceSystem VARCHAR(30) NOT NULL,
            SourceType VARCHAR(30) NOT NULL,
            SourceEntityId VARCHAR(100) NOT NULL,
            SnapshotHash CHAR(64) NOT NULL,
            HashVersion VARCHAR(20) NOT NULL,
            RequestId VARCHAR(100) NULL,
            Operator VARCHAR(50) NULL,
            Remark NVARCHAR(1000) NULL,
            ReceiveTime DATETIME2(3) NOT NULL,
            CreateTime DATETIME2(3) NOT NULL
                CONSTRAINT DF_WMS_FinishedGoodsInboundReceipt_CreateTime DEFAULT (SYSDATETIME())
        );

    END;

    IF EXISTS
    (
        SELECT 1
        FROM sys.default_constraints dc
        INNER JOIN sys.columns c ON c.object_id=dc.parent_object_id AND c.column_id=dc.parent_column_id
        WHERE dc.parent_object_id=OBJECT_ID(N'dbo.WMS_FinishedGoodsInboundReceipt')
          AND (c.name<>N'CreateTime' OR LOWER(REPLACE(REPLACE(REPLACE(dc.definition, N'(', N''), N')', N''), N' ', N''))<>N'sysdatetime')
    ) OR NOT EXISTS
    (
        SELECT 1
        FROM sys.default_constraints dc
        INNER JOIN sys.columns c ON c.object_id=dc.parent_object_id AND c.column_id=dc.parent_column_id
        WHERE dc.parent_object_id=OBJECT_ID(N'dbo.WMS_FinishedGoodsInboundReceipt')
          AND c.name=N'CreateTime'
          AND LOWER(REPLACE(REPLACE(REPLACE(dc.definition, N'(', N''), N')', N''), N' ', N''))=N'sysdatetime'
    )
    BEGIN
        THROW 51015, N'HUMAN_REVIEW_REQUIRED: receipt default definition differs from 015.', 1;
    END;

    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.WMS_FinishedGoodsInboundReceipt'))
       OR EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'dbo.WMS_FinishedGoodsInboundReceipt'))
    BEGIN
        THROW 51015, N'HUMAN_REVIEW_REQUIRED: receipt check or foreign-key definition differs from 015.', 1;
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE name = N'UX_WMS_FinishedGoodsInboundReceipt_Source'
          AND object_id = OBJECT_ID(N'dbo.WMS_FinishedGoodsInboundReceipt')
    )
    BEGIN
        CREATE UNIQUE INDEX UX_WMS_FinishedGoodsInboundReceipt_Source
        ON dbo.WMS_FinishedGoodsInboundReceipt(SourceSystem, SourceType, SourceEntityId);
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.key_constraints k INNER JOIN sys.index_columns ic ON ic.object_id=k.parent_object_id AND ic.index_id=k.unique_index_id INNER JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
        WHERE k.type='PK' AND k.parent_object_id=OBJECT_ID(N'dbo.WMS_FinishedGoodsInboundReceipt') AND c.name='Id' AND ic.key_ordinal=1
    )
    BEGIN
        THROW 51015, N'HUMAN_REVIEW_REQUIRED: receipt primary key differs from 015.', 1;
    END;

    IF EXISTS
    (
        SELECT c.name, ic.key_ordinal FROM sys.key_constraints k INNER JOIN sys.index_columns ic ON ic.object_id=k.parent_object_id AND ic.index_id=k.unique_index_id INNER JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
        WHERE k.type='PK' AND k.parent_object_id=OBJECT_ID(N'dbo.WMS_FinishedGoodsInboundReceipt') AND ic.is_included_column=0
        EXCEPT SELECT * FROM (VALUES ('Id',1)) e(name,key_ordinal)
    ) OR EXISTS
    (
        SELECT * FROM (VALUES ('Id',1)) e(name,key_ordinal)
        EXCEPT
        SELECT c.name, ic.key_ordinal FROM sys.key_constraints k INNER JOIN sys.index_columns ic ON ic.object_id=k.parent_object_id AND ic.index_id=k.unique_index_id INNER JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
        WHERE k.type='PK' AND k.parent_object_id=OBJECT_ID(N'dbo.WMS_FinishedGoodsInboundReceipt') AND ic.is_included_column=0
    )
    BEGIN
        THROW 51015, N'HUMAN_REVIEW_REQUIRED: receipt primary key columns differ from 015.', 1;
    END;

    IF EXISTS
    (
        SELECT 1 FROM sys.indexes i
        WHERE i.name = N'UX_WMS_FinishedGoodsInboundReceipt_Source'
          AND i.object_id = OBJECT_ID(N'dbo.WMS_FinishedGoodsInboundReceipt')
          AND (i.is_unique <> 1 OR i.has_filter <> 0)
    )
    BEGIN
        THROW 51015, N'HUMAN_REVIEW_REQUIRED: receipt source index definition differs from 015.', 1;
    END;

    IF EXISTS
    (
        SELECT 1 FROM sys.index_columns ic
        INNER JOIN sys.indexes i ON i.object_id=ic.object_id AND i.index_id=ic.index_id
        WHERE i.name=N'UX_WMS_FinishedGoodsInboundReceipt_Source'
          AND i.object_id=OBJECT_ID(N'dbo.WMS_FinishedGoodsInboundReceipt')
          AND ic.is_included_column=1
    )
    BEGIN
        THROW 51015, N'HUMAN_REVIEW_REQUIRED: receipt source index includes differ from 015.', 1;
    END;

    IF EXISTS
    (
        SELECT c.name, ic.key_ordinal FROM sys.indexes i INNER JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id INNER JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
        WHERE i.name=N'UX_WMS_FinishedGoodsInboundReceipt_Source' AND i.object_id=OBJECT_ID(N'dbo.WMS_FinishedGoodsInboundReceipt') AND ic.is_included_column=0
        EXCEPT SELECT * FROM (VALUES ('SourceSystem',1),('SourceType',2),('SourceEntityId',3)) e(name,key_ordinal)
    ) OR EXISTS
    (
        SELECT * FROM (VALUES ('SourceSystem',1),('SourceType',2),('SourceEntityId',3)) e(name,key_ordinal)
        EXCEPT
        SELECT c.name, ic.key_ordinal FROM sys.indexes i INNER JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id INNER JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
        WHERE i.name=N'UX_WMS_FinishedGoodsInboundReceipt_Source' AND i.object_id=OBJECT_ID(N'dbo.WMS_FinishedGoodsInboundReceipt') AND ic.is_included_column=0
    )
    BEGIN
        THROW 51015, N'HUMAN_REVIEW_REQUIRED: receipt source index keys differ from 015.', 1;
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_WMS_FinishedGoodsInboundReceipt_RequestId'
          AND object_id = OBJECT_ID(N'dbo.WMS_FinishedGoodsInboundReceipt')
    )
    BEGIN
        CREATE INDEX IX_WMS_FinishedGoodsInboundReceipt_RequestId
        ON dbo.WMS_FinishedGoodsInboundReceipt(RequestId)
        WHERE RequestId IS NOT NULL;
    END;

    IF EXISTS
    (
        SELECT 1 FROM sys.indexes i
        WHERE i.name=N'IX_WMS_FinishedGoodsInboundReceipt_RequestId' AND i.object_id=OBJECT_ID(N'dbo.WMS_FinishedGoodsInboundReceipt')
          AND (i.is_unique<>0 OR i.has_filter<>1
               OR LOWER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(i.filter_definition,N' ',N''),N'[',N''),N']',N''),N'(',N''),N')',N''))<>N'requestidisnotnull')
    )
    BEGIN
        THROW 51015, N'HUMAN_REVIEW_REQUIRED: receipt request index differs from 015.', 1;
    END;

    IF EXISTS
    (
        SELECT 1 FROM sys.index_columns ic
        INNER JOIN sys.indexes i ON i.object_id=ic.object_id AND i.index_id=ic.index_id
        WHERE i.name=N'IX_WMS_FinishedGoodsInboundReceipt_RequestId'
          AND i.object_id=OBJECT_ID(N'dbo.WMS_FinishedGoodsInboundReceipt')
          AND ic.is_included_column=1
    )
    BEGIN
        THROW 51015, N'HUMAN_REVIEW_REQUIRED: receipt request index includes differ from 015.', 1;
    END;

    IF EXISTS
    (
        SELECT c.name, ic.key_ordinal FROM sys.indexes i INNER JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id INNER JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
        WHERE i.name=N'IX_WMS_FinishedGoodsInboundReceipt_RequestId' AND i.object_id=OBJECT_ID(N'dbo.WMS_FinishedGoodsInboundReceipt') AND ic.is_included_column=0
        EXCEPT SELECT * FROM (VALUES ('RequestId',1)) e(name,key_ordinal)
    ) OR EXISTS
    (
        SELECT * FROM (VALUES ('RequestId',1)) e(name,key_ordinal)
        EXCEPT
        SELECT c.name, ic.key_ordinal FROM sys.indexes i INNER JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id INNER JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
        WHERE i.name=N'IX_WMS_FinishedGoodsInboundReceipt_RequestId' AND i.object_id=OBJECT_ID(N'dbo.WMS_FinishedGoodsInboundReceipt') AND ic.is_included_column=0
    )
    BEGIN
        THROW 51015, N'HUMAN_REVIEW_REQUIRED: receipt request index keys differ from 015.', 1;
    END;

    /*
        HU source identity index.

        Important:
        1. Check historical duplicates before creating the unique index.
        2. Create the index only when it does not exist.
        3. Validate the final definition after creation/existence check.
        4. Use NCHAR(39) to build the expected '' predicates and avoid fragile
           nested quote escaping inside a T-SQL string literal.
    */
    DECLARE @ExpectedHuSourceIdentityFilter NVARCHAR(4000);

    SET @ExpectedHuSourceIdentityFilter =
        N'sourcesystemisnotnullandsourceentityidisnotnullandsourcesystem<>'
        + NCHAR(39)
        + NCHAR(39)
        + N'andsourceentityid<>'
        + NCHAR(39)
        + NCHAR(39);

    /* HUMAN_REVIEW_REQUIRED: inspect this result before execution. Do not repair data here. */
    IF EXISTS
    (
        SELECT 1
        FROM dbo.WMS_HU
        WHERE SourceSystem IS NOT NULL
          AND SourceEntityId IS NOT NULL
          AND LTRIM(RTRIM(SourceSystem)) <> ''
          AND LTRIM(RTRIM(SourceEntityId)) <> ''
        GROUP BY SourceSystem, HUType, SourceEntityId
        HAVING COUNT(1) > 1
    )
    BEGIN
        THROW 51015, N'HUMAN_REVIEW_REQUIRED: existing WMS_HU source identity duplicates prevent filtered unique index creation.', 1;
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'UX_WMS_HU_SourceIdentity'
          AND object_id = OBJECT_ID(N'dbo.WMS_HU')
    )
    BEGIN
        CREATE UNIQUE INDEX UX_WMS_HU_SourceIdentity
        ON dbo.WMS_HU(SourceSystem, HUType, SourceEntityId)
        WHERE SourceSystem IS NOT NULL
          AND SourceEntityId IS NOT NULL
          AND SourceSystem <> ''
          AND SourceEntityId <> '';
    END;

    IF EXISTS
    (
        SELECT 1
        FROM sys.indexes i
        WHERE i.name = N'UX_WMS_HU_SourceIdentity'
          AND i.object_id = OBJECT_ID(N'dbo.WMS_HU')
          AND
          (
              i.is_unique <> 1
              OR i.has_filter <> 1
              OR LOWER
                 (
                     REPLACE
                     (
                         REPLACE
                         (
                             REPLACE
                             (
                                 REPLACE
                                 (
                                     REPLACE
                                     (
                                         i.filter_definition,
                                         N' ',
                                         N''
                                     ),
                                     N'[',
                                     N''
                                 ),
                                 N']',
                                 N''
                             ),
                             N'(',
                             N''
                         ),
                         N')',
                         N''
                     )
                 ) <> @ExpectedHuSourceIdentityFilter
          )
    )
    BEGIN
        THROW 51015, N'HUMAN_REVIEW_REQUIRED: HU source identity filtered index differs from 015.', 1;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM sys.index_columns ic
        INNER JOIN sys.indexes i
            ON i.object_id = ic.object_id
           AND i.index_id = ic.index_id
        WHERE i.name = N'UX_WMS_HU_SourceIdentity'
          AND i.object_id = OBJECT_ID(N'dbo.WMS_HU')
          AND ic.is_included_column = 1
    )
    BEGIN
        THROW 51015, N'HUMAN_REVIEW_REQUIRED: HU source identity index includes differ from 015.', 1;
    END;

    IF EXISTS
    (
        SELECT
            c.name,
            ic.key_ordinal
        FROM sys.indexes i
        INNER JOIN sys.index_columns ic
            ON ic.object_id = i.object_id
           AND ic.index_id = i.index_id
        INNER JOIN sys.columns c
            ON c.object_id = ic.object_id
           AND c.column_id = ic.column_id
        WHERE i.name = N'UX_WMS_HU_SourceIdentity'
          AND i.object_id = OBJECT_ID(N'dbo.WMS_HU')
          AND ic.is_included_column = 0

        EXCEPT

        SELECT *
        FROM
        (
            VALUES
                ('SourceSystem', 1),
                ('HUType', 2),
                ('SourceEntityId', 3)
        ) e(name, key_ordinal)
    )
    OR EXISTS
    (
        SELECT *
        FROM
        (
            VALUES
                ('SourceSystem', 1),
                ('HUType', 2),
                ('SourceEntityId', 3)
        ) e(name, key_ordinal)

        EXCEPT

        SELECT
            c.name,
            ic.key_ordinal
        FROM sys.indexes i
        INNER JOIN sys.index_columns ic
            ON ic.object_id = i.object_id
           AND ic.index_id = i.index_id
        INNER JOIN sys.columns c
            ON c.object_id = ic.object_id
           AND c.column_id = ic.column_id
        WHERE i.name = N'UX_WMS_HU_SourceIdentity'
          AND i.object_id = OBJECT_ID(N'dbo.WMS_HU')
          AND ic.is_included_column = 0
    )
    BEGIN
        THROW 51015, N'HUMAN_REVIEW_REQUIRED: HU source identity index keys differ from 015.', 1;
    END;

    IF OBJECT_ID(N'dbo.WMS_SchemaVersion', N'U') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM dbo.WMS_SchemaVersion WHERE VersionNo = '1.1.0')
    BEGIN
        INSERT INTO dbo.WMS_SchemaVersion(VersionNo, ScriptName, Remark)
        VALUES ('1.1.0', '015_FinishedGoodsInboundCore.sql', N'Finished Goods Inbound Core receipt and HU source identity.');
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
