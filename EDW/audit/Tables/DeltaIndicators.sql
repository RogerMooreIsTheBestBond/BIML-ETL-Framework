CREATE TABLE [audit].[DeltaIndicators] (
    [DeltaIndictorsID] BIGINT        IDENTITY (1, 1) NOT NULL,
    [ETLPackageRunID]  BIGINT        NULL,
    [PackageName]      VARCHAR (100) NULL,
    [TargetTableName]  VARCHAR (100) NULL,
    [IndicatorField]   VARCHAR (100) NULL,
    [IndicatorValue]   VARCHAR (50)  NULL,
    [Created]          DATETIME2 (7) NULL,
    [IsCurrent]        BIT           NULL,
    CONSTRAINT [PK_DeltaIndicators] PRIMARY KEY CLUSTERED ([DeltaIndictorsID] ASC),
    CONSTRAINT [FK_DeltaIndicators_ETLPackageRun] FOREIGN KEY ([ETLPackageRunID]) REFERENCES [audit].[ETLPackageRun] ([ETLPackageRunID])
);


