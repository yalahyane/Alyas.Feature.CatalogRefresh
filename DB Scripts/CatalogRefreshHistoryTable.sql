SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[CatalogRefreshHistory](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Status] [nvarchar](10) NOT NULL,
	[Source] [nvarchar](10) NOT NULL,
	[Destination] [nvarchar](10) NOT NULL,
	[StartTime] [datetime] NOT NULL,
	[EndTime] [datetime] NULL,
	[ErrorMessage] [nvarchar](max) NULL,
 CONSTRAINT [PK_CatalogRefreshHistory] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO


