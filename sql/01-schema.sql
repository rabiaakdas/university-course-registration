:r sql/guard-lab.sql
SET XACT_ABORT ON;
BEGIN TRANSACTION;

CREATE TABLE dbo.Courses
(
    Id int NOT NULL CONSTRAINT PK_Courses PRIMARY KEY,
    Code nvarchar(20) NOT NULL CONSTRAINT UQ_Courses_Code UNIQUE,
    Name nvarchar(200) NOT NULL,
    Credits int NOT NULL,
    CONSTRAINT CK_Courses_Credits_Positive CHECK (Credits > 0)
);

CREATE TABLE dbo.AcademicTerms
(
    Id int NOT NULL CONSTRAINT PK_AcademicTerms PRIMARY KEY,
    Name nvarchar(100) NOT NULL,
    StartsOn date NOT NULL,
    EndsOn date NOT NULL,
    CONSTRAINT CK_AcademicTerms_DateRange CHECK (EndsOn >= StartsOn)
);

CREATE TABLE dbo.CourseOfferings
(
    Id int NOT NULL CONSTRAINT PK_CourseOfferings PRIMARY KEY,
    CourseId int NOT NULL,
    AcademicTermId int NOT NULL,
    SectionCode nvarchar(10) NOT NULL,
    Capacity int NOT NULL,
    CONSTRAINT CK_CourseOfferings_Capacity_Positive CHECK (Capacity > 0),
    CONSTRAINT UQ_CourseOfferings_Course_Term_Section UNIQUE (CourseId, AcademicTermId, SectionCode),
    CONSTRAINT FK_CourseOfferings_Courses FOREIGN KEY (CourseId)
        REFERENCES dbo.Courses(Id) ON DELETE NO ACTION ON UPDATE NO ACTION,
    CONSTRAINT FK_CourseOfferings_AcademicTerms FOREIGN KEY (AcademicTermId)
        REFERENCES dbo.AcademicTerms(Id) ON DELETE NO ACTION ON UPDATE NO ACTION
);

COMMIT;
