using Microsoft.AspNetCore.Identity;

namespace UniversityRegistration.Models;

// Account data is separate from the academic Student profile.
public class ApplicationUser : IdentityUser;
