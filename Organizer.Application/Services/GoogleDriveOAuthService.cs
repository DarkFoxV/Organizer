using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Drive.v3;
using Google.Apis.Services;

namespace Organizer.Application.Services;

public sealed class GoogleDriveOAuthService(
    AppPreferencesService preferencesService,
    ITokenStorage tokenStorage,
    IAppLogger logger)
{
    private static readonly string[] Scopes = [DriveService.Scope.DriveFile];

    public bool IsConfigured => !string.IsNullOrWhiteSpace(GetClientId());

    public bool IsConnected => IsConfigured && tokenStorage.HasRefreshToken;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        logger.Info("Google Drive login started.");

        try
        {
            await AuthorizeAsync(cancellationToken);
            logger.Info("Google Drive login completed.");
        }
        catch (Exception ex)
        {
            logger.Error("Google Drive login failed", ex);
            throw new InvalidOperationException(
                preferencesService.T("Loc.Backup.GoogleDriveAuthFailed"),
                ex);
        }
    }

    public async Task<DriveService> CreateDriveServiceAsync(CancellationToken cancellationToken = default)
    {
        var credential = await AuthorizeAsync(cancellationToken);

        return new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "Organizer"
        });
    }

    private async Task<UserCredential> AuthorizeAsync(CancellationToken cancellationToken)
    {
        var clientId = GetClientId();
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException(preferencesService.T("Loc.Backup.GoogleDriveMissingClientId"));

        var initializer = new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets
            {
                ClientId = clientId,
                ClientSecret = GetClientSecret()
            }
        };

        logger.Info($"Google OAuth credential: client_id={MaskClientId(clientId)}, type_hint={GetCredentialTypeHint(clientId)}.");
        logger.Info($"Google OAuth authorization: broker=GoogleWebAuthorizationBroker, use_pkce=true, scope={DriveService.Scope.DriveFile}.");

        return await GoogleWebAuthorizationBroker.AuthorizeAsync(
            initializer,
            Scopes,
            "default",
            usePkce: true,
            taskCancellationToken: cancellationToken,
            dataStore: tokenStorage);
    }

    private string? GetClientId()
    {
        return NormalizeOptionalValue(preferencesService.Current.GoogleDriveClientId)
            ?? NormalizeOptionalValue(Environment.GetEnvironmentVariable("GOOGLE_DRIVE_CLIENT_ID"));
    }

    private string? GetClientSecret()
    {
        return NormalizeOptionalValue(preferencesService.Current.GoogleDriveClientSecret)
            ?? NormalizeOptionalValue(Environment.GetEnvironmentVariable("GOOGLE_DRIVE_CLIENT_SECRET"));
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string MaskClientId(string clientId)
    {
        if (clientId.Length <= 12)
            return "***";

        return $"{clientId[..6]}...{clientId[^6..]}";
    }

    private static string GetCredentialTypeHint(string clientId)
    {
        return clientId.EndsWith(".apps.googleusercontent.com", StringComparison.OrdinalIgnoreCase)
            ? "google-oauth-client-id"
            : "unknown-format";
    }
}
