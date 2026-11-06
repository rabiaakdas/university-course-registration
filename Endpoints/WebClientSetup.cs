namespace UniversityRegistration.Endpoints;

public static class WebClientSetup
{
    // Only the web-root provider; never expose the content root or local secrets.
    public static void UseUniversityWebClient(this WebApplication app)
    {
        var oidc = app.Services.GetRequiredService<UniversityRegistration.Accounts.UniversityOidcSettings>();
        var formTargets = oidc.Enabled ? "'self' " + new Uri(oidc.Authority).GetLeftPart(UriPartial.Authority) : "'self'";
        app.UseDefaultFiles();
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = context =>
            {
                var headers = context.Context.Response.Headers;
                headers.CacheControl = "no-store";
                headers["X-Content-Type-Options"] = "nosniff";
                headers["Referrer-Policy"] = "no-referrer";
                if (context.File.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                    headers.ContentSecurityPolicy = $"default-src 'none'; script-src 'self'; style-src 'self'; connect-src 'self'; img-src 'self'; base-uri 'none'; form-action {formTargets}; object-src 'none'; frame-ancestors 'none'";
            }
        });
    }
}
