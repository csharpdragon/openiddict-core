﻿

/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OpenIddict.Core;
using Xunit;
using Xunit.Abstractions;
using static OpenIddict.Server.OpenIddictServerEvents;
using static OpenIddict.Server.OpenIddictServerHandlers;
using static OpenIddict.Server.OpenIddictServerHandlers.Protection;

namespace OpenIddict.Server.IntegrationTests;

public abstract partial class OpenIddictServerIntegrationTests
{
    protected OpenIddictServerIntegrationTests(ITestOutputHelper outputHelper)
    {
        OutputHelper = outputHelper;
    }

    protected ITestOutputHelper OutputHelper { get; }

    [Fact]
    public async Task ProcessAuthentication_UnknownEndpointCausesAnException()
    {
        // Arrange
        await using var server = await CreateServerAsync(options => options.EnableDegradedMode());
        await using var client = await server.CreateClientAsync();

        // Act and assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(delegate
        {
            return client.PostAsync("/authenticate", new OpenIddictRequest());
        });

        Assert.Equal(SR.GetResourceString(SR.ID0002), exception.Message);
    }

    [Fact]
    public async Task ProcessAuthentication_InvalidEndpointCausesAnException()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.SetConfigurationEndpointUris("/authenticate");

            options.AddEventHandler<HandleConfigurationRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SkipRequest();

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act and assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(delegate
        {
            return client.GetAsync("/authenticate");
        });

        Assert.Equal(SR.GetResourceString(SR.ID0002), exception.Message);
    }

    [Fact]
    public async Task ProcessAuthentication_UnsupportedGrantTypeThrowsAnException()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.SetTokenEndpointUris("/authenticate");

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SkipRequest();

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act and assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(delegate
        {
            return client.PostAsync("/authenticate", new OpenIddictRequest
            {
                GrantType = GrantTypes.Password,
                Username = "johndoe",
                Password = "A3ddj3w",
            });
        });

        Assert.Equal(SR.GetResourceString(SR.ID0001), exception.Message);
    }

    [Fact]
    public async Task ProcessAuthentication_MissingAccessTokenReturnsNull()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.SetLogoutEndpointUris("/authenticate");

            options.AddEventHandler<HandleLogoutRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SkipRequest();

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.GetAsync("/authenticate", new OpenIddictRequest
        {
            AccessToken = null
        });

        // Assert
        Assert.Null((string?) response[Claims.Subject]);
    }

    [Fact]
    public async Task ProcessAuthentication_InvalidAccessTokenReturnsNull()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.SetLogoutEndpointUris("/authenticate");

            options.AddEventHandler<HandleLogoutRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SkipRequest();

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.GetAsync("/authenticate", new OpenIddictRequest
        {
            AccessToken = "38323A4B-6CB2-41B8-B457-1951987CB383"
        });

        // Assert
        Assert.Null((string?) response[Claims.Subject]);
    }

    [Fact]
    public async Task ProcessAuthentication_ValidAccessTokenReturnsExpectedIdentity()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.SetUserinfoEndpointUris("/authenticate");

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
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.GetAsync("/authenticate", new OpenIddictRequest
        {
            AccessToken = "access_token"
        });

        // Assert
        Assert.Equal("Bob le Magnifique", (string?) response[Claims.Subject]);
    }

    [Fact]
    public async Task ProcessAuthentication_MissingIdTokenHintReturnsNull()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.SetLogoutEndpointUris("/authenticate");

            options.AddEventHandler<HandleLogoutRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SkipRequest();

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.GetAsync("/authenticate", new OpenIddictRequest
        {
            IdTokenHint = null
        });

        // Assert
        Assert.Null((string?) response[Claims.Subject]);
    }

    [Fact]
    public async Task ProcessAuthentication_InvalidIdTokenHintReturnsNull()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.SetLogoutEndpointUris("/authenticate");

            options.AddEventHandler<HandleLogoutRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SkipRequest();

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.GetAsync("/authenticate", new OpenIddictRequest
        {
            IdTokenHint = "38323A4B-6CB2-41B8-B457-1951987CB383"
        });

        // Assert
        Assert.Null((string?) response[Claims.Subject]);
    }

    [Fact]
    public async Task ProcessAuthentication_ValidIdTokenHintReturnsExpectedIdentity()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.SetLogoutEndpointUris("/authenticate");

            options.AddEventHandler<HandleLogoutRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SkipRequest();

                    return default;
                }));

            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal("id_token", context.Token);
                    Assert.Equal(new[] { TokenTypeHints.IdToken }, context.ValidTokenTypes);

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetTokenType(TokenTypeHints.IdToken)
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.GetAsync("/authenticate", new OpenIddictRequest
        {
            IdTokenHint = "id_token"
        });

        // Assert
        Assert.Equal("Bob le Magnifique", (string?) response[Claims.Subject]);
    }

    [Fact]
    public async Task ProcessAuthentication_MissingAuthorizationCodeReturnsNull()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.SetTokenEndpointUris("/authenticate");

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SkipRequest();

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/authenticate", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            Code = null,
            GrantType = GrantTypes.AuthorizationCode
        });

        // Assert
        Assert.Null((string?) response[Claims.Subject]);
    }

    [Fact]
    public async Task ProcessAuthentication_InvalidAuthorizationCodeReturnsNull()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.SetTokenEndpointUris("/authenticate");

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SkipRequest();

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/authenticate", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            Code = "38323A4B-6CB2-41B8-B457-1951987CB383",
            GrantType = GrantTypes.AuthorizationCode
        });

        // Assert
        Assert.Null((string?) response[Claims.Subject]);
    }

    [Fact]
    public async Task ProcessAuthentication_ValidAuthorizationCodeReturnsExpectedIdentity()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.SetTokenEndpointUris("/authenticate");

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SkipRequest();

                    return default;
                }));

            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal("authorization_code", context.Token);
                    Assert.Equal(new[] { TokenTypeHints.AuthorizationCode }, context.ValidTokenTypes);

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetTokenType(TokenTypeHints.AuthorizationCode)
                        .SetClaim(Claims.Subject, "Bob le Magnifique")
                        .SetPresenters("Fabrikam");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/authenticate", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            Code = "authorization_code",
            GrantType = GrantTypes.AuthorizationCode
        });

        // Assert
        Assert.Equal("Bob le Magnifique", (string?) response[Claims.Subject]);
    }

    [Fact]
    public async Task ProcessAuthentication_MissingRefreshTokenReturnsNull()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.SetTokenEndpointUris("/authenticate");

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SkipRequest();

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/authenticate", new OpenIddictRequest
        {
            GrantType = GrantTypes.RefreshToken,
            RefreshToken = null
        });

        // Assert
        Assert.Null((string?) response[Claims.Subject]);
    }

    [Fact]
    public async Task ProcessAuthentication_InvalidRefreshTokenReturnsNull()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.SetTokenEndpointUris("/authenticate");

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SkipRequest();

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/authenticate", new OpenIddictRequest
        {
            GrantType = GrantTypes.RefreshToken,
            RefreshToken = "38323A4B-6CB2-41B8-B457-1951987CB383"
        });

        // Assert
        Assert.Null((string?) response[Claims.Subject]);
    }

    [Fact]
    public async Task ProcessAuthentication_ValidRefreshTokenReturnsExpectedIdentity()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.SetTokenEndpointUris("/authenticate");

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SkipRequest();

                    return default;
                }));

            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal("refresh_token", context.Token);
                    Assert.Equal(new[] { TokenTypeHints.RefreshToken }, context.ValidTokenTypes);

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetTokenType(TokenTypeHints.RefreshToken)
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/authenticate", new OpenIddictRequest
        {
            GrantType = GrantTypes.RefreshToken,
            RefreshToken = "refresh_token"
        });

        // Assert
        Assert.Equal("Bob le Magnifique", (string?) response[Claims.Subject]);
    }

    [Fact]
    public async Task ProcessChallenge_UnknownEndpointCausesAnException()
    {
        // Arrange
        await using var server = await CreateServerAsync(options => options.EnableDegradedMode());
        await using var client = await server.CreateClientAsync();

        // Act and assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(delegate
        {
            return client.PostAsync("/challenge", new OpenIddictRequest());
        });

        Assert.Equal(SR.GetResourceString(SR.ID0006), exception.Message);
    }

    [Fact]
    public async Task ProcessChallenge_InvalidEndpointCausesAnException()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.SetConfigurationEndpointUris("/challenge");

            options.AddEventHandler<HandleConfigurationRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SkipRequest();

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act and assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(delegate
        {
            return client.GetAsync("/challenge");
        });

        Assert.Equal(SR.GetResourceString(SR.ID0006), exception.Message);
    }

    [Fact]
    public async Task ProcessChallenge_ReturnsDefaultErrorForAuthorizationRequestsWhenNoneIsSpecified()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.SetAuthorizationEndpointUris("/challenge");

            options.AddEventHandler<HandleAuthorizationRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SkipRequest();

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/challenge", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            Nonce = "n-0S6_WzA2Mj",
            RedirectUri = "http://www.fabrikam.com/path",
            ResponseType = ResponseTypes.Code,
            Scope = Scopes.OpenId
        });

        // Assert
        Assert.Equal(Errors.AccessDenied, response.Error);
        Assert.Equal(SR.GetResourceString(SR.ID2015), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2015), response.ErrorUri);
    }

    [Fact]
    public async Task ProcessChallenge_ReturnsDefaultErrorForTokenRequestsWhenNoneIsSpecified()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.SetTokenEndpointUris("/challenge");

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SkipRequest();

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/challenge", new OpenIddictRequest
        {
            GrantType = GrantTypes.Password,
            Username = "johndoe",
            Password = "A3ddj3w"
        });

        // Assert
        Assert.Equal(Errors.InvalidGrant, response.Error);
        Assert.Equal(SR.GetResourceString(SR.ID2024), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2024), response.ErrorUri);
    }

    [Fact]
    public async Task ProcessChallenge_ReturnsDefaultErrorForUserinfoRequestsWhenNoneIsSpecified()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.SetUserinfoEndpointUris("/challenge");

            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal("SlAV32hkKG", context.Token);

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetTokenType(TokenTypeHints.AccessToken)
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.AddEventHandler<HandleUserinfoRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SkipRequest();

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/challenge", new OpenIddictRequest
        {
            AccessToken = "SlAV32hkKG"
        });

        // Assert
        Assert.Equal(Errors.InsufficientAccess, response.Error);
        Assert.Equal(SR.GetResourceString(SR.ID2025), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2025), response.ErrorUri);
    }

    [Theory]
    [InlineData("custom_error", null, null)]
    [InlineData("custom_error", "custom_description", null)]
    [InlineData("custom_error", "custom_description", "custom_uri")]
    [InlineData(null, "custom_description", null)]
    [InlineData(null, "custom_description", "custom_uri")]
    [InlineData(null, null, "custom_uri")]
    [InlineData(null, null, null)]
    public async Task ProcessChallenge_AllowsRejectingRequest(string error, string description, string uri)
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.SetTokenEndpointUris("/challenge");

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SkipRequest();

                    return default;
                }));

            options.AddEventHandler<ProcessChallengeContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Reject(error, description, uri);

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/challenge", new OpenIddictRequest
        {
            GrantType = GrantTypes.Password,
            Username = "johndoe",
            Password = "A3ddj3w"
        });

        // Assert
        Assert.Equal(error ?? Errors.InvalidRequest, response.Error);
        Assert.Equal(description, response.ErrorDescription);
        Assert.Equal(uri, response.ErrorUri);
    }

    [Fact]
    public async Task ProcessChallenge_AllowsHandlingResponse()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.SetTokenEndpointUris("/challenge");

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SkipRequest();

                    return default;
                }));

            options.AddEventHandler<ProcessChallengeContext>(builder =>
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
        var response = await client.PostAsync("/challenge", new OpenIddictRequest
        {
            GrantType = GrantTypes.Password,
            Username = "johndoe",
            Password = "A3ddj3w"
        });

        // Assert
        Assert.Equal("Bob le Bricoleur", (string?) response["name"]);
    }

    [Fact]
    public async Task ProcessSignIn_UnknownEndpointCausesAnException()
    {
        // Arrange
        await using var server = await CreateServerAsync(options => options.EnableDegradedMode());
        await using var client = await server.CreateClientAsync();

        // Act and assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(delegate
        {
            return client.PostAsync("/signin", new OpenIddictRequest());
        });

        Assert.Equal(SR.GetResourceString(SR.ID0010), exception.Message);
    }

    [Fact]
    public async Task ProcessSignIn_InvalidEndpointCausesAnException()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.SetConfigurationEndpointUris("/signin");

            options.AddEventHandler<HandleConfigurationRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SkipRequest();

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act and assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(delegate
        {
            return client.GetAsync("/signin");
        });

        Assert.Equal(SR.GetResourceString(SR.ID0010), exception.Message);
    }

    [Fact]
    public async Task ProcessSignIn_NullIdentityCausesAnException()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal();

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act and assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(delegate
        {
            return client.PostAsync("/connect/token", new OpenIddictRequest
            {
                GrantType = GrantTypes.Password,
                Username = "johndoe",
                Password = "A3ddj3w"
            });
        });

        Assert.Equal(SR.GetResourceString(SR.ID0011), exception.Message);
    }

    [Fact]
    public async Task ProcessSignIn_NullAuthenticationTypeCausesAnException()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity());

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act and assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(delegate
        {
            return client.PostAsync("/connect/token", new OpenIddictRequest
            {
                GrantType = GrantTypes.Password,
                Username = "johndoe",
                Password = "A3ddj3w"
            });
        });

        Assert.Equal(SR.GetResourceString(SR.ID0014), exception.Message);
    }

    [Fact]
    public async Task ProcessSignIn_AuthenticatedIdentityFromDeviceEndpointCausesAnException()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<HandleDeviceRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Test"));

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act and assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(delegate
        {
            return client.PostAsync("/connect/device", new OpenIddictRequest
            {
                ClientId = "Fabrikam"
            });
        });

        Assert.Equal(SR.GetResourceString(SR.ID0012), exception.Message);
    }

    [Fact]
    public async Task ProcessSignIn_MissingSubjectCausesAnException()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<HandleAuthorizationRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"));

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act and assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(delegate
        {
            return client.PostAsync("/connect/authorize", new OpenIddictRequest
            {
                ClientId = "Fabrikam",
                RedirectUri = "http://www.fabrikam.com/path",
                ResponseType = ResponseTypes.Code
            });
        });

        Assert.Equal(SR.GetResourceString(SR.ID0015), exception.Message);
    }

    [Fact]
    public async Task ProcessSignIn_NonNullSubjectFromDeviceEndpointCausesAnException()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<HandleDeviceRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity())
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act and assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(delegate
        {
            return client.PostAsync("/connect/device", new OpenIddictRequest
            {
                ClientId = "Fabrikam"
            });
        });

        Assert.Equal(SR.GetResourceString(SR.ID0013), exception.Message);
    }

    [Fact]
    public async Task ProcessSignIn_ScopeDefaultsToOpenId()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));

            options.AddEventHandler<ProcessSignInContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal(new[] { Scopes.OpenId }, context.Principal!.GetScopes());

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            GrantType = GrantTypes.Password,
            Username = "johndoe",
            Password = "A3ddj3w",
            Scope = Scopes.OpenId
        });

        // Assert
        Assert.NotNull(response.AccessToken);
    }

    [Fact]
    public async Task ProcessSignIn_ResourcesAreInferredFromAudiences()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetAudiences("http://www.fabrikam.com/")
                        .SetScopes(Scopes.OfflineAccess)
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));

            options.AddEventHandler<ProcessSignInContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal(new[] { "http://www.fabrikam.com/" }, context.Principal!.GetResources());

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            GrantType = GrantTypes.Password,
            Username = "johndoe",
            Password = "A3ddj3w"
        });

        // Assert
        Assert.NotNull(response.AccessToken);
        Assert.NotNull(response.RefreshToken);
    }

    [Fact]
    public async Task ProcessSignIn_AllowsOverridingDefaultTokensSelection()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));

            options.AddEventHandler<ProcessSignInContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    context.GenerateAccessToken = context.IncludeAccessToken = false;
                    context.GenerateAuthorizationCode = context.IncludeAuthorizationCode = true;
                    context.GenerateDeviceCode = context.IncludeDeviceCode = true;
                    context.GenerateIdentityToken = context.IncludeIdentityToken = true;
                    context.GenerateRefreshToken = context.IncludeRefreshToken = true;
                    context.GenerateUserCode = context.IncludeUserCode = true;

                    return default;
                });

                builder.SetOrder(EvaluateGeneratedTokens.Descriptor.Order + 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            GrantType = GrantTypes.Password,
            Username = "johndoe",
            Password = "A3ddj3w"
        });

        // Assert
        Assert.Null(response.AccessToken);
        Assert.NotNull(response.Code);
        Assert.NotNull(response.DeviceCode);
        Assert.NotNull(response.IdToken);
        Assert.NotNull(response.RefreshToken);
        Assert.NotNull(response.UserCode);
    }

    [Fact]
    public async Task ProcessSignIn_NoTokenIsReturnedForNoneFlowRequests()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<HandleAuthorizationRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));

            options.AddEventHandler<ProcessSignInContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.False(context.GenerateAccessToken);
                    Assert.False(context.GenerateAuthorizationCode);
                    Assert.False(context.GenerateDeviceCode);
                    Assert.False(context.GenerateIdentityToken);
                    Assert.False(context.GenerateRefreshToken);
                    Assert.False(context.GenerateUserCode);
                    Assert.False(context.IncludeAccessToken);
                    Assert.False(context.IncludeAuthorizationCode);
                    Assert.False(context.IncludeDeviceCode);
                    Assert.False(context.IncludeIdentityToken);
                    Assert.False(context.IncludeRefreshToken);
                    Assert.False(context.IncludeUserCode);

                    return default;
                });

                builder.SetOrder(EvaluateGeneratedTokens.Descriptor.Order + 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/authorize", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            Nonce = "n-0S6_WzA2Mj",
            RedirectUri = "http://www.fabrikam.com/path",
            ResponseType = ResponseTypes.None,
            Scope = Scopes.OpenId
        });

        // Assert
        Assert.Null(response.AccessToken);
        Assert.Null(response.Code);
        Assert.Null(response.DeviceCode);
        Assert.Null(response.IdToken);
        Assert.Null(response.RefreshToken);
        Assert.Null(response.UserCode);
    }

    [Theory]
    [InlineData("code id_token token")]
    [InlineData("code token")]
    [InlineData("id_token token")]
    [InlineData("token")]
    public async Task ProcessSignIn_AnAccessTokenIsReturnedForImplicitAndHybridFlowRequests(string type)
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<HandleAuthorizationRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));

            options.AddEventHandler<ProcessSignInContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.True(context.GenerateAccessToken);
                    Assert.True(context.IncludeAccessToken);

                    return default;
                });

                builder.SetOrder(EvaluateGeneratedTokens.Descriptor.Order + 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/authorize", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            Nonce = "n-0S6_WzA2Mj",
            RedirectUri = "http://www.fabrikam.com/path",
            ResponseType = type,
            Scope = Scopes.OpenId
        });

        // Assert
        Assert.NotNull(response.AccessToken);
    }

    [Fact]
    public async Task ProcessSignIn_AnAccessTokenIsReturnedForCodeGrantRequests()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal("SplxlOBeZQQYbYS6WxSbIA", context.Token);
                    Assert.Equal(new[] { TokenTypeHints.AuthorizationCode }, context.ValidTokenTypes);

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetTokenType(TokenTypeHints.AuthorizationCode)
                        .SetPresenters("Fabrikam")
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.AddEventHandler<ProcessSignInContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.True(context.GenerateAccessToken);
                    Assert.True(context.IncludeAccessToken);

                    return default;
                });

                builder.SetOrder(EvaluateGeneratedTokens.Descriptor.Order + 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            Code = "SplxlOBeZQQYbYS6WxSbIA",
            GrantType = GrantTypes.AuthorizationCode
        });

        // Assert
        Assert.NotNull(response.AccessToken);
    }

    [Fact]
    public async Task ProcessSignIn_AnAccessTokenIsReturnedForDeviceGrantRequests()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal("GmRhmhcxhwAzkoEqiMEg_DnyEysNkuNhszIySk9eS", context.Token);
                    Assert.Equal(new[] { TokenTypeHints.DeviceCode }, context.ValidTokenTypes);

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity())
                        .SetTokenType(TokenTypeHints.DeviceCode)
                        .SetPresenters("Fabrikam");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.AddEventHandler<ProcessSignInContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.True(context.GenerateAccessToken);
                    Assert.True(context.IncludeAccessToken);

                    return default;
                });

                builder.SetOrder(EvaluateGeneratedTokens.Descriptor.Order + 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            DeviceCode = "GmRhmhcxhwAzkoEqiMEg_DnyEysNkuNhszIySk9eS",
            GrantType = GrantTypes.DeviceCode
        });

        // Assert
        Assert.NotNull(response.AccessToken);
    }

    [Fact]
    public async Task ProcessSignIn_AnAccessTokenIsReturnedForRefreshTokenGrantRequests()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal("8xLOxBtZp8", context.Token);
                    Assert.Equal(new[] { TokenTypeHints.RefreshToken }, context.ValidTokenTypes);

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetTokenType(TokenTypeHints.RefreshToken)
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.AddEventHandler<ProcessSignInContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.True(context.GenerateAccessToken);
                    Assert.True(context.IncludeAccessToken);

                    return default;
                });

                builder.SetOrder(EvaluateGeneratedTokens.Descriptor.Order + 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            GrantType = GrantTypes.RefreshToken,
            RefreshToken = "8xLOxBtZp8"
        });

        // Assert
        Assert.NotNull(response.AccessToken);
    }

    [Fact]
    public async Task ProcessSignIn_AnAccessTokenIsReturnedForPasswordGrantRequests()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));

            options.AddEventHandler<ProcessSignInContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.True(context.GenerateAccessToken);
                    Assert.True(context.IncludeAccessToken);

                    return default;
                });

                builder.SetOrder(EvaluateGeneratedTokens.Descriptor.Order + 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            GrantType = GrantTypes.Password,
            Username = "johndoe",
            Password = "A3ddj3w"
        });

        // Assert
        Assert.NotNull(response.AccessToken);
    }

    [Fact]
    public async Task ProcessSignIn_AnAccessTokenIsReturnedForClientCredentialsGrantRequests()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));

            options.AddEventHandler<ProcessSignInContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.True(context.GenerateAccessToken);
                    Assert.True(context.IncludeAccessToken);

                    return default;
                });

                builder.SetOrder(EvaluateGeneratedTokens.Descriptor.Order + 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            ClientSecret = "7Fjfp0ZBr1KtDRbnfVdmIw",
            GrantType = GrantTypes.ClientCredentials,
        });

        // Assert
        Assert.NotNull(response.AccessToken);
    }

    [Fact]
    public async Task ProcessSignIn_AnAccessTokenIsReturnedForCustomGrantRequests()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.AllowCustomFlow("urn:ietf:params:oauth:grant-type:custom_grant");

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));

            options.AddEventHandler<ProcessSignInContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.True(context.GenerateAccessToken);
                    Assert.True(context.IncludeAccessToken);

                    return default;
                });

                builder.SetOrder(EvaluateGeneratedTokens.Descriptor.Order + 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            GrantType = "urn:ietf:params:oauth:grant-type:custom_grant"
        });

        // Assert
        Assert.NotNull(response.AccessToken);
    }

    [Theory]
    [InlineData("code")]
    [InlineData("code id_token")]
    [InlineData("code id_token token")]
    [InlineData("code token")]
    public async Task ProcessSignIn_AnAuthorizationCodeIsReturnedForCodeAndHybridFlowRequests(string type)
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<HandleAuthorizationRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));

            options.AddEventHandler<ProcessSignInContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.True(context.GenerateAuthorizationCode);
                    Assert.True(context.IncludeAuthorizationCode);

                    return default;
                });

                builder.SetOrder(EvaluateGeneratedTokens.Descriptor.Order + 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/authorize", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            Nonce = "n-0S6_WzA2Mj",
            RedirectUri = "http://www.fabrikam.com/path",
            ResponseType = type,
            Scope = Scopes.OpenId
        });

        // Assert
        Assert.NotNull(response.Code);
    }

    [Fact]
    public async Task ProcessSignIn_ADeviceCodeIsReturnedForDeviceRequests()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<HandleAuthorizationRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity());

                    return default;
                }));

            options.AddEventHandler<ProcessSignInContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.True(context.GenerateDeviceCode);
                    Assert.True(context.IncludeDeviceCode);

                    return default;
                });

                builder.SetOrder(EvaluateGeneratedTokens.Descriptor.Order + 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/device", new OpenIddictRequest
        {
            ClientId = "Fabrikam"
        });

        // Assert
        Assert.NotNull(response.DeviceCode);
    }

    [Fact]
    public async Task ProcessSignIn_ScopesCanBeOverridenForRefreshTokenRequests()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.RegisterScopes(Scopes.Profile);

            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal("8xLOxBtZp8", context.Token);
                    Assert.Equal(new[] { TokenTypeHints.RefreshToken }, context.ValidTokenTypes);

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetTokenType(TokenTypeHints.RefreshToken)
                        .SetScopes(Scopes.Profile, Scopes.OfflineAccess)
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.AddEventHandler<ProcessSignInContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal(new[] { Scopes.Profile }, context.AccessTokenPrincipal?.GetScopes());

                    return default;
                });

                builder.SetOrder(PrepareAccessTokenPrincipal.Descriptor.Order + 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            GrantType = GrantTypes.RefreshToken,
            RefreshToken = "8xLOxBtZp8",
            Scope = Scopes.Profile
        });

        // Assert
        Assert.NotNull(response.AccessToken);
    }

    [Fact]
    public async Task ProcessSignIn_NoRefreshTokenIsReturnedWhenOfflineAccessScopeIsNotGranted()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));

            options.AddEventHandler<ProcessSignInContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.False(context.GenerateRefreshToken);
                    Assert.False(context.IncludeRefreshToken);

                    return default;
                });

                builder.SetOrder(EvaluateGeneratedTokens.Descriptor.Order + 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            GrantType = GrantTypes.Password,
            Username = "johndoe",
            Password = "A3ddj3w"
        });

        // Assert
        Assert.Null(response.RefreshToken);
    }

    [Fact]
    public async Task ProcessSignIn_ARefreshTokenIsReturnedForCodeGrantRequests()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal("SplxlOBeZQQYbYS6WxSbIA", context.Token);
                    Assert.Equal(new[] { TokenTypeHints.AuthorizationCode }, context.ValidTokenTypes);

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetTokenType(TokenTypeHints.AuthorizationCode)
                        .SetPresenters("Fabrikam")
                        .SetScopes(Scopes.OfflineAccess)
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.AddEventHandler<ProcessSignInContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.True(context.GenerateRefreshToken);
                    Assert.True(context.IncludeRefreshToken);

                    return default;
                });

                builder.SetOrder(EvaluateGeneratedTokens.Descriptor.Order + 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            Code = "SplxlOBeZQQYbYS6WxSbIA",
            GrantType = GrantTypes.AuthorizationCode
        });

        // Assert
        Assert.NotNull(response.RefreshToken);
    }

    [Fact]
    public async Task ProcessSignIn_ARefreshTokenIsReturnedForDeviceGrantRequests()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal("GmRhmhcxhwAzkoEqiMEg_DnyEysNkuNhszIySk9eS", context.Token);
                    Assert.Equal(new[] { TokenTypeHints.DeviceCode }, context.ValidTokenTypes);

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity())
                        .SetTokenType(TokenTypeHints.DeviceCode)
                        .SetPresenters("Fabrikam");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetScopes(Scopes.OfflineAccess)
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.AddEventHandler<ProcessSignInContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.True(context.GenerateRefreshToken);
                    Assert.True(context.IncludeRefreshToken);

                    return default;
                });

                builder.SetOrder(EvaluateGeneratedTokens.Descriptor.Order + 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            DeviceCode = "GmRhmhcxhwAzkoEqiMEg_DnyEysNkuNhszIySk9eS",
            GrantType = GrantTypes.DeviceCode
        });

        // Assert
        Assert.NotNull(response.RefreshToken);
    }

    [Fact]
    public async Task ProcessSignIn_ARefreshTokenIsReturnedForRefreshTokenGrantRequests()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal("8xLOxBtZp8", context.Token);
                    Assert.Equal(new[] { TokenTypeHints.RefreshToken }, context.ValidTokenTypes);

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetTokenType(TokenTypeHints.RefreshToken)
                        .SetScopes(Scopes.OfflineAccess)
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.AddEventHandler<ProcessSignInContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.True(context.GenerateRefreshToken);
                    Assert.True(context.IncludeRefreshToken);

                    return default;
                });

                builder.SetOrder(EvaluateGeneratedTokens.Descriptor.Order + 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            GrantType = GrantTypes.RefreshToken,
            RefreshToken = "8xLOxBtZp8"
        });

        // Assert
        Assert.NotNull(response.RefreshToken);
    }

    [Fact]
    public async Task ProcessSignIn_ARefreshTokenIsReturnedForPasswordGrantRequests()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<ProcessSignInContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.True(context.GenerateRefreshToken);
                    Assert.True(context.IncludeRefreshToken);

                    return default;
                });

                builder.SetOrder(EvaluateGeneratedTokens.Descriptor.Order + 500);
            });

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetScopes(Scopes.OfflineAccess)
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            GrantType = GrantTypes.Password,
            Username = "johndoe",
            Password = "A3ddj3w"
        });

        // Assert
        Assert.NotNull(response.RefreshToken);
    }

    [Fact]
    public async Task ProcessSignIn_ARefreshTokenIsReturnedForClientCredentialsGrantRequests()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<ProcessSignInContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.True(context.GenerateRefreshToken);
                    Assert.True(context.IncludeRefreshToken);

                    return default;
                });

                builder.SetOrder(EvaluateGeneratedTokens.Descriptor.Order + 500);
            });

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetScopes(Scopes.OfflineAccess)
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            ClientSecret = "7Fjfp0ZBr1KtDRbnfVdmIw",
            GrantType = GrantTypes.ClientCredentials,
        });

        // Assert
        Assert.NotNull(response.RefreshToken);
    }

    [Fact]
    public async Task ProcessSignIn_ARefreshTokenIsReturnedForCustomGrantRequests()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.AllowCustomFlow("urn:ietf:params:oauth:grant-type:custom_grant");

            options.AddEventHandler<ProcessSignInContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.True(context.GenerateRefreshToken);
                    Assert.True(context.IncludeRefreshToken);

                    return default;
                });

                builder.SetOrder(EvaluateGeneratedTokens.Descriptor.Order + 500);
            });

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetScopes(Scopes.OfflineAccess)
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            GrantType = "urn:ietf:params:oauth:grant-type:custom_grant"
        });

        // Assert
        Assert.NotNull(response.RefreshToken);
    }

    [Fact]
    public async Task ProcessSignIn_NoIdentityTokenIsReturnedWhenOfflineAccessScopeIsNotGranted()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));

            options.AddEventHandler<ProcessSignInContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.False(context.GenerateIdentityToken);
                    Assert.False(context.IncludeIdentityToken);

                    return default;
                });

                builder.SetOrder(EvaluateGeneratedTokens.Descriptor.Order + 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            GrantType = GrantTypes.Password,
            Username = "johndoe",
            Password = "A3ddj3w"
        });

        // Assert
        Assert.Null(response.IdToken);
    }

    [Theory]
    [InlineData("code id_token")]
    [InlineData("code id_token token")]
    [InlineData("id_token")]
    [InlineData("id_token token")]
    public async Task ProcessSignIn_AnIdentityTokenIsReturnedForImplicitAndHybridFlowRequests(string type)
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<HandleAuthorizationRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));

            options.AddEventHandler<ProcessSignInContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.True(context.GenerateIdentityToken);
                    Assert.True(context.IncludeIdentityToken);

                    return default;
                });

                builder.SetOrder(EvaluateGeneratedTokens.Descriptor.Order + 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/authorize", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            Nonce = "n-0S6_WzA2Mj",
            RedirectUri = "http://www.fabrikam.com/path",
            ResponseType = type,
            Scope = Scopes.OpenId
        });

        // Assert
        Assert.NotNull(response.IdToken);
    }

    [Fact]
    public async Task ProcessSignIn_AnIdentityTokenIsReturnedForCodeGrantRequests()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal("SplxlOBeZQQYbYS6WxSbIA", context.Token);
                    Assert.Equal(new[] { TokenTypeHints.AuthorizationCode }, context.ValidTokenTypes);

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetTokenType(TokenTypeHints.AuthorizationCode)
                        .SetPresenters("Fabrikam")
                        .SetScopes(Scopes.OpenId)
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.AddEventHandler<ProcessSignInContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.True(context.GenerateIdentityToken);
                    Assert.True(context.IncludeIdentityToken);

                    return default;
                });

                builder.SetOrder(EvaluateGeneratedTokens.Descriptor.Order + 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            Code = "SplxlOBeZQQYbYS6WxSbIA",
            GrantType = GrantTypes.AuthorizationCode
        });

        // Assert
        Assert.NotNull(response.IdToken);
    }

    [Fact]
    public async Task ProcessSignIn_AnIdentityTokenIsReturnedForDeviceGrantRequests()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal("GmRhmhcxhwAzkoEqiMEg_DnyEysNkuNhszIySk9eS", context.Token);
                    Assert.Equal(new[] { TokenTypeHints.DeviceCode }, context.ValidTokenTypes);

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity())
                        .SetTokenType(TokenTypeHints.DeviceCode)
                        .SetPresenters("Fabrikam");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetScopes(Scopes.OpenId)
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.AddEventHandler<ProcessSignInContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.True(context.GenerateIdentityToken);
                    Assert.True(context.IncludeIdentityToken);

                    return default;
                });

                builder.SetOrder(EvaluateGeneratedTokens.Descriptor.Order + 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            DeviceCode = "GmRhmhcxhwAzkoEqiMEg_DnyEysNkuNhszIySk9eS",
            GrantType = GrantTypes.DeviceCode
        });

        // Assert
        Assert.NotNull(response.IdToken);
    }

    [Fact]
    public async Task ProcessSignIn_AnIdentityTokenIsReturnedForRefreshTokenGrantRequests()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal("8xLOxBtZp8", context.Token);
                    Assert.Equal(new[] { TokenTypeHints.RefreshToken }, context.ValidTokenTypes);

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetTokenType(TokenTypeHints.RefreshToken)
                        .SetScopes(Scopes.OpenId)
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.AddEventHandler<ProcessSignInContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.True(context.GenerateIdentityToken);
                    Assert.True(context.IncludeIdentityToken);

                    return default;
                });

                builder.SetOrder(EvaluateGeneratedTokens.Descriptor.Order + 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            GrantType = GrantTypes.RefreshToken,
            RefreshToken = "8xLOxBtZp8"
        });

        // Assert
        Assert.NotNull(response.IdToken);
    }

    [Fact]
    public async Task ProcessSignIn_AnIdentityTokenIsReturnedForPasswordGrantRequests()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<ProcessSignInContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.True(context.GenerateIdentityToken);
                    Assert.True(context.IncludeIdentityToken);

                    return default;
                });

                builder.SetOrder(EvaluateGeneratedTokens.Descriptor.Order + 500);
            });

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            GrantType = GrantTypes.Password,
            Username = "johndoe",
            Password = "A3ddj3w",
            Scope = Scopes.OpenId
        });

        // Assert
        Assert.NotNull(response.IdToken);
    }

    [Fact]
    public async Task ProcessSignIn_AnIdentityTokenIsReturnedForClientCredentialsGrantRequests()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));

            options.AddEventHandler<ProcessSignInContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.True(context.GenerateIdentityToken);
                    Assert.True(context.IncludeIdentityToken);

                    return default;
                });

                builder.SetOrder(EvaluateGeneratedTokens.Descriptor.Order + 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            ClientSecret = "7Fjfp0ZBr1KtDRbnfVdmIw",
            GrantType = GrantTypes.ClientCredentials,
            Scope = Scopes.OpenId
        });

        // Assert
        Assert.NotNull(response.IdToken);
    }

    [Fact]
    public async Task ProcessSignIn_AnIdentityTokenIsReturnedForCustomGrantRequests()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.AllowCustomFlow("urn:ietf:params:oauth:grant-type:custom_grant");

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));

            options.AddEventHandler<ProcessSignInContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.True(context.GenerateIdentityToken);
                    Assert.True(context.IncludeIdentityToken);

                    return default;
                });

                builder.SetOrder(EvaluateGeneratedTokens.Descriptor.Order + 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            GrantType = "urn:ietf:params:oauth:grant-type:custom_grant",
            Scope = Scopes.OpenId
        });

        // Assert
        Assert.NotNull(response.IdToken);
    }

    [Fact]
    public async Task ProcessSignIn_AUserCodeIsReturnedForDeviceRequests()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<HandleAuthorizationRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity());

                    return default;
                }));

            options.AddEventHandler<ProcessSignInContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.True(context.GenerateUserCode);
                    Assert.True(context.IncludeUserCode);

                    return default;
                });

                builder.SetOrder(EvaluateGeneratedTokens.Descriptor.Order + 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/device", new OpenIddictRequest
        {
            ClientId = "Fabrikam"
        });

        // Assert
        Assert.NotNull(response.UserCode);
    }

    [Fact]
    public async Task ProcessSignIn_PrivateClaimsAreAutomaticallyRestored()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal("8xLOxBtZp8", context.Token);
                    Assert.Equal(new[] { TokenTypeHints.RefreshToken }, context.ValidTokenTypes);

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetTokenType(TokenTypeHints.RefreshToken)
                        .SetScopes(Scopes.OpenId, Scopes.OfflineAccess)
                        .SetClaim(Claims.Subject, "Bob le Bricoleur")
                        .SetClaim(Claims.Prefixes.Private + "_private_claim", "value");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.AddEventHandler<ProcessSignInContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal(new[] { Scopes.OpenId, Scopes.OfflineAccess }, context.Principal!.GetScopes());
                    Assert.Equal("value", context.Principal!.GetClaim(Claims.Prefixes.Private + "_private_claim"));

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            GrantType = GrantTypes.RefreshToken,
            RefreshToken = "8xLOxBtZp8"
        });

        // Assert
        Assert.NotNull(response.IdToken);
        Assert.NotNull(response.RefreshToken);
    }

    [Fact]
    public async Task ProcessSignIn_AuthorizationCodeIsAutomaticallyRedeemed()
    {
        // Arrange
        var token = new OpenIddictToken();

        var manager = CreateTokenManager(mock =>
        {
            mock.Setup(manager => manager.FindByIdAsync("3E228451-1555-46F7-A471-951EFBA23A56", It.IsAny<CancellationToken>()))
                .ReturnsAsync(token);

            mock.Setup(manager => manager.GetIdAsync(token, It.IsAny<CancellationToken>()))
                .ReturnsAsync("3E228451-1555-46F7-A471-951EFBA23A56");

            mock.Setup(manager => manager.HasStatusAsync(token, Statuses.Valid, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            mock.Setup(manager => manager.TryRedeemAsync(token, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            mock.Setup(manager => manager.CreateAsync(It.IsAny<OpenIddictTokenDescriptor>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OpenIddictToken());
        });

        await using var server = await CreateServerAsync(options =>
        {
            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal("SplxlOBeZQQYbYS6WxSbIA", context.Token);
                    Assert.Equal(new[] { TokenTypeHints.AuthorizationCode }, context.ValidTokenTypes);

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetTokenType(TokenTypeHints.AuthorizationCode)
                        .SetPresenters("Fabrikam")
                        .SetTokenId("3E228451-1555-46F7-A471-951EFBA23A56")
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.Services.AddSingleton(CreateApplicationManager(mock =>
            {
                var application = new OpenIddictApplication();

                mock.Setup(manager => manager.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(application);

                mock.Setup(manager => manager.HasClientTypeAsync(application, ClientTypes.Public, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
            }));

            options.Services.AddSingleton(manager);
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            Code = "SplxlOBeZQQYbYS6WxSbIA",
            GrantType = GrantTypes.AuthorizationCode,
            RedirectUri = "http://www.fabrikam.com/path"
        });

        // Assert
        Mock.Get(manager).Verify(manager => manager.FindByIdAsync("3E228451-1555-46F7-A471-951EFBA23A56", It.IsAny<CancellationToken>()), Times.AtLeastOnce());
        Mock.Get(manager).Verify(manager => manager.TryRedeemAsync(token, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task ProcessSignIn_RefreshTokenIsAutomaticallyRedeemedWhenRollingTokensAreEnabled()
    {
        // Arrange
        var token = new OpenIddictToken();

        var manager = CreateTokenManager(mock =>
        {
            mock.Setup(manager => manager.FindByIdAsync("60FFF7EA-F98E-437B-937E-5073CC313103", It.IsAny<CancellationToken>()))
                .ReturnsAsync(token);

            mock.Setup(manager => manager.GetIdAsync(token, It.IsAny<CancellationToken>()))
                .ReturnsAsync("60FFF7EA-F98E-437B-937E-5073CC313103");

            mock.Setup(manager => manager.HasStatusAsync(token, Statuses.Redeemed, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            mock.Setup(manager => manager.HasStatusAsync(token, Statuses.Valid, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            mock.Setup(manager => manager.TryRedeemAsync(token, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            mock.Setup(manager => manager.CreateAsync(It.IsAny<OpenIddictTokenDescriptor>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OpenIddictToken());
        });

        await using var server = await CreateServerAsync(options =>
        {
            options.DisableAuthorizationStorage();

            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal("8xLOxBtZp8", context.Token);
                    Assert.Equal(new[] { TokenTypeHints.RefreshToken }, context.ValidTokenTypes);

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetTokenType(TokenTypeHints.RefreshToken)
                        .SetScopes(Scopes.OpenId, Scopes.OfflineAccess)
                        .SetTokenId("60FFF7EA-F98E-437B-937E-5073CC313103")
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.Services.AddSingleton(manager);
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            GrantType = GrantTypes.RefreshToken,
            RefreshToken = "8xLOxBtZp8"
        });

        // Assert
        Assert.NotNull(response.RefreshToken);

        Mock.Get(manager).Verify(manager => manager.FindByIdAsync("60FFF7EA-F98E-437B-937E-5073CC313103", It.IsAny<CancellationToken>()), Times.AtLeastOnce());
        Mock.Get(manager).Verify(manager => manager.TryRedeemAsync(token, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task ProcessSignIn_RefreshTokenIsNotRedeemedWhenRollingTokensAreDisabled()
    {
        // Arrange
        var token = new OpenIddictToken();

        var manager = CreateTokenManager(mock =>
        {
            mock.Setup(manager => manager.FindByIdAsync("60FFF7EA-F98E-437B-937E-5073CC313103", It.IsAny<CancellationToken>()))
                .ReturnsAsync(token);

            mock.Setup(manager => manager.HasStatusAsync(token, Statuses.Redeemed, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            mock.Setup(manager => manager.HasStatusAsync(token, Statuses.Valid, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            mock.Setup(manager => manager.CreateAsync(It.IsAny<OpenIddictTokenDescriptor>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OpenIddictToken());
        });

        await using var server = await CreateServerAsync(options =>
        {
            options.DisableAuthorizationStorage();
            options.DisableRollingRefreshTokens();

            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal("8xLOxBtZp8", context.Token);
                    Assert.Equal(new[] { TokenTypeHints.RefreshToken }, context.ValidTokenTypes);

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetTokenType(TokenTypeHints.RefreshToken)
                        .SetScopes(Scopes.OpenId, Scopes.OfflineAccess)
                        .SetTokenId("60FFF7EA-F98E-437B-937E-5073CC313103")
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.Services.AddSingleton(manager);
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            GrantType = GrantTypes.RefreshToken,
            RefreshToken = "8xLOxBtZp8"
        });

        // Assert
        Assert.NotNull(response.RefreshToken);

        Mock.Get(manager).Verify(manager => manager.FindByIdAsync("60FFF7EA-F98E-437B-937E-5073CC313103", It.IsAny<CancellationToken>()), Times.AtLeastOnce());
        Mock.Get(manager).Verify(manager => manager.TryRedeemAsync(token, It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task ProcessSignIn_AdHocAuthorizationIsAutomaticallyCreated()
    {
        // Arrange
        var token = new OpenIddictToken();

        var manager = CreateAuthorizationManager(mock =>
        {
            mock.Setup(manager => manager.FindByIdAsync("1AF06AB2-A0FC-4E3D-86AF-E04DA8C7BE70", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OpenIddictAuthorization());

            mock.Setup(manager => manager.CreateAsync(It.IsAny<OpenIddictAuthorizationDescriptor>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OpenIddictAuthorization());
        });

        await using var server = await CreateServerAsync(options =>
        {
            options.Services.AddSingleton(CreateApplicationManager(mock =>
            {
                var application = new OpenIddictApplication();

                mock.Setup(manager => manager.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(application);

                mock.Setup(manager => manager.ValidateRedirectUriAsync(application, "http://www.fabrikam.com/path", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                mock.Setup(manager => manager.HasClientTypeAsync(application, ClientTypes.Public, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                mock.Setup(manager => manager.GetIdAsync(application, It.IsAny<CancellationToken>()))
                    .ReturnsAsync("3E228451-1555-46F7-A471-951EFBA23A56");
            }));

            options.Services.AddSingleton(CreateTokenManager(mock =>
            {
                mock.Setup(manager => manager.CreateAsync(It.IsAny<OpenIddictTokenDescriptor>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                mock.Setup(manager => manager.GetIdAsync(token, It.IsAny<CancellationToken>()))
                    .ReturnsAsync("3E228451-1555-46F7-A471-951EFBA23A56");

                mock.Setup(manager => manager.FindByIdAsync("3E228451-1555-46F7-A471-951EFBA23A56", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);
            }));

            options.Services.AddSingleton(manager);

            options.AddEventHandler<HandleAuthorizationRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/authorize", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            RedirectUri = "http://www.fabrikam.com/path",
            ResponseType = ResponseTypes.Code,
        });

        // Assert
        Assert.NotNull(response.Code);

        Mock.Get(manager).Verify(manager => manager.CreateAsync(
            It.Is<OpenIddictAuthorizationDescriptor>(descriptor =>
                descriptor.ApplicationId == "3E228451-1555-46F7-A471-951EFBA23A56" &&
                descriptor.CreationDate != null &&
                descriptor.Subject == "Bob le Magnifique" &&
                descriptor.Type == AuthorizationTypes.AdHoc),
            It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task ProcessSignIn_AdHocAuthorizationIsNotCreatedWhenAuthorizationStorageIsDisabled()
    {
        // Arrange
        var token = new OpenIddictToken();

        var manager = CreateAuthorizationManager(mock =>
        {
            mock.Setup(manager => manager.FindByIdAsync("1AF06AB2-A0FC-4E3D-86AF-E04DA8C7BE70", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OpenIddictAuthorization());
        });

        await using var server = await CreateServerAsync(options =>
        {
            options.Services.AddSingleton(CreateApplicationManager(mock =>
            {
                var application = new OpenIddictApplication();

                mock.Setup(manager => manager.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(application);

                mock.Setup(manager => manager.ValidateRedirectUriAsync(application, "http://www.fabrikam.com/path", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                mock.Setup(manager => manager.HasClientTypeAsync(application, ClientTypes.Public, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                mock.Setup(manager => manager.GetIdAsync(application, It.IsAny<CancellationToken>()))
                    .ReturnsAsync("3E228451-1555-46F7-A471-951EFBA23A56");
            }));

            options.Services.AddSingleton(CreateTokenManager(mock =>
            {
                mock.Setup(manager => manager.CreateAsync(It.IsAny<OpenIddictTokenDescriptor>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                mock.Setup(manager => manager.GetIdAsync(token, It.IsAny<CancellationToken>()))
                    .ReturnsAsync("3E228451-1555-46F7-A471-951EFBA23A56");

                mock.Setup(manager => manager.FindByIdAsync("3E228451-1555-46F7-A471-951EFBA23A56", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);
            }));

            options.Services.AddSingleton(manager);

            options.DisableAuthorizationStorage();

            options.AddEventHandler<HandleAuthorizationRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/authorize", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            RedirectUri = "http://www.fabrikam.com/path",
            ResponseType = ResponseTypes.Code,
        });

        // Assert
        Assert.NotNull(response.Code);

        Mock.Get(manager).Verify(manager => manager.CreateAsync(It.IsAny<OpenIddictAuthorizationDescriptor>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task ProcessSignIn_ExpiresInIsReturnedWhenExpirationDateIsKnown()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            GrantType = GrantTypes.Password,
            Username = "johndoe",
            Password = "A3ddj3w"
        });

        // Assert
        Assert.NotNull(response.ExpiresIn);
    }

    [Fact]
    public async Task ProcessSignIn_ScopesAreReturnedWhenTheyDifferFromRequestedScopes()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.RegisterScopes(Scopes.Phone, Scopes.Profile);

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetScopes(Scopes.Profile)
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            GrantType = GrantTypes.Password,
            Username = "johndoe",
            Password = "A3ddj3w",
            Scope = "openid phone profile"
        });

        // Assert
        Assert.Equal(Scopes.Profile, response.Scope);
    }

    [Theory]
    [InlineData("custom_error", null, null)]
    [InlineData("custom_error", "custom_description", null)]
    [InlineData("custom_error", "custom_description", "custom_uri")]
    [InlineData(null, "custom_description", null)]
    [InlineData(null, "custom_description", "custom_uri")]
    [InlineData(null, null, "custom_uri")]
    [InlineData(null, null, null)]
    public async Task ProcessSignIn_AllowsRejectingRequest(string error, string description, string uri)
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<HandleAuthorizationRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));

            options.AddEventHandler<ProcessSignInContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Reject(error, description, uri);

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/authorize", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            RedirectUri = "http://www.fabrikam.com/path",
            ResponseType = ResponseTypes.Code,
            Scope = Scopes.OpenId
        });

        // Assert
        Assert.Equal(error ?? Errors.InvalidRequest, response.Error);
        Assert.Equal(description, response.ErrorDescription);
        Assert.Equal(uri, response.ErrorUri);
    }

    [Fact]
    public async Task ProcessSignIn_AllowsHandlingResponse()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));

            options.AddEventHandler<ProcessSignInContext>(builder =>
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
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            GrantType = GrantTypes.Password,
            Username = "johndoe",
            Password = "A3ddj3w"
        });

        // Assert
        Assert.Equal("Bob le Bricoleur", (string?) response["name"]);
    }

    [Fact]
    public async Task ProcessSignOut_UnknownEndpointCausesAnException()
    {
        // Arrange
        await using var server = await CreateServerAsync(options => options.EnableDegradedMode());
        await using var client = await server.CreateClientAsync();

        // Act and assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(delegate
        {
            return client.PostAsync("/signout", new OpenIddictRequest());
        });

        Assert.Equal(SR.GetResourceString(SR.ID0024), exception.Message);
    }

    [Fact]
    public async Task ProcessSignOut_InvalidEndpointCausesAnException()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.SetConfigurationEndpointUris("/signout");

            options.AddEventHandler<HandleConfigurationRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SkipRequest();

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act and assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(delegate
        {
            return client.GetAsync("/signout");
        });

        Assert.Equal(SR.GetResourceString(SR.ID0024), exception.Message);
    }

    [Theory]
    [InlineData("custom_error", null, null)]
    [InlineData("custom_error", "custom_description", null)]
    [InlineData("custom_error", "custom_description", "custom_uri")]
    [InlineData(null, "custom_description", null)]
    [InlineData(null, "custom_description", "custom_uri")]
    [InlineData(null, null, "custom_uri")]
    [InlineData(null, null, null)]
    public async Task ProcessSignOut_AllowsRejectingRequest(string error, string description, string uri)
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<HandleLogoutRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SignOut();

                    return default;
                }));

            options.AddEventHandler<ProcessSignOutContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Reject(error, description, uri);

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/logout", new OpenIddictRequest());

        // Assert
        Assert.Equal(error ?? Errors.InvalidRequest, response.Error);
        Assert.Equal(description, response.ErrorDescription);
        Assert.Equal(uri, response.ErrorUri);
    }

    [Fact]
    public async Task ProcessSignOut_AllowsHandlingResponse()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<HandleLogoutRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SignOut();

                    return default;
                }));

            options.AddEventHandler<ProcessSignOutContext>(builder =>
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
        var response = await client.PostAsync("/connect/logout", new OpenIddictRequest());

        // Assert
        Assert.Equal("Bob le Bricoleur", (string?) response["name"]);
    }

    protected virtual void ConfigureServices(IServiceCollection services)
    {
        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.SetDefaultApplicationEntity<OpenIddictApplication>()
                       .SetDefaultAuthorizationEntity<OpenIddictAuthorization>()
                       .SetDefaultScopeEntity<OpenIddictScope>()
                       .SetDefaultTokenEntity<OpenIddictToken>();

                options.Services.AddSingleton(CreateApplicationManager())
                                .AddSingleton(CreateAuthorizationManager())
                                .AddSingleton(CreateScopeManager())
                                .AddSingleton(CreateTokenManager());
            })

            .AddServer(options =>
            {
                // Enable the tested endpoints.
                options.SetAuthorizationEndpointUris("/connect/authorize")
                       .SetConfigurationEndpointUris("/.well-known/openid-configuration")
                       .SetCryptographyEndpointUris("/.well-known/jwks")
                       .SetDeviceEndpointUris("/connect/device")
                       .SetIntrospectionEndpointUris("/connect/introspect")
                       .SetLogoutEndpointUris("/connect/logout")
                       .SetRevocationEndpointUris("/connect/revoke")
                       .SetTokenEndpointUris("/connect/token")
                       .SetUserinfoEndpointUris("/connect/userinfo")
                       .SetVerificationEndpointUris("/connect/verification");

                options.AllowAuthorizationCodeFlow()
                       .AllowClientCredentialsFlow()
                       .AllowDeviceCodeFlow()
                       .AllowHybridFlow()
                       .AllowImplicitFlow()
                       .AllowNoneFlow()
                       .AllowPasswordFlow()
                       .AllowRefreshTokenFlow();

                // Accept anonymous clients by default.
                options.AcceptAnonymousClients();

                // Disable permission enforcement by default.
                options.IgnoreEndpointPermissions()
                       .IgnoreGrantTypePermissions()
                       .IgnoreResponseTypePermissions()
                       .IgnoreScopePermissions();

                options.AddSigningCertificate(
                    assembly: typeof(OpenIddictServerIntegrationTests).Assembly,
                    resource: "OpenIddict.Server.IntegrationTests.Certificate.pfx",
                    password: "Owin.Security.OpenIdConnect.Server");

                options.AddEncryptionCertificate(
                    assembly: typeof(OpenIddictServerIntegrationTests).Assembly,
                    resource: "OpenIddict.Server.IntegrationTests.Certificate.pfx",
                    password: "Owin.Security.OpenIdConnect.Server");

                options.AddEventHandler<ValidateAuthorizationRequestContext>(builder =>
                    builder.UseInlineHandler(context => default));

                options.AddEventHandler<ValidateDeviceRequestContext>(builder =>
                    builder.UseInlineHandler(context => default));

                options.AddEventHandler<ValidateIntrospectionRequestContext>(builder =>
                    builder.UseInlineHandler(context => default));

                options.AddEventHandler<ValidateLogoutRequestContext>(builder =>
                    builder.UseInlineHandler(context => default));

                options.AddEventHandler<ValidateRevocationRequestContext>(builder =>
                    builder.UseInlineHandler(context => default));

                options.AddEventHandler<ValidateTokenRequestContext>(builder =>
                    builder.UseInlineHandler(context => default));

                options.AddEventHandler<ValidateVerificationRequestContext>(builder =>
                    builder.UseInlineHandler(context => default));

                options.AddEventHandler<ValidateTokenContext>(builder =>
                    builder.UseInlineHandler(context => default));

                options.AddEventHandler<GenerateTokenContext>(builder =>
                    builder.UseInlineHandler(context => default));
            });
    }

    protected abstract ValueTask<OpenIddictServerIntegrationTestServer> CreateServerAsync(
        Action<OpenIddictServerBuilder>? configuration = null);

    protected OpenIddictApplicationManager<OpenIddictApplication> CreateApplicationManager(
        Action<Mock<OpenIddictApplicationManager<OpenIddictApplication>>>? configuration = null)
    {
        var manager = new Mock<OpenIddictApplicationManager<OpenIddictApplication>>(
            Mock.Of<IOpenIddictApplicationCache<OpenIddictApplication>>(),
            OutputHelper.ToLogger<OpenIddictApplicationManager<OpenIddictApplication>>(),
            Mock.Of<IOptionsMonitor<OpenIddictCoreOptions>>(),
            Mock.Of<IOpenIddictApplicationStoreResolver>());

        configuration?.Invoke(manager);

        return manager.Object;
    }

    protected OpenIddictAuthorizationManager<OpenIddictAuthorization> CreateAuthorizationManager(
        Action<Mock<OpenIddictAuthorizationManager<OpenIddictAuthorization>>>? configuration = null)
    {
        var manager = new Mock<OpenIddictAuthorizationManager<OpenIddictAuthorization>>(
            Mock.Of<IOpenIddictAuthorizationCache<OpenIddictAuthorization>>(),
            OutputHelper.ToLogger<OpenIddictAuthorizationManager<OpenIddictAuthorization>>(),
            Mock.Of<IOptionsMonitor<OpenIddictCoreOptions>>(),
            Mock.Of<IOpenIddictAuthorizationStoreResolver>());

        configuration?.Invoke(manager);

        return manager.Object;
    }

    protected OpenIddictScopeManager<OpenIddictScope> CreateScopeManager(
        Action<Mock<OpenIddictScopeManager<OpenIddictScope>>>? configuration = null)
    {
        var manager = new Mock<OpenIddictScopeManager<OpenIddictScope>>(
            Mock.Of<IOpenIddictScopeCache<OpenIddictScope>>(),
            OutputHelper.ToLogger<OpenIddictScopeManager<OpenIddictScope>>(),
            Mock.Of<IOptionsMonitor<OpenIddictCoreOptions>>(),
            Mock.Of<IOpenIddictScopeStoreResolver>());

        configuration?.Invoke(manager);

        return manager.Object;
    }

    protected OpenIddictTokenManager<OpenIddictToken> CreateTokenManager(
        Action<Mock<OpenIddictTokenManager<OpenIddictToken>>>? configuration = null)
    {
        var manager = new Mock<OpenIddictTokenManager<OpenIddictToken>>(
            Mock.Of<IOpenIddictTokenCache<OpenIddictToken>>(),
            OutputHelper.ToLogger<OpenIddictTokenManager<OpenIddictToken>>(),
            Mock.Of<IOptionsMonitor<OpenIddictCoreOptions>>(),
            Mock.Of<IOpenIddictTokenStoreResolver>());

        configuration?.Invoke(manager);

        return manager.Object;
    }

    public class OpenIddictApplication { }
    public class OpenIddictAuthorization { }
    public class OpenIddictScope { }
    public class OpenIddictToken { }
}
