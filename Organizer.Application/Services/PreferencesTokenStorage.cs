using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Apis.Json;

namespace Organizer.Application.Services;

public sealed class PreferencesTokenStorage(AppPreferencesService preferencesService) : ITokenStorage
{
    public bool HasRefreshToken => preferencesService.ReadStoredData(preferences =>
        (preferences.GoogleDriveTokenStore?.Count ?? 0) > 0
        || !string.IsNullOrWhiteSpace(preferences.GoogleDriveRefreshToken));

    public Task StoreAsync<T>(string key, T value)
    {
        ArgumentNullException.ThrowIfNull(key);

        var storageKey = CreateStorageKey<T>(key);
        var json = NewtonsoftJsonSerializer.Instance.Serialize(value);

        preferencesService.UpdateStoredData(preferences =>
        {
            preferences.GoogleDriveTokenStore ??= [];
            preferences.GoogleDriveTokenStore[storageKey] = json;
        });

        return Task.CompletedTask;
    }

    public Task DeleteAsync<T>(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        var storageKey = CreateStorageKey<T>(key);
        preferencesService.UpdateStoredData(preferences =>
        {
            preferences.GoogleDriveTokenStore ??= [];
            preferences.GoogleDriveTokenStore.Remove(storageKey);
        });

        return Task.CompletedTask;
    }

    public Task<T> GetAsync<T>(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        var storageKey = CreateStorageKey<T>(key);
        var json = preferencesService.ReadStoredData(preferences =>
        {
            var store = preferences.GoogleDriveTokenStore ?? new Dictionary<string, string>();
            return store.TryGetValue(storageKey, out var storedJson) ? storedJson : null;
        });

        if (string.IsNullOrWhiteSpace(json))
            return Task.FromResult(default(T)!);

        return Task.FromResult(NewtonsoftJsonSerializer.Instance.Deserialize<T>(json));
    }

    public Task ClearAsync()
    {
        preferencesService.UpdateStoredData(preferences =>
        {
            preferences.GoogleDriveRefreshToken = null;
            preferences.GoogleDriveTokenStore = [];
        });

        return Task.CompletedTask;
    }

    private static string CreateStorageKey<T>(string key)
    {
        return $"{typeof(T).FullName}:{key}";
    }
}
