CREATE TABLE [audit].[ETLError] (
    [ETLErrorID]       BIGINT           NOT NULL,
    [ETLPackageRunID]  BIGINT           NULL,
    [ExecutionGUID]    UNIQUEIDENTIFIER NULL,
    [TaskGUID]         UNIQUEIDENTIFIER NULL,
    [TaskName]         VARCHAR (100)    NULL,
    [TaskDescription]  VARCHAR (200)    NULL,
    [ErrorType]        VARCHAR (20)     NULL,
    [ErrorCode]        VARCHAR (100)    NULL,
    [ErrorDescription] VARCHAR (5000)   NULL,
    [Created]          DATETIME2 (7)    NULL,
    PRIMARY KEY CLUSTERED ([ETLErrorID] ASC),
    CONSTRAINT [FK_ETLError_ETLPackageRun] FOREIGN KEY ([ETLPackageRunID]) REFERENCES [audit].[ETLPackageRun] ([ETLPackageRunID])
);


