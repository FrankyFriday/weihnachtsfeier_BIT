using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace WeihnachtsfeierBit.Framework;

public abstract class AuthenticatedComponentBase : ComponentBase
{
    [Inject] protected AuthenticationStateProvider AuthenticationStateProvider { get; set; } = null!;
    [Inject] protected ProjectApiClient ProjectApiClient { get; set; } = null!;

    protected string? AccessToken { get; private set; }
    protected string? ErrorMessage { get; private set; }
    protected bool IsLoading { get; private set; } = true;

    protected async Task LoadAuthenticatedAsync(Func<string, Task> action)
    {
        IsLoading = true;
        ErrorMessage = null;
        AccessToken = await GetAccessTokenAsync();
        if (string.IsNullOrEmpty(AccessToken))
        {
            ErrorMessage = "Die Admin-Sitzung ist nicht verfügbar. Bitte melde dich erneut an.";
            IsLoading = false;
            return;
        }

        try
        {
            await action(AccessToken);
        }
        catch (HttpRequestException exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected async Task RunAuthenticatedAsync(Func<string, Task> action)
    {
        ErrorMessage = null;
        AccessToken ??= await GetAccessTokenAsync();
        if (string.IsNullOrEmpty(AccessToken))
        {
            ErrorMessage = "Die Admin-Sitzung ist nicht verfügbar. Bitte melde dich erneut an.";
            return;
        }

        try
        {
            await action(AccessToken);
        }
        catch (HttpRequestException exception)
        {
            ErrorMessage = exception.Message;
        }
    }

    private async Task<string?> GetAccessTokenAsync()
    {
        var authentication = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        return authentication.User.FindFirst("api_access_token")?.Value;
    }
}