﻿using Microsoft.Extensions.DependencyInjection;

namespace OpenIddict.Core;

/// <summary>
/// Exposes a method allowing to resolve an authorization store.
/// </summary>
public class OpenIddictAuthorizationStoreResolver : IOpenIddictAuthorizationStoreResolver
{
    private readonly IServiceProvider _provider;

    public OpenIddictAuthorizationStoreResolver(IServiceProvider provider)
        => _provider = provider ?? throw new ArgumentNullException(nameof(provider));

    /// <summary>
    /// Returns an authorization store compatible with the specified authorization type or throws an
    /// <see cref="InvalidOperationException"/> if no store can be built using the specified type.
    /// </summary>
    /// <typeparam name="TAuthorization">The type of the Authorization entity.</typeparam>
    /// <returns>An <see cref="IOpenIddictAuthorizationStore{TAuthorization}"/>.</returns>
    public IOpenIddictAuthorizationStore<TAuthorization> Get<TAuthorization>() where TAuthorization : class
        => _provider.GetService<IOpenIddictAuthorizationStore<TAuthorization>>() ??
            throw new InvalidOperationException(SR.GetResourceString(SR.ID0229));
}
