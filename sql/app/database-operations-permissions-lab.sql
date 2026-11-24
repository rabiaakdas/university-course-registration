-- Development laboratory provisioning only. Run as migration/administration identity
-- in the exclusively owned test DB, never from an application migration/startup.
CREATE ROLE Day50Report;
CREATE USER Day50ReportUser WITHOUT LOGIN;
ALTER ROLE Day50Report ADD MEMBER Day50ReportUser;
GRANT SELECT ON OBJECT::dbo.OfferingCapacityReport TO Day50Report;
CREATE ROLE Day50CatalogRuntime;
CREATE USER Day50RuntimeUser WITHOUT LOGIN;
ALTER ROLE Day50CatalogRuntime ADD MEMBER Day50RuntimeUser;
GRANT SELECT, INSERT ON OBJECT::dbo.Courses TO Day50CatalogRuntime;
GRANT UPDATE ON OBJECT::dbo.Courses (Code,Name,Credits) TO Day50CatalogRuntime;
-- No sequence/UPDLOCK grants: this role is ONLY for catalog reads/course writes,
-- not enrollment, waitlist, Identity or the complete web application's runtime.
