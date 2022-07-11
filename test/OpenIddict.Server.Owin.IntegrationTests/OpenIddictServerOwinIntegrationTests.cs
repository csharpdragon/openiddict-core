﻿/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Owin;
using Microsoft.Owin.Security;
using Microsoft.Owin.Testing;
using OpenIddict.Server.IntegrationTests;
using Owin;
using Xunit;
using Xunit.Abstractions;
using static OpenIddict.Server.OpenIddictServerEvents;
using static OpenIddict.Server.OpenIddictServerHandlers.Protection;
using static OpenIddict.Server.Owin.OpenIddictServerOwinHandlers;

namespace OpenIddict.Server.Owin.IntegrationTests;

public partial class OpenIddictServerOwinIntegrationTests : OpenIddictServerIntegrationTests
{
    public OpenIddictServerOwinIntegrationTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Fact]
    public async Task ProcessAuthentication_CreationDateIsMappedToIssuedUtc()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.SetUserinfoEndpointUris("/authenticate/properties");

            options.AddEventHandler<HandleUserinfoRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SkipRequest();

                    return default;
                }));

            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal("access_token", context.Token);
                    Assert.Equal(new[] { TokenTypeHints.AccessToken }, context.ValidTokenTypes);

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetTokenType(TokenTypeHints.AccessToken)
                        .SetClaim(Claims.Subject, "Bob le Magnifique")
                        .SetCreationDate(new DateTimeOffset(2020, 01, 01, 00, 00, 00, TimeSpan.Zero));

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.GetAsync("/authenticate/properties", new OpenIddictRequest
        {
            AccessToken = "access_token"
        });

        // Assert
        var properties = new AuthenticationProperties(response.GetParameters()
            .ToDictionary(parameter => parameter.Key, parameter => (string?) parameter.Value));

        Assert.Equal(new DateTimeOffset(2020, 01, 01, 00, 00, 00, TimeSpan.Zero), properties.IssuedUtc);
    }

    [Fact]
    public async Task ProcessAuthentication_ExpirationDateIsMappedToIssuedUtc()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.SetUserinfoEndpointUris("/authenticate/properties");

            options.AddEventHandler<HandleUserinfoRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SkipRequest();

                    return default;
                }));

            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal("access_token", context.Token);
                    Assert.Equal(new[] { TokenTypeHints.AccessToken }, context.ValidTokenTypes);

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetTokenType(TokenTypeHints.AccessToken)
                        .SetExpirationDate(new DateTimeOffset(2120, 01, 01, 00, 00, 00, TimeSpan.Zero));

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.GetAsync("/authenticate/properties", new OpenIddictRequest
        {
            AccessToken = "access_token"
        });

        // Assert
        var properties = new AuthenticationProperties(response.GetParameters()
            .ToDictionary(parameter => parameter.Key, parameter => (string?) parameter.Value));

        Assert.Equal(new DateTimeOffset(2120, 01, 01, 00, 00, 00, TimeSpan.Zero), properties.ExpiresUtc);
    }

    [Fact]
    public async Task ProcessChallenge_ReturnsParametersFromAuthenticationProperties()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.SetTokenEndpointUris("/challenge/custom");

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SkipRequest();

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/challenge/custom", new OpenIddictRequest
        {
            GrantType = GrantTypes.Password,
            Username = "johndoe",
            Password = "A3ddj3w"
        });

        // Assert
        Assert.True((bool) response["boolean_parameter"]);
        Assert.Equal(JsonValueKind.True, ((JsonElement) response["boolean_parameter"]).ValueKind);
        Assert.Equal(42, (long) response["integer_parameter"]);
        Assert.Equal(JsonValueKind.Number, ((JsonElement) response["integer_parameter"]).ValueKind);
        Assert.Equal("Bob l'Eponge", (string?) response["string_parameter"]);
        Assert.Equal(JsonValueKind.String, ((JsonElement) response["string_parameter"]).ValueKind);
        Assert.Equal(new[] { "Contoso", "Fabrikam" }, (string[]?) response["json_parameter"]);
        Assert.Equal(JsonValueKind.Array, ((JsonElement) response["json_parameter"]).ValueKind);
    }

    [Fact]
    public async Task ProcessChallenge_ReturnsErrorFromAuthenticationProperties()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.SetTokenEndpointUris("/challenge/custom");

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SkipRequest();

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/challenge/custom", new OpenIddictRequest
        {
            GrantType = GrantTypes.Password,
            Username = "johndoe",
            Password = "A3ddj3w"
        });

        // Assert
        Assert.Equal("custom_error", response.Error);
        Assert.Equal("custom_error_description", response.ErrorDescription);
        Assert.Equal("custom_error_uri", response.ErrorUri);
    }

    [Theory]
    [InlineData("/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/connect", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/CONNECT", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/connect/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/CONNECT/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/connect/authorize", OpenIddictServerEndpointType.Authorization)]
    [InlineData("/CONNECT/AUTHORIZE", OpenIddictServerEndpointType.Authorization)]
    [InlineData("/connect/authorize/", OpenIddictServerEndpointType.Authorization)]
    [InlineData("/CONNECT/AUTHORIZE/", OpenIddictServerEndpointType.Authorization)]
    [InlineData("/connect/authorize/subpath", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/CONNECT/AUTHORIZE/SUBPATH", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/connect/authorize/subpath/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/CONNECT/AUTHORIZE/SUBPATH/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/.well-known/openid-configuration", OpenIddictServerEndpointType.Configuration)]
    [InlineData("/.WELL-KNOWN/OPENID-CONFIGURATION", OpenIddictServerEndpointType.Configuration)]
    [InlineData("/.well-known/openid-configuration/", OpenIddictServerEndpointType.Configuration)]
    [InlineData("/.WELL-KNOWN/OPENID-CONFIGURATION/", OpenIddictServerEndpointType.Configuration)]
    [InlineData("/.well-known/openid-configuration/subpath", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/.WELL-KNOWN/OPENID-CONFIGURATION/SUBPATH", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/.well-known/openid-configuration/subpath/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/.WELL-KNOWN/OPENID-CONFIGURATION/SUBPATH/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/.well-known/jwks", OpenIddictServerEndpointType.Cryptography)]
    [InlineData("/.WELL-KNOWN/JWKS", OpenIddictServerEndpointType.Cryptography)]
    [InlineData("/.well-known/jwks/", OpenIddictServerEndpointType.Cryptography)]
    [InlineData("/.WELL-KNOWN/JWKS/", OpenIddictServerEndpointType.Cryptography)]
    [InlineData("/.well-known/jwks/subpath", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/.WELL-KNOWN/JWKS/SUBPATH", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/.well-known/jwks/subpath/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/.WELL-KNOWN/JWKS/SUBPATH/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/connect/device", OpenIddictServerEndpointType.Device)]
    [InlineData("/CONNECT/DEVICE", OpenIddictServerEndpointType.Device)]
    [InlineData("/connect/device/", OpenIddictServerEndpointType.Device)]
    [InlineData("/CONNECT/DEVICE/", OpenIddictServerEndpointType.Device)]
    [InlineData("/connect/device/subpath", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/CONNECT/DEVICE/SUBPATH", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/connect/device/subpath/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/CONNECT/DEVICE/SUBPATH/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/connect/introspect", OpenIddictServerEndpointType.Introspection)]
    [InlineData("/CONNECT/INTROSPECT", OpenIddictServerEndpointType.Introspection)]
    [InlineData("/connect/introspect/", OpenIddictServerEndpointType.Introspection)]
    [InlineData("/CONNECT/INTROSPECT/", OpenIddictServerEndpointType.Introspection)]
    [InlineData("/connect/introspect/subpath", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/CONNECT/INTROSPECT/SUBPATH", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/connect/introspect/subpath/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/CONNECT/INTROSPECT/SUBPATH/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/connect/logout", OpenIddictServerEndpointType.Logout)]
    [InlineData("/CONNECT/LOGOUT", OpenIddictServerEndpointType.Logout)]
    [InlineData("/connect/logout/", OpenIddictServerEndpointType.Logout)]
    [InlineData("/CONNECT/LOGOUT/", OpenIddictServerEndpointType.Logout)]
    [InlineData("/connect/logout/subpath", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/CONNECT/LOGOUT/SUBPATH", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/connect/logout/subpath/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/CONNECT/LOGOUT/SUBPATH/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/connect/revoke", OpenIddictServerEndpointType.Revocation)]
    [InlineData("/CONNECT/REVOKE", OpenIddictServerEndpointType.Revocation)]
    [InlineData("/connect/revoke/", OpenIddictServerEndpointType.Revocation)]
    [InlineData("/CONNECT/REVOKE/", OpenIddictServerEndpointType.Revocation)]
    [InlineData("/connect/revoke/subpath", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/CONNECT/REVOKE/SUBPATH", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/connect/revoke/subpath/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/CONNECT/REVOKE/SUBPATH/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/connect/token", OpenIddictServerEndpointType.Token)]
    [InlineData("/CONNECT/TOKEN", OpenIddictServerEndpointType.Token)]
    [InlineData("/connect/token/", OpenIddictServerEndpointType.Token)]
    [InlineData("/CONNECT/TOKEN/", OpenIddictServerEndpointType.Token)]
    [InlineData("/connect/token/subpath", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/CONNECT/TOKEN/SUBPATH", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/connect/token/subpath/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/CONNECT/TOKEN/SUBPATH/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/connect/userinfo", OpenIddictServerEndpointType.Userinfo)]
    [InlineData("/CONNECT/USERINFO", OpenIddictServerEndpointType.Userinfo)]
    [InlineData("/connect/userinfo/", OpenIddictServerEndpointType.Userinfo)]
    [InlineData("/CONNECT/USERINFO/", OpenIddictServerEndpointType.Userinfo)]
    [InlineData("/connect/userinfo/subpath", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/CONNECT/USERINFO/SUBPATH", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/connect/userinfo/subpath/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/CONNECT/USERINFO/SUBPATH/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/connect/verification", OpenIddictServerEndpointType.Verification)]
    [InlineData("/CONNECT/VERIFICATION", OpenIddictServerEndpointType.Verification)]
    [InlineData("/connect/verification/", OpenIddictServerEndpointType.Verification)]
    [InlineData("/CONNECT/VERIFICATION/", OpenIddictServerEndpointType.Verification)]
    [InlineData("/connect/verification/subpath", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/CONNECT/VERIFICATION/SUBPATH", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/connect/verification/subpath/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/CONNECT/VERIFICATION/SUBPATH/", OpenIddictServerEndpointType.Unknown)]
    public async Task ProcessRequest_MatchesCorrespondingRelativeEndpoint(string path, OpenIddictServerEndpointType type)
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<HandleLogoutRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SkipRequest();

                    return default;
                }));

            options.AddEventHandler<HandleVerificationRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SkipRequest();

                    return default;
                }));

            options.AddEventHandler<ProcessRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    // Assert
                    Assert.Equal(type, context.EndpointType);

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act
        await client.PostAsync(path, new OpenIddictRequest());
    }

    [Theory]
    [InlineData("https://localhost/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://localhost:443/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://localhost/connect", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://LOCALHOST/CONNECT", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://localhost/connect/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://LOCALHOST/CONNECT/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://localhost:443/connect", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://LOCALHOST:443/CONNECT", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://localhost:443/connect/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://LOCALHOST:443/CONNECT/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://localhost/connect/authorize", OpenIddictServerEndpointType.Authorization)]
    [InlineData("HTTPS://LOCALHOST/CONNECT/AUTHORIZE", OpenIddictServerEndpointType.Authorization)]
    [InlineData("https://localhost/connect/authorize/", OpenIddictServerEndpointType.Authorization)]
    [InlineData("HTTPS://LOCALHOST/CONNECT/AUTHORIZE/", OpenIddictServerEndpointType.Authorization)]
    [InlineData("https://localhost:443/connect/authorize", OpenIddictServerEndpointType.Authorization)]
    [InlineData("HTTPS://LOCALHOST:443/CONNECT/AUTHORIZE", OpenIddictServerEndpointType.Authorization)]
    [InlineData("https://localhost:443/connect/authorize/", OpenIddictServerEndpointType.Authorization)]
    [InlineData("HTTPS://LOCALHOST:443/CONNECT/AUTHORIZE/", OpenIddictServerEndpointType.Authorization)]
    [InlineData("https://fabrikam.com/connect/authorize", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://FABRIKAM.COM/CONNECT/AUTHORIZE", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://fabrikam.com/connect/authorize/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://FABRIKAM.COM/CONNECT/AUTHORIZE/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://localhost:8888/connect/authorize", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://LOCALHOST:8888/CONNECT/AUTHORIZE", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://localhost:8888/connect/authorize/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://LOCALHOST:8888/CONNECT/AUTHORIZE/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://localhost/.well-known/openid-configuration", OpenIddictServerEndpointType.Configuration)]
    [InlineData("HTTPS://LOCALHOST/.WELL-KNOWN/OPENID-CONFIGURATION", OpenIddictServerEndpointType.Configuration)]
    [InlineData("https://localhost/.well-known/openid-configuration/", OpenIddictServerEndpointType.Configuration)]
    [InlineData("HTTPS://LOCALHOST/.WELL-KNOWN/OPENID-CONFIGURATION/", OpenIddictServerEndpointType.Configuration)]
    [InlineData("https://localhost:443/.well-known/openid-configuration", OpenIddictServerEndpointType.Configuration)]
    [InlineData("HTTPS://LOCALHOST:443/.WELL-KNOWN/OPENID-CONFIGURATION", OpenIddictServerEndpointType.Configuration)]
    [InlineData("https://localhost:443/.well-known/openid-configuration/", OpenIddictServerEndpointType.Configuration)]
    [InlineData("HTTPS://LOCALHOST:443/.WELL-KNOWN/OPENID-CONFIGURATION/", OpenIddictServerEndpointType.Configuration)]
    [InlineData("https://fabrikam.com/.well-known/openid-configuration", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://FABRIKAM.COM/.WELL-KNOWN/OPENID-CONFIGURATION", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://fabrikam.com/.well-known/openid-configuration/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://FABRIKAM.COM/.WELL-KNOWN/OPENID-CONFIGURATION/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://localhost:8888/.well-known/openid-configuration", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://LOCALHOST:8888/.WELL-KNOWN/OPENID-CONFIGURATION", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://localhost:8888/.well-known/openid-configuration/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://LOCALHOST:8888/.WELL-KNOWN/OPENID-CONFIGURATION/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://localhost/.well-known/jwks", OpenIddictServerEndpointType.Cryptography)]
    [InlineData("HTTPS://LOCALHOST/.WELL-KNOWN/JWKS", OpenIddictServerEndpointType.Cryptography)]
    [InlineData("https://localhost/.well-known/jwks/", OpenIddictServerEndpointType.Cryptography)]
    [InlineData("HTTPS://LOCALHOST/.WELL-KNOWN/JWKS/", OpenIddictServerEndpointType.Cryptography)]
    [InlineData("https://localhost:443/.well-known/jwks", OpenIddictServerEndpointType.Cryptography)]
    [InlineData("HTTPS://LOCALHOST:443/.WELL-KNOWN/JWKS", OpenIddictServerEndpointType.Cryptography)]
    [InlineData("https://localhost:443/.well-known/jwks/", OpenIddictServerEndpointType.Cryptography)]
    [InlineData("HTTPS://LOCALHOST:443/.WELL-KNOWN/JWKS/", OpenIddictServerEndpointType.Cryptography)]
    [InlineData("https://fabrikam.com/.well-known/jwks", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://FABRIKAM.COM/.WELL-KNOWN/JWKS", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://fabrikam.com/.well-known/jwks/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://FABRIKAM.COM/.WELL-KNOWN/JWKS/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://localhost:8888/.well-known/jwks", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://LOCALHOST:8888/.WELL-KNOWN/JWKS", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://localhost:8888/.well-known/jwks/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://LOCALHOST:8888/.WELL-KNOWN/JWKS/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://localhost/connect/device", OpenIddictServerEndpointType.Device)]
    [InlineData("HTTPS://LOCALHOST/CONNECT/DEVICE", OpenIddictServerEndpointType.Device)]
    [InlineData("https://localhost/connect/device/", OpenIddictServerEndpointType.Device)]
    [InlineData("HTTPS://LOCALHOST/CONNECT/DEVICE/", OpenIddictServerEndpointType.Device)]
    [InlineData("https://localhost:443/connect/device", OpenIddictServerEndpointType.Device)]
    [InlineData("HTTPS://LOCALHOST:443/CONNECT/DEVICE", OpenIddictServerEndpointType.Device)]
    [InlineData("https://localhost:443/connect/device/", OpenIddictServerEndpointType.Device)]
    [InlineData("HTTPS://LOCALHOST:443/CONNECT/DEVICE/", OpenIddictServerEndpointType.Device)]
    [InlineData("https://fabrikam.com/connect/device", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://FABRIKAM.COM/CONNECT/DEVICE", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://fabrikam.com/connect/device/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://FABRIKAM.COM/CONNECT/DEVICE/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://localhost:8888/connect/device", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://LOCALHOST:8888/CONNECT/DEVICE", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://localhost:8888/connect/device/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://LOCALHOST:8888/CONNECT/DEVICE/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://localhost/connect/introspect", OpenIddictServerEndpointType.Introspection)]
    [InlineData("HTTPS://LOCALHOST/CONNECT/INTROSPECT", OpenIddictServerEndpointType.Introspection)]
    [InlineData("https://localhost/connect/introspect/", OpenIddictServerEndpointType.Introspection)]
    [InlineData("HTTPS://LOCALHOST/CONNECT/INTROSPECT/", OpenIddictServerEndpointType.Introspection)]
    [InlineData("https://localhost:443/connect/introspect", OpenIddictServerEndpointType.Introspection)]
    [InlineData("HTTPS://LOCALHOST:443/CONNECT/INTROSPECT", OpenIddictServerEndpointType.Introspection)]
    [InlineData("https://localhost:443/connect/introspect/", OpenIddictServerEndpointType.Introspection)]
    [InlineData("HTTPS://LOCALHOST:443/CONNECT/INTROSPECT/", OpenIddictServerEndpointType.Introspection)]
    [InlineData("https://fabrikam.com/connect/introspect", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://FABRIKAM.COM/CONNECT/INTROSPECT", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://fabrikam.com/connect/introspect/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://FABRIKAM.COM/CONNECT/INTROSPECT/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://localhost:8888/connect/introspect", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://LOCALHOST:8888/CONNECT/INTROSPECT", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://localhost:8888/connect/introspect/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://LOCALHOST:8888/CONNECT/INTROSPECT/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://localhost/connect/logout", OpenIddictServerEndpointType.Logout)]
    [InlineData("HTTPS://LOCALHOST/CONNECT/LOGOUT", OpenIddictServerEndpointType.Logout)]
    [InlineData("https://localhost/connect/logout/", OpenIddictServerEndpointType.Logout)]
    [InlineData("HTTPS://LOCALHOST/CONNECT/LOGOUT/", OpenIddictServerEndpointType.Logout)]
    [InlineData("https://localhost:443/connect/logout", OpenIddictServerEndpointType.Logout)]
    [InlineData("HTTPS://LOCALHOST:443/CONNECT/LOGOUT", OpenIddictServerEndpointType.Logout)]
    [InlineData("https://localhost:443/connect/logout/", OpenIddictServerEndpointType.Logout)]
    [InlineData("HTTPS://LOCALHOST:443/CONNECT/LOGOUT/", OpenIddictServerEndpointType.Logout)]
    [InlineData("https://fabrikam.com/connect/logout", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://FABRIKAM.COM/CONNECT/LOGOUT", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://fabrikam.com/connect/logout/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://FABRIKAM.COM/CONNECT/LOGOUT/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://localhost:8888/connect/logout", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://LOCALHOST:8888/CONNECT/LOGOUT", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://localhost:8888/connect/logout/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://LOCALHOST:8888/CONNECT/LOGOUT/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://localhost/connect/revoke", OpenIddictServerEndpointType.Revocation)]
    [InlineData("HTTPS://LOCALHOST/CONNECT/REVOKE", OpenIddictServerEndpointType.Revocation)]
    [InlineData("https://localhost/connect/revoke/", OpenIddictServerEndpointType.Revocation)]
    [InlineData("HTTPS://LOCALHOST/CONNECT/REVOKE/", OpenIddictServerEndpointType.Revocation)]
    [InlineData("https://localhost:443/connect/revoke", OpenIddictServerEndpointType.Revocation)]
    [InlineData("HTTPS://LOCALHOST:443/CONNECT/REVOKE", OpenIddictServerEndpointType.Revocation)]
    [InlineData("https://localhost:443/connect/revoke/", OpenIddictServerEndpointType.Revocation)]
    [InlineData("HTTPS://LOCALHOST:443/CONNECT/REVOKE/", OpenIddictServerEndpointType.Revocation)]
    [InlineData("https://fabrikam.com/connect/revoke", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://FABRIKAM.COM/CONNECT/REVOKE", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://fabrikam.com/connect/revoke/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://FABRIKAM.COM/CONNECT/REVOKE/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://localhost:8888/connect/revoke", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://LOCALHOST:8888/CONNECT/REVOKE", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://localhost:8888/connect/revoke/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://LOCALHOST:8888/CONNECT/REVOKE/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://localhost/connect/token", OpenIddictServerEndpointType.Token)]
    [InlineData("HTTPS://LOCALHOST/CONNECT/TOKEN", OpenIddictServerEndpointType.Token)]
    [InlineData("https://localhost/connect/token/", OpenIddictServerEndpointType.Token)]
    [InlineData("HTTPS://LOCALHOST/CONNECT/TOKEN/", OpenIddictServerEndpointType.Token)]
    [InlineData("https://localhost:443/connect/token", OpenIddictServerEndpointType.Token)]
    [InlineData("HTTPS://LOCALHOST:443/CONNECT/TOKEN", OpenIddictServerEndpointType.Token)]
    [InlineData("https://localhost:443/connect/token/", OpenIddictServerEndpointType.Token)]
    [InlineData("HTTPS://LOCALHOST:443/CONNECT/TOKEN/", OpenIddictServerEndpointType.Token)]
    [InlineData("https://fabrikam.com/connect/token", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://FABRIKAM.COM/CONNECT/TOKEN", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://fabrikam.com/connect/token/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://FABRIKAM.COM/CONNECT/TOKEN/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://localhost:8888/connect/token", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://LOCALHOST:8888/CONNECT/TOKEN", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://localhost:8888/connect/token/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://LOCALHOST:8888/CONNECT/TOKEN/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://localhost/connect/userinfo", OpenIddictServerEndpointType.Userinfo)]
    [InlineData("HTTPS://LOCALHOST/CONNECT/USERINFO", OpenIddictServerEndpointType.Userinfo)]
    [InlineData("https://localhost/connect/userinfo/", OpenIddictServerEndpointType.Userinfo)]
    [InlineData("HTTPS://LOCALHOST/CONNECT/USERINFO/", OpenIddictServerEndpointType.Userinfo)]
    [InlineData("https://localhost:443/connect/userinfo", OpenIddictServerEndpointType.Userinfo)]
    [InlineData("HTTPS://LOCALHOST:443/CONNECT/USERINFO", OpenIddictServerEndpointType.Userinfo)]
    [InlineData("https://localhost:443/connect/userinfo/", OpenIddictServerEndpointType.Userinfo)]
    [InlineData("HTTPS://LOCALHOST:443/CONNECT/USERINFO/", OpenIddictServerEndpointType.Userinfo)]
    [InlineData("https://fabrikam.com/connect/userinfo", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://FABRIKAM.COM/CONNECT/USERINFO", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://fabrikam.com/connect/userinfo/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://FABRIKAM.COM/CONNECT/USERINFO/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://localhost:8888/connect/userinfo", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://LOCALHOST:8888/CONNECT/USERINFO", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://localhost:8888/connect/userinfo/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://LOCALHOST:8888/CONNECT/USERINFO/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://localhost/connect/verification", OpenIddictServerEndpointType.Verification)]
    [InlineData("HTTPS://LOCALHOST/CONNECT/VERIFICATION", OpenIddictServerEndpointType.Verification)]
    [InlineData("https://localhost/connect/verification/", OpenIddictServerEndpointType.Verification)]
    [InlineData("HTTPS://LOCALHOST/CONNECT/VERIFICATION/", OpenIddictServerEndpointType.Verification)]
    [InlineData("https://localhost:443/connect/verification", OpenIddictServerEndpointType.Verification)]
    [InlineData("HTTPS://LOCALHOST:443/CONNECT/VERIFICATION", OpenIddictServerEndpointType.Verification)]
    [InlineData("https://localhost:443/connect/verification/", OpenIddictServerEndpointType.Verification)]
    [InlineData("HTTPS://LOCALHOST:443/CONNECT/VERIFICATION/", OpenIddictServerEndpointType.Verification)]
    [InlineData("https://fabrikam.com/connect/verification", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://FABRIKAM.COM/CONNECT/VERIFICATION", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://fabrikam.com/connect/verification/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://FABRIKAM.COM/CONNECT/VERIFICATION/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://localhost:8888/connect/verification", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://LOCALHOST:8888/CONNECT/VERIFICATION", OpenIddictServerEndpointType.Unknown)]
    [InlineData("https://localhost:8888/connect/verification/", OpenIddictServerEndpointType.Unknown)]
    [InlineData("HTTPS://LOCALHOST:8888/CONNECT/VERIFICATION/", OpenIddictServerEndpointType.Unknown)]
    public async Task ProcessRequest_MatchesCorrespondingAbsoluteEndpoint(string path, OpenIddictServerEndpointType type)
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.SetAuthorizationEndpointUris("https://localhost/connect/authorize")
                   .SetConfigurationEndpointUris("https://localhost/.well-known/openid-configuration")
                   .SetCryptographyEndpointUris("https://localhost/.well-known/jwks")
                   .SetDeviceEndpointUris("https://localhost/connect/device")
                   .SetIntrospectionEndpointUris("https://localhost/connect/introspect")
                   .SetLogoutEndpointUris("https://localhost/connect/logout")
                   .SetRevocationEndpointUris("https://localhost/connect/revoke")
                   .SetTokenEndpointUris("https://localhost/connect/token")
                   .SetUserinfoEndpointUris("https://localhost/connect/userinfo")
                   .SetVerificationEndpointUris("https://localhost/connect/verification");

            options.AddEventHandler<HandleLogoutRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SkipRequest();

                    return default;
                }));

            options.AddEventHandler<HandleVerificationRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SkipRequest();

                    return default;
                }));

            options.AddEventHandler<ProcessRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    // Assert
                    Assert.Equal(type, context.EndpointType);

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act
        await client.PostAsync(path, new OpenIddictRequest());
    }

    [Theory]
    [InlineData("/custom/connect/authorize", OpenIddictServerEndpointType.Authorization)]
    [InlineData("/custom/.well-known/openid-configuration", OpenIddictServerEndpointType.Configuration)]
    [InlineData("/custom/.well-known/jwks", OpenIddictServerEndpointType.Cryptography)]
    [InlineData("/custom/connect/device", OpenIddictServerEndpointType.Device)]
    [InlineData("/custom/connect/custom", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/custom/connect/introspect", OpenIddictServerEndpointType.Introspection)]
    [InlineData("/custom/connect/logout", OpenIddictServerEndpointType.Logout)]
    [InlineData("/custom/connect/revoke", OpenIddictServerEndpointType.Revocation)]
    [InlineData("/custom/connect/token", OpenIddictServerEndpointType.Token)]
    [InlineData("/custom/connect/userinfo", OpenIddictServerEndpointType.Userinfo)]
    [InlineData("/custom/connect/verification", OpenIddictServerEndpointType.Verification)]
    public async Task ProcessRequest_AllowsOverridingEndpoint(string address, OpenIddictServerEndpointType type)
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<HandleLogoutRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SkipRequest();

                    return default;
                }));

            options.AddEventHandler<HandleVerificationRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SkipRequest();

                    return default;
                }));

            options.AddEventHandler<ProcessRequestContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    // Act
                    context.EndpointType = type;

                    // Assert
                    Assert.Equal(type, context.EndpointType);

                    return default;
                });

                builder.SetOrder(InferEndpointType.Descriptor.Order + 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        await client.PostAsync(address, new OpenIddictRequest());
    }

    [Theory]
    [InlineData("/.well-known/openid-configuration")]
    [InlineData("/.well-known/jwks")]
    [InlineData("/connect/authorize")]
    [InlineData("/connect/device")]
    [InlineData("/connect/introspect")]
    [InlineData("/connect/logout")]
    [InlineData("/connect/revoke")]
    [InlineData("/connect/token")]
    [InlineData("/connect/userinfo")]
    [InlineData("/connect/verification")]
    public async Task ProcessRequest_RejectsInsecureHttpRequests(string address)
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.UseOwin()
                   .Configure(options => options.DisableTransportSecurityRequirement = false);
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync(address, new OpenIddictRequest());

        // Assert
        Assert.Equal(Errors.InvalidRequest, response.Error);
        Assert.Equal(SR.GetResourceString(SR.ID2083), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2083), response.ErrorUri);
    }

    [Theory]
    [InlineData("/.well-known/openid-configuration")]
    [InlineData("/.well-known/jwks")]
    [InlineData("/custom")]
    [InlineData("/connect/authorize")]
    [InlineData("/connect/device")]
    [InlineData("/connect/introspect")]
    [InlineData("/connect/logout")]
    [InlineData("/connect/revoke")]
    [InlineData("/connect/token")]
    [InlineData("/connect/userinfo")]
    [InlineData("/connect/verification")]
    public async Task ProcessRequest_AllowsHandlingResponse(string address)
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<ProcessRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Transaction.SetProperty("custom_response", new
                    {
                        name = "Bob le Bricoleur"
                    });

                    context.HandleRequest();

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync(address, new OpenIddictRequest());

        // Assert
        Assert.Equal("Bob le Bricoleur", (string?) response["name"]);
    }

    [Theory]
    [InlineData("/.well-known/openid-configuration")]
    [InlineData("/.well-known/jwks")]
    [InlineData("/custom")]
    [InlineData("/connect/authorize")]
    [InlineData("/connect/device")]
    [InlineData("/connect/introspect")]
    [InlineData("/connect/logout")]
    [InlineData("/connect/revoke")]
    [InlineData("/connect/token")]
    [InlineData("/connect/userinfo")]
    [InlineData("/connect/verification")]
    public async Task ProcessRequest_AllowsSkippingHandler(string address)
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<ProcessRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SkipRequest();

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync(address, new OpenIddictRequest());

        // Assert
        Assert.Equal("Bob le Magnifique", (string?) response["name"]);
    }

    [Fact]
    public async Task ProcessSignIn_ReturnsParametersFromAuthenticationProperties()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.SetTokenEndpointUris("/signin/custom");

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SkipRequest();

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/signin/custom", new OpenIddictRequest
        {
            GrantType = GrantTypes.Password,
            Username = "johndoe",
            Password = "A3ddj3w"
        });

        // Assert
        Assert.True((bool) response["boolean_parameter"]);
        Assert.Equal(JsonValueKind.True, ((JsonElement) response["boolean_parameter"]).ValueKind);
        Assert.Equal(42, (long) response["integer_parameter"]);
        Assert.Equal(JsonValueKind.Number, ((JsonElement) response["integer_parameter"]).ValueKind);
        Assert.Equal("Bob l'Eponge", (string?) response["string_parameter"]);
        Assert.Equal(JsonValueKind.String, ((JsonElement) response["string_parameter"]).ValueKind);
        Assert.Equal(new[] { "Contoso", "Fabrikam" }, (string[]?) response["json_parameter"]);
        Assert.Equal(JsonValueKind.Array, ((JsonElement) response["json_parameter"]).ValueKind);
    }

    [Fact]
    public async Task ProcessSignOut_ReturnsParametersFromAuthenticationProperties()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.SetLogoutEndpointUris("/signout/custom");

            options.AddEventHandler<HandleLogoutRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SkipRequest();

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/signout/custom", new OpenIddictRequest
        {
            PostLogoutRedirectUri = "http://www.fabrikam.com/path",
            State = "af0ifjsldkj"
        });

        // Assert
        Assert.True((bool) response["boolean_parameter"]);
        Assert.Equal(42, (long) response["integer_parameter"]);
        Assert.Equal("Bob l'Eponge", (string?) response["string_parameter"]);
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "The caller is responsible for disposing the test server.")]
    protected override ValueTask<OpenIddictServerIntegrationTestServer> CreateServerAsync(Action<OpenIddictServerBuilder>? configuration = null)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);

        services.AddLogging(options => options.AddXUnit(OutputHelper));

        services.AddOpenIddict()
            .AddServer(options =>
            {
                // Disable the transport security requirement during testing.
                options.UseOwin()
                       .DisableTransportSecurityRequirement();

                configuration?.Invoke(options);
            });

        var provider = services.BuildServiceProvider();

        var server = TestServer.Create(app =>
        {
            app.Use(async (context, next) =>
            {
                using var scope = provider.CreateScope();

                context.Set(typeof(IServiceProvider).FullName, scope.ServiceProvider);

                try
                {
                    await next();
                }

                finally
                {
                    context.Environment.Remove(typeof(IServiceProvider).FullName);
                }
            });

            app.Use(async (context, next) =>
            {
                await next();

                var transaction = context.Get<OpenIddictServerTransaction>(typeof(OpenIddictServerTransaction).FullName);
                var response = transaction?.GetProperty<object>("custom_response");
                if (response is not null)
                {
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(response));
                }
            });

            app.UseOpenIddictServer();

            app.Use(async (context, next) =>
            {
                if (context.Request.Path == new PathString("/signin"))
                {
                    var identity = new ClaimsIdentity(OpenIddictServerOwinDefaults.AuthenticationType);
                    identity.AddClaim(Claims.Subject, "Bob le Bricoleur");

                    context.Authentication.SignIn(identity);
                    return;
                }

                else if (context.Request.Path == new PathString("/signin/custom"))
                {
                    var identity = new ClaimsIdentity(OpenIddictServerOwinDefaults.AuthenticationType);
                    identity.AddClaim(Claims.Subject, "Bob le Bricoleur");

                    var principal = new ClaimsPrincipal(identity);

                    var properties = new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        ["boolean_parameter#boolean"] = "true",
                        ["integer_parameter#integer"] = "42",
                        ["string_parameter#string"] = "Bob l'Eponge",
                        ["json_parameter#json"] = @"[""Contoso"",""Fabrikam""]"
                    });

                    context.Authentication.SignIn(properties, identity);
                    return;
                }

                else if (context.Request.Path == new PathString("/signout"))
                {
                    context.Authentication.SignOut(OpenIddictServerOwinDefaults.AuthenticationType);
                    return;
                }

                else if (context.Request.Path == new PathString("/signout/custom"))
                {

                    var properties = new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        ["boolean_parameter#boolean"] = "true",
                        ["integer_parameter#integer"] = "42",
                        ["string_parameter#string"] = "Bob l'Eponge"
                    });

                    context.Authentication.SignOut(properties, OpenIddictServerOwinDefaults.AuthenticationType);
                    return;
                }

                else if (context.Request.Path == new PathString("/challenge"))
                {
                    context.Authentication.Challenge(OpenIddictServerOwinDefaults.AuthenticationType);
                    return;
                }

                else if (context.Request.Path == new PathString("/challenge/custom"))
                {
                    var properties = new AuthenticationProperties(new Dictionary<string, string>
                    {
                        [OpenIddictServerOwinConstants.Properties.Error] = "custom_error",
                        [OpenIddictServerOwinConstants.Properties.ErrorDescription] = "custom_error_description",
                        [OpenIddictServerOwinConstants.Properties.ErrorUri] = "custom_error_uri",

                        ["boolean_parameter#boolean"] = "true",
                        ["integer_parameter#integer"] = "42",
                        ["string_parameter#string"] = "Bob l'Eponge",
                        ["json_parameter#json"] = @"[""Contoso"",""Fabrikam""]"
                    });

                    context.Authentication.Challenge(properties, OpenIddictServerOwinDefaults.AuthenticationType);
                    return;
                }

                else if (context.Request.Path == new PathString("/authenticate"))
                {
                    var result = await context.Authentication.AuthenticateAsync(OpenIddictServerOwinDefaults.AuthenticationType);
                    if (result?.Identity is null)
                    {
                        return;
                    }

                    var claims = result.Identity.Claims.GroupBy(claim => claim.Type)
                        .Select(group => new KeyValuePair<string, string?[]?>(
                            group.Key, group.Select(claim => claim.Value).ToArray()));

                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new OpenIddictResponse(claims)));
                    return;
                }

                else if (context.Request.Path == new PathString("/authenticate/properties"))
                {
                    var result = await context.Authentication.AuthenticateAsync(OpenIddictServerOwinDefaults.AuthenticationType);
                    if (result?.Properties is null)
                    {
                        return;
                    }

                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new OpenIddictResponse(result.Properties.Dictionary)));
                    return;
                }

                await next();
            });

            app.Run(context =>
            {
                context.Response.ContentType = "application/json";
                return context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    name = "Bob le Magnifique"
                }));
            });
        });

        return new(new OpenIddictServerOwinIntegrationTestServer(server));
    }
}
