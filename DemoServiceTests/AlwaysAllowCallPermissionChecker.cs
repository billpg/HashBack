using System;
using System.Threading.Tasks;
using DemoService.Services;

namespace DemoServiceTests;

/// <summary>An ICallPermissionChecker that always permits, for tests where the
/// permission-check feature itself isn't what's under test.</summary>
internal sealed class AlwaysAllowCallPermissionChecker : ICallPermissionChecker
{
    public Task<bool> IsCallPermittedAsync(Uri target) => Task.FromResult(true);
}
