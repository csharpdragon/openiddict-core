﻿/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using Microsoft.Extensions.Caching.Distributed;
using Owin;

namespace OpenIddict.Server.Owin;

/// <summary>
/// Provides various settings needed to configure the OpenIddict OWIN server integration.
/// </summary>
public class OpenIddictServerOwinOptions : AuthenticationOptions
{
    /// <summary>
    /// Creates a new instance of the <see cref="OpenIddictServerOwinOptions"/> class.
    /// </summary>
    public OpenIddictServerOwinOptions()
        : base(OpenIddictServerOwinDefaults.AuthenticationType)
        => AuthenticationMode = AuthenticationMode.Passive;

    /// <summary>
    /// Gets or sets a boolean indicating whether incoming requests arriving on insecure endpoints should be rejected.
    /// By default, this property is set to <see langword="false"/> to help mitigate man-in-the-middle attacks.
    /// </summary>
    public bool DisableTransportSecurityRequirement { get; set; }

    /// <summary>
    /// Gets or sets a boolean indicating whether the pass-through mode is enabled for the authorization endpoint.
    /// When the pass-through mode is used, OpenID Connect requests are initially handled by OpenIddict.
    /// Once validated, the rest of the request processing pipeline is invoked, so that OpenID Connect requests
    /// can be handled at a later stage (in a custom middleware or in a MVC controller, for instance).
    /// </summary>
    public bool EnableAuthorizationEndpointPassthrough { get; set; }

    /// <summary>
    /// Gets or sets a boolean indicating whether OpenIddict should allow the rest of the request processing pipeline
    /// to be invoked when returning an error from the interactive authorization and logout endpoints.
    /// When this option is enabled, special logic must be added to these actions to handle errors, that can be
    /// retrieved using <see cref="OpenIddictServerOwinHelpers.GetOpenIddictServerResponse(IOwinContext)"/>
    /// </summary>
    public bool EnableErrorPassthrough { get; set; }

    /// <summary>
    /// Gets or sets a boolean indicating whether the pass-through mode is enabled for the authorization endpoint.
    /// When the pass-through mode is used, OpenID Connect requests are initially handled by OpenIddict.
    /// Once validated, the rest of the request processing pipeline is invoked, so that OpenID Connect requests
    /// can be handled at a later stage (in a custom middleware or in a MVC controller, for instance).
    /// </summary>
    public bool EnableLogoutEndpointPassthrough { get; set; }

    /// <summary>
    /// Gets or sets a boolean indicating whether the pass-through mode is enabled for the token endpoint.
    /// When the pass-through mode is used, OpenID Connect requests are initially handled by OpenIddict.
    /// Once validated, the rest of the request processing pipeline is invoked, so that OpenID Connect requests
    /// can be handled at a later stage (in a custom middleware or in a MVC controller, for instance).
    /// </summary>
    public bool EnableTokenEndpointPassthrough { get; set; }

    /// <summary>
    /// Gets or sets a boolean indicating whether the pass-through mode is enabled for the userinfo endpoint.
    /// When the pass-through mode is used, OpenID Connect requests are initially handled by OpenIddict.
    /// Once validated, the rest of the request processing pipeline is invoked, so that OpenID Connect requests
    /// can be handled at a later stage (in a custom middleware or in a MVC controller, for instance).
    /// </summary>
    public bool EnableUserinfoEndpointPassthrough { get; set; }

    /// <summary>
    /// Gets or sets a boolean indicating whether the pass-through mode is enabled for the user verification endpoint.
    /// When the pass-through mode is used, OpenID Connect requests are initially handled by OpenIddict.
    /// Once validated, the rest of the request processing pipeline is invoked, so that OpenID Connect requests
    /// can be handled at a later stage (in a custom middleware or in a MVC controller, for instance).
    /// </summary>
    public bool EnableVerificationEndpointPassthrough { get; set; }

    /// <summary>
    /// Gets or sets a boolean indicating whether requests received by the authorization endpoint
    /// should be cached. When enabled, authorization requests are automatically stored
    /// in the distributed cache, which allows flowing large payloads across requests.
    /// Enabling this option is recommended when using external authentication providers
    /// or when large GET or POST OpenID Connect authorization requests support is required.
    /// </summary>
    public bool EnableAuthorizationRequestCaching { get; set; }

    /// <summary>
    /// Gets or sets a boolean indicating whether requests received by the logout endpoint should be cached.
    /// When enabled, authorization requests are automatically stored in the distributed cache.
    /// </summary>
    public bool EnableLogoutRequestCaching { get; set; }

    /// <summary>
    /// Gets or sets the optional "realm" value returned to the caller as part of the WWW-Authenticate header.
    /// </summary>
    public string? Realm { get; set; }

    /// <summary>
    /// Gets or sets the caching policy used by the authorization endpoint.
    /// </summary>
    public DistributedCacheEntryOptions AuthorizationRequestCachingPolicy { get; set; } = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
        SlidingExpiration = TimeSpan.FromMinutes(30)
    };

    /// <summary>
    /// Gets or sets the caching policy used by the logout endpoint.
    /// </summary>
    public DistributedCacheEntryOptions LogoutRequestCachingPolicy { get; set; } = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
        SlidingExpiration = TimeSpan.FromMinutes(30)
    };
}
