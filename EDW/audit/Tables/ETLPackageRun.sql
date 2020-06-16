CREATE TABLE [audit].[ETLPackageRun] (
    [ETLPackageRunID]    BIGINT           NOT NULL,
    [ETLRunID]           BIGINT           NULL,
    [PackageName]        VARCHAR (100)    NULL,
    [PackageGUID]        UNIQUEIDENTIFIER NULL,
    [PackageVersionGUID] UNIQUEIDENTIFIER NULL,
    [ExecutionGUID]      UNIQUEIDENTIFIER NULL,
    [Start]              DATETIME2 (7)    NULL,
    [Finish]             DATETIME2 (7)    NULL,
    [HasErrors]          BIT              NULL,
    [HasInferredMembers] BIT              NULL,
    [Created]            DATETIME2 (7)    NULL,
    [Updated]            DATETIME2 (7)    NULL,
    PRIMARY KEY CLUSTERED ([ETLPackageRunID] ASC),
    CONSTRAINT [FK_ETLPackageRun_ETLRun] FOREIGN KEY ([ETLRunID]) REFERENCES [audit].[ETLRun] ([ETLRunID])
);


