﻿/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using Microsoft.IdentityModel.Tokens;

namespace OpenIddict.Abstractions;

/// <summary>
/// Represents the configuration of an authorization server.
/// </summary>
/// <remarks>
/// Note: depending on the stack used to produce this instance, only a few select properties may be available.
/// </remarks>
public class OpenIddictConfiguration
{
    /// <summary>
    /// Gets or sets the address of the authorization endpoint.
    /// </summary>
    public Uri? AuthorizationEndpoint { get; set; }

    /// <summary>
    /// Gets or sets a boolean indicating whether the "iss" parameter is returned in authorization responses.
    /// </summary>
    public bool? AuthorizationResponseIssParameterSupported { get; set; }

    /// <summary>
    /// Gets the code challenge methods supported by the server.
    /// </summary>
    public HashSet<string> CodeChallengeMethodsSupported { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the grant types supported by the server.
    /// </summary>
    public HashSet<string> GrantTypesSupported { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the address of the introspection endpoint.
    /// </summary>
    public Uri? IntrospectionEndpoint { get; set; }

    /// <summary>
    /// Gets the client authentication methods supported by the introspection endpoint.
    /// </summary>
    public HashSet<string> IntrospectionEndpointAuthMethodsSupported { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the address of the issuer.
    /// </summary>
    public Uri? Issuer { get; set; }

    /// <summary>
    /// Gets or sets the JSON Web Key set containing the keys exposed by the server.
    /// </summary>
    public JsonWebKeySet? JsonWebKeySet { get; set; }

    /// <summary>
    /// Gets or sets the address of the JWKS endpoint.
    /// </summary>
    public Uri? JwksUri { get; set; }

    /// <summary>
    /// Gets the response mode supported by the server.
    /// </summary>
    public HashSet<string> ResponseModesSupported { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the response types supported by the server.
    /// </summary>
    public HashSet<string> ResponseTypesSupported { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the scopes supported by the server.
    /// </summary>
    public HashSet<string> ScopesSupported { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the signing keys extracted from the JSON Web Key set.
    /// </summary>
    public List<SecurityKey> SigningKeys { get; } = new();

    /// <summary>
    /// Gets or sets the address of the token endpoint.
    /// </summary>
    public Uri? TokenEndpoint { get; set; }

    /// <summary>
    /// Gets the client authentication methods supported by the token endpoint.
    /// </summary>
    public HashSet<string> TokenEndpointAuthMethodsSupported { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the address of the userinfo endpoint.
    /// </summary>
    public Uri? UserinfoEndpoint { get; set; }
}
