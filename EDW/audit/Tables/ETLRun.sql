CREATE TABLE [audit].[ETLRun] (
    [ETLRunID]   BIGINT        NOT NULL,
    [ETLRunName] VARCHAR (100) NULL,
    [Start]      DATETIME2 (7) NULL,
    [Finish]     DATETIME2 (7) NULL,
    [HasErrors]  BIT           NULL,
    [Created]    DATETIME2 (7) NULL,
    [Updated]    DATETIME2 (7) NULL,
    CONSTRAINT [PK__ETLRun__FB90B85B2AC3272A] PRIMARY KEY CLUSTERED ([ETLRunID] ASC)
);


