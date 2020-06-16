CREATE TABLE [audit].[ETLTaskRun] (
    [ETLTaskRunID]    BIGINT           NOT NULL,
    [ETLPackageRunID] BIGINT           NULL,
    [ExecutionGUID]   UNIQUEIDENTIFIER NULL,
    [TaskGUID]        UNIQUEIDENTIFIER NULL,
    [TaskName]        VARCHAR (100)    NULL,
    [TaskDescription] VARCHAR (200)    NULL,
    [Created]         DATETIME2 (7)    NULL,
    PRIMARY KEY CLUSTERED ([ETLTaskRunID] ASC),
    CONSTRAINT [FK_ETLTaskRun_ETLPackageRun] FOREIGN KEY ([ETLPackageRunID]) REFERENCES [audit].[ETLPackageRun] ([ETLPackageRunID])
);


