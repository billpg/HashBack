/* Copyright William Godfrey, 2024. All rights reserved.
 * billpg.com
 */
using Microsoft.AspNetCore.Http;

internal class RedirectEndpoints
{
    internal static Delegate Found(string target)
    {
        return FoundRedirector;
        void FoundRedirector(HttpContext context)
            => context.Response.Redirect(target);        
    }
}