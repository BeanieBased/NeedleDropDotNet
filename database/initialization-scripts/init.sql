-- Runs once when the database container first starts (see entrypoint.sh).
-- Mirrors the schema NeedleDrop.Api/Data/Db.cs also knows how to create —
-- having both means the database is ready immediately on container start,
-- and the API's own startup check is a harmless no-op since everything
-- already exists.

IF DB_ID('NeedleDropDb') IS NULL
CREATE DATABASE NeedleDropDb;
GO

USE NeedleDropDb;
GO

IF OBJECT_ID('dbo.Tracks', 'U') IS NULL
CREATE TABLE dbo.Tracks (
    Id              INT IDENTITY PRIMARY KEY,
    SpotifyTrackId  NVARCHAR(50)  NOT NULL,
    Title           NVARCHAR(200) NOT NULL,
    Artist          NVARCHAR(200) NOT NULL,
    Streams         BIGINT        NULL
);
GO

IF OBJECT_ID('dbo.SongLeaderboard', 'U') IS NULL
CREATE TABLE dbo.SongLeaderboard (
    Id         INT IDENTITY PRIMARY KEY,
    Initials   CHAR(3) NOT NULL,
    Score      INT     NOT NULL
);
GO

IF OBJECT_ID('dbo.StreamsLeaderboard', 'U') IS NULL
CREATE TABLE dbo.StreamsLeaderboard (
    Id         INT IDENTITY PRIMARY KEY,
    Initials   CHAR(3) NOT NULL,
    Score      INT     NOT NULL
);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Tracks)
INSERT INTO dbo.Tracks (SpotifyTrackId, Title, Artist, Streams) VALUES
('t1', 'Sample Track A', 'Artist One',   812000000),
('t2', 'Sample Track B', 'Artist Two',   640000000),
('t3', 'Sample Track C', 'Artist Three', 1204000000),
('t4', 'Sample Track D', 'Artist Four',  990000000),
('t5', 'Sample Track E', 'Artist Five',  455000000),
('t6', 'Sample Track F', 'Artist Six',   610000000);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SongLeaderboard)
INSERT INTO dbo.SongLeaderboard (Initials, Score) VALUES
('JWB', 8), ('ALT', 7), ('MRQ', 6);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.StreamsLeaderboard)
INSERT INTO dbo.StreamsLeaderboard (Initials, Score) VALUES
('JWB', 83), ('ABC', 61), ('QRS', 44);
GO
