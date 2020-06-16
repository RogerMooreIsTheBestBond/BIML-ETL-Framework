CREATE TABLE [audit].[InferredMembers] (
    [InferredMembersID] BIGINT        NOT NULL,
    [ETLPackageRunID]   BIGINT        NULL,
    [Dimension]         VARCHAR (100) NULL,
    [NaturalKey]        VARCHAR (100) NULL,
    [Created]           DATETIME2 (7) NULL,
    PRIMARY KEY CLUSTERED ([InferredMembersID] ASC),
    CONSTRAINT [FK_InferredMembers_ETLPackageRun] FOREIGN KEY ([ETLPackageRunID]) REFERENCES [audit].[ETLPackageRun] ([ETLPackageRunID])
);


