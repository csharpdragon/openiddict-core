﻿/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace OpenIddict.Core;

/// <summary>
/// Provides methods allowing to cache applications after retrieving them from the store.
/// </summary>
/// <typeparam name="TApplication">The type of the Application entity.</typeparam>
public class OpenIddictApplicationCache<TApplication> : IOpenIddictApplicationCache<TApplication>, IDisposable where TApplication : class
{
    private readonly MemoryCache _cache;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _signals;
    private readonly IOpenIddictApplicationStore<TApplication> _store;

    public OpenIddictApplicationCache(
        IOptionsMonitor<OpenIddictCoreOptions> options,
        IOpenIddictApplicationStoreResolver resolver)
    {
        _cache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = (options ?? throw new ArgumentNullException(nameof(options))).CurrentValue.EntityCacheLimit
        });

        _signals = new ConcurrentDictionary<string, CancellationTokenSource>(StringComparer.Ordinal);
        _store = (resolver ?? throw new ArgumentNullException(nameof(resolver))).Get<TApplication>();
    }

    /// <inheritdoc/>
    public async ValueTask AddAsync(TApplication application, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        _cache.Remove(new
        {
            Method = nameof(FindByClientIdAsync),
            Identifier = await _store.GetClientIdAsync(application, cancellationToken)
        });

        _cache.Remove(new
        {
            Method = nameof(FindByIdAsync),
            Identifier = await _store.GetIdAsync(application, cancellationToken)
        });

        foreach (var address in await _store.GetPostLogoutRedirectUrisAsync(application, cancellationToken))
        {
            _cache.Remove(new
            {
                Method = nameof(FindByPostLogoutRedirectUriAsync),
                Address = address
            });
        }

        foreach (var address in await _store.GetRedirectUrisAsync(application, cancellationToken))
        {
            _cache.Remove(new
            {
                Method = nameof(FindByRedirectUriAsync),
                Address = address
            });
        }

        await CreateEntryAsync(new
        {
            Method = nameof(FindByIdAsync),
            Identifier = await _store.GetIdAsync(application, cancellationToken)
        }, application, cancellationToken);

        await CreateEntryAsync(new
        {
            Method = nameof(FindByClientIdAsync),
            Identifier = await _store.GetClientIdAsync(application, cancellationToken)
        }, application, cancellationToken);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var signal in _signals)
        {
            signal.Value.Dispose();
        }

        _cache.Dispose();
    }

    /// <inheritdoc/>
    public ValueTask<TApplication?> FindByClientIdAsync(string identifier, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0195), nameof(identifier));
        }

        var parameters = new
        {
            Method = nameof(FindByClientIdAsync),
            Identifier = identifier
        };

        if (_cache.TryGetValue(parameters, out TApplication? application))
        {
            return new(application);
        }

        return new(ExecuteAsync());

        async Task<TApplication?> ExecuteAsync()
        {
            if ((application = await _store.FindByClientIdAsync(identifier, cancellationToken)) is not null)
            {
                await AddAsync(application, cancellationToken);
            }

            await CreateEntryAsync(parameters, application, cancellationToken);

            return application;
        }
    }

    /// <inheritdoc/>
    public ValueTask<TApplication?> FindByIdAsync(string identifier, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0195), nameof(identifier));
        }

        var parameters = new
        {
            Method = nameof(FindByIdAsync),
            Identifier = identifier
        };

        if (_cache.TryGetValue(parameters, out TApplication? application))
        {
            return new(application);
        }

        return new(ExecuteAsync());

        async Task<TApplication?> ExecuteAsync()
        {
            if ((application = await _store.FindByIdAsync(identifier, cancellationToken)) is not null)
            {
                await AddAsync(application, cancellationToken);
            }

            await CreateEntryAsync(parameters, application, cancellationToken);

            return application;
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<TApplication> FindByPostLogoutRedirectUriAsync(string address, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(address))
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0143), nameof(address));
        }

        return ExecuteAsync(cancellationToken);

        async IAsyncEnumerable<TApplication> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var parameters = new
            {
                Method = nameof(FindByPostLogoutRedirectUriAsync),
                Address = address
            };

            if (!_cache.TryGetValue(parameters, out ImmutableArray<TApplication> applications))
            {
                var builder = ImmutableArray.CreateBuilder<TApplication>();

                await foreach (var application in _store.FindByPostLogoutRedirectUriAsync(address, cancellationToken))
                {
                    builder.Add(application);

                    await AddAsync(application, cancellationToken);
                }

                applications = builder.ToImmutable();

                await CreateEntryAsync(parameters, applications, cancellationToken);
            }

            foreach (var application in applications)
            {
                yield return application;
            }
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<TApplication> FindByRedirectUriAsync(string address, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(address))
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0143), nameof(address));
        }

        return ExecuteAsync(cancellationToken);

        async IAsyncEnumerable<TApplication> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var parameters = new
            {
                Method = nameof(FindByRedirectUriAsync),
                Address = address
            };

            if (!_cache.TryGetValue(parameters, out ImmutableArray<TApplication> applications))
            {
                var builder = ImmutableArray.CreateBuilder<TApplication>();

                await foreach (var application in _store.FindByRedirectUriAsync(address, cancellationToken))
                {
                    builder.Add(application);

                    await AddAsync(application, cancellationToken);
                }

                applications = builder.ToImmutable();

                await CreateEntryAsync(parameters, applications, cancellationToken);
            }

            foreach (var application in applications)
            {
                yield return application;
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask RemoveAsync(TApplication application, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        var identifier = await _store.GetIdAsync(application, cancellationToken);
        if (string.IsNullOrEmpty(identifier))
        {
            throw new InvalidOperationException(SR.GetResourceString(SR.ID0196));
        }

        if (_signals.TryRemove(identifier, out CancellationTokenSource? signal))
        {
            signal.Cancel();
            signal.Dispose();
        }
    }

    /// <summary>
    /// Creates a cache entry for the specified key.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="application">The application to store in the cache entry, if applicable.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
    protected virtual async ValueTask CreateEntryAsync(object key, TApplication? application, CancellationToken cancellationToken)
    {
        if (key is null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        using var entry = _cache.CreateEntry(key);

        if (application is not null)
        {
            entry.AddExpirationToken(await CreateExpirationSignalAsync(application, cancellationToken) ??
                throw new InvalidOperationException(SR.GetResourceString(SR.ID0197)));
        }

        entry.SetSize(1L);
        entry.SetValue(application);
    }

    /// <summary>
    /// Creates a cache entry for the specified key.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="applications">The applications to store in the cache entry.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
    protected virtual async ValueTask CreateEntryAsync(
        object key, ImmutableArray<TApplication> applications, CancellationToken cancellationToken)
    {
        if (key is null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        using var entry = _cache.CreateEntry(key);

        foreach (var application in applications)
        {
            entry.AddExpirationToken(await CreateExpirationSignalAsync(application, cancellationToken) ??
                throw new InvalidOperationException(SR.GetResourceString(SR.ID0197)));
        }

        entry.SetSize(applications.Length);
        entry.SetValue(applications);
    }

    /// <summary>
    /// Creates an expiration signal allowing to invalidate all the
    /// cache entries associated with the specified application.
    /// </summary>
    /// <param name="application">The application associated with the expiration signal.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
    /// <returns>
    /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation,
    /// whose result returns an expiration signal for the specified application.
    /// </returns>
    protected virtual async ValueTask<IChangeToken> CreateExpirationSignalAsync(
        TApplication application, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        var identifier = await _store.GetIdAsync(application, cancellationToken);
        if (string.IsNullOrEmpty(identifier))
        {
            throw new InvalidOperationException(SR.GetResourceString(SR.ID0196));
        }

        var signal = _signals.GetOrAdd(identifier, _ => new CancellationTokenSource());

        return new CancellationChangeToken(signal.Token);
    }
}
