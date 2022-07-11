﻿/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System.Collections.Immutable;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using static OpenIddict.Server.OpenIddictServerEvents;
using static OpenIddict.Server.OpenIddictServerHandlers.Protection;

namespace OpenIddict.Server.IntegrationTests;

public abstract partial class OpenIddictServerIntegrationTests
{
    [Theory]
    [InlineData(nameof(HttpMethod.Delete))]
    [InlineData(nameof(HttpMethod.Get))]
    [InlineData(nameof(HttpMethod.Head))]
    [InlineData(nameof(HttpMethod.Options))]
    [InlineData(nameof(HttpMethod.Put))]
    [InlineData(nameof(HttpMethod.Trace))]
    public async Task ExtractTokenRequest_UnexpectedMethodReturnsAnError(string method)
    {
        // Arrange
        await using var server = await CreateServerAsync(options => options.EnableDegradedMode());
        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.SendAsync(method, "/connect/token", new OpenIddictRequest());

        // Assert
        Assert.Equal(Errors.InvalidRequest, response.Error);
        Assert.Equal(SR.GetResourceString(SR.ID2084), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2084), response.ErrorUri);
    }

    [Theory]
    [InlineData("custom_error", null, null)]
    [InlineData("custom_error", "custom_description", null)]
    [InlineData("custom_error", "custom_description", "custom_uri")]
    [InlineData(null, "custom_description", null)]
    [InlineData(null, "custom_description", "custom_uri")]
    [InlineData(null, null, "custom_uri")]
    [InlineData(null, null, null)]
    public async Task ExtractTokenRequest_AllowsRejectingRequest(string error, string description, string uri)
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<ExtractTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Reject(error, description, uri);

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest());

        // Assert
        Assert.Equal(error ?? Errors.InvalidRequest, response.Error);
        Assert.Equal(description, response.ErrorDescription);
        Assert.Equal(uri, response.ErrorUri);
    }

    [Fact]
    public async Task ExtractTokenRequest_AllowsHandlingResponse()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<ExtractTokenRequestContext>(builder =>
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
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest());

        // Assert
        Assert.Equal("Bob le Bricoleur", (string?) response["name"]);
    }

    [Fact]
    public async Task ExtractTokenRequest_AllowsSkippingHandler()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<ExtractTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SkipRequest();

                    return default;
                }));
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest());

        // Assert
        Assert.Equal("Bob le Magnifique", (string?) response["name"]);
    }

    [Fact]
    public async Task ValidateTokenRequest_MissingGrantTypeCausesAnError()
    {
        // Arrange
        await using var server = await CreateServerAsync(options => options.EnableDegradedMode());
        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            GrantType = null
        });

        // Assert
        Assert.Equal(Errors.InvalidRequest, response.Error);
        Assert.Equal(SR.FormatID2029(Parameters.GrantType), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2029), response.ErrorUri);
    }

    [Fact]
    public async Task ValidateTokenRequest_MissingClientIdCausesAnErrorForCodeFlowRequests()
    {
        // Arrange
        await using var server = await CreateServerAsync(options => options.EnableDegradedMode());
        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = null,
            Code = "SplxlOBeZQQYbYS6WxSbIA",
            GrantType = GrantTypes.AuthorizationCode
        });

        // Assert
        Assert.Equal(Errors.InvalidClient, response.Error);
        Assert.Equal(SR.FormatID2029(Parameters.ClientId), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2029), response.ErrorUri);
    }

    [Fact]
    public async Task ValidateTokenRequest_MissingCodeCausesAnError()
    {
        // Arrange
        await using var server = await CreateServerAsync(options => options.EnableDegradedMode());
        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            Code = null,
            GrantType = GrantTypes.AuthorizationCode
        });

        // Assert
        Assert.Equal(Errors.InvalidRequest, response.Error);
        Assert.Equal(SR.FormatID2029(Parameters.Code), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2029), response.ErrorUri);
    }

    [Fact]
    public async Task ValidateTokenRequest_MissingRefreshTokenCausesAnError()
    {
        // Arrange
        await using var server = await CreateServerAsync(options => options.EnableDegradedMode());
        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            GrantType = GrantTypes.RefreshToken,
            RefreshToken = null
        });

        // Assert
        Assert.Equal(Errors.InvalidRequest, response.Error);
        Assert.Equal(SR.FormatID2029(Parameters.RefreshToken), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2029), response.ErrorUri);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("client_id", null)]
    [InlineData(null, "client_secret")]
    public async Task ValidateTokenRequest_MissingClientCredentialsCauseAnError(string identifier, string secret)
    {
        // Arrange
        await using var server = await CreateServerAsync(options => options.EnableDegradedMode());
        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = identifier,
            ClientSecret = secret,
            GrantType = GrantTypes.ClientCredentials
        });

        // Assert
        Assert.Equal(Errors.InvalidRequest, response.Error);
        Assert.Equal(SR.FormatID2057(Parameters.ClientId, Parameters.ClientSecret), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2057), response.ErrorUri);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("username", null)]
    [InlineData(null, "password")]
    public async Task ValidateTokenRequest_MissingUserCredentialsCauseAnError(string username, string password)
    {
        // Arrange
        await using var server = await CreateServerAsync(options => options.EnableDegradedMode());
        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            GrantType = GrantTypes.Password,
            Username = username,
            Password = password
        });

        // Assert
        Assert.Equal(Errors.InvalidRequest, response.Error);
        Assert.Equal(SR.FormatID2059(Parameters.Username, Parameters.Password), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2059), response.ErrorUri);
    }

    [Fact]
    public async Task ValidateTokenRequest_AuthorizationCodeRequestIsRejectedWhenPkceIsRequiredAndCodeVerifierIsMissing()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.RequireProofKeyForCodeExchange();
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            Code = "SplxlOBeZQQYbYS6WxSbIA",
            CodeVerifier = null,
            GrantType = GrantTypes.AuthorizationCode,
            RedirectUri = "http://www.fabrikam.com/path"
        });

        // Assert
        Assert.Equal(Errors.InvalidRequest, response.Error);
        Assert.Equal(SR.FormatID2029(Parameters.CodeVerifier), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2029), response.ErrorUri);
    }

    [Fact]
    public async Task ValidateTokenRequest_AuthorizationCodeRequestIsValidatedWhenPkceIsNotRequiredAndCodeVerifierIsMissing()
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
            ClientId = "Fabrikam",
            Code = "SplxlOBeZQQYbYS6WxSbIA",
            CodeVerifier = null,
            GrantType = GrantTypes.AuthorizationCode,
            RedirectUri = "http://www.fabrikam.com/path"
        });

        // Assert
        Assert.NotNull(response.AccessToken);
    }

    [Fact]
    public async Task ValidateTokenRequest_InvalidAuthorizationCodeCausesAnError()
    {
        // Arrange
        await using var server = await CreateServerAsync(options => options.EnableDegradedMode());
        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            Code = "SplxlOBeZQQYbYS6WxSbIA",
            GrantType = GrantTypes.AuthorizationCode
        });

        // Assert
        Assert.Equal(Errors.InvalidGrant, response.Error);
        Assert.Equal(SR.GetResourceString(SR.ID2001), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2001), response.ErrorUri);
    }

    [Fact]
    public async Task ValidateTokenRequest_InvalidRefreshTokenCausesAnError()
    {
        // Arrange
        await using var server = await CreateServerAsync(options => options.EnableDegradedMode());
        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            GrantType = GrantTypes.RefreshToken,
            RefreshToken = "8xLOxBtZp8"
        });

        // Assert
        Assert.Equal(Errors.InvalidGrant, response.Error);
        Assert.Equal(SR.GetResourceString(SR.ID2003), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2003), response.ErrorUri);
    }

    [Fact]
    public async Task ValidateTokenRequest_ExpiredAuthorizationCodeCausesAnError()
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
                        .SetExpirationDate(DateTimeOffset.UtcNow - TimeSpan.FromDays(1))
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
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
        Assert.Equal(Errors.InvalidGrant, response.Error);
        Assert.Equal(SR.GetResourceString(SR.ID2016), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2016), response.ErrorUri);
    }

    [Fact]
    public async Task ValidateTokenRequest_ExpiredRefreshTokenCausesAnError()
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
                        .SetExpirationDate(DateTimeOffset.UtcNow - TimeSpan.FromDays(1))
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
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
        Assert.Equal(Errors.InvalidGrant, response.Error);
        Assert.Equal(SR.GetResourceString(SR.ID2018), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2018), response.ErrorUri);
    }

    [Fact]
    public async Task ValidateTokenRequest_ExpiredDeviceCodeCausesAnError()
    {
        // Arrange
        var token = new OpenIddictToken();

        var manager = CreateTokenManager(mock =>
        {
            mock.Setup(manager => manager.FindByReferenceIdAsync("g43LaWCUrz2RaLILz2L1bg1bOpMSv1hGrH12IIkB9H4", It.IsAny<CancellationToken>()))
                .ReturnsAsync(token);

            mock.Setup(manager => manager.HasTypeAsync(token, TokenTypeHints.DeviceCode, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            mock.Setup(manager => manager.GetIdAsync(token, It.IsAny<CancellationToken>()))
                .ReturnsAsync("60FFF7EA-F98E-437B-937E-5073CC313103");

            mock.Setup(manager => manager.GetPayloadAsync(token, It.IsAny<CancellationToken>()))
                .ReturnsAsync("GmRhmhcxhwAzkoEqiMEg_DnyEysNkuNhszIySk9eS");

            mock.Setup(manager => manager.FindByIdAsync("60FFF7EA-F98E-437B-937E-5073CC313103", It.IsAny<CancellationToken>()))
                .ReturnsAsync(token);

            mock.Setup(manager => manager.GetExpirationDateAsync(token, It.IsAny<CancellationToken>()))
                .ReturnsAsync(DateTimeOffset.UtcNow - TimeSpan.FromDays(1));

            mock.Setup(manager => manager.GetTypeAsync(token, It.IsAny<CancellationToken>()))
                .ReturnsAsync(TokenTypeHints.DeviceCode);
        });

        await using var server = await CreateServerAsync(options =>
        {
            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal("GmRhmhcxhwAzkoEqiMEg_DnyEysNkuNhszIySk9eS", context.Token);
                    Assert.Equal(new[] { TokenTypeHints.DeviceCode }, context.ValidTokenTypes);

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
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
            GrantType = GrantTypes.DeviceCode,
            DeviceCode = "g43LaWCUrz2RaLILz2L1bg1bOpMSv1hGrH12IIkB9H4"
        });

        // Assert
        Assert.Equal(Errors.ExpiredToken, response.Error);
        Assert.Equal(SR.GetResourceString(SR.ID2017), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2017), response.ErrorUri);

        Mock.Get(manager).Verify(manager => manager.FindByIdAsync("60FFF7EA-F98E-437B-937E-5073CC313103", It.IsAny<CancellationToken>()), Times.AtLeastOnce());
        Mock.Get(manager).Verify(manager => manager.HasStatusAsync(token, Statuses.Inactive, It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task ValidateTokenRequest_AuthorizationCodeCausesAnErrorWhenPresentersAreMissing()
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
                        .SetPresenters(Enumerable.Empty<string>())
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act and assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(delegate
        {
            return client.PostAsync("/connect/token", new OpenIddictRequest
            {
                ClientId = "Fabrikam",
                Code = "SplxlOBeZQQYbYS6WxSbIA",
                GrantType = GrantTypes.AuthorizationCode
            });
        });

        Assert.Equal(SR.GetResourceString(SR.ID0043), exception.Message);
    }

    [Fact]
    public async Task ValidateTokenRequest_AuthorizationCodeCausesAnErrorWhenCallerIsNotAPresenter()
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
                        .SetPresenters("Contoso")
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
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
        Assert.Equal(Errors.InvalidGrant, response.Error);
        Assert.Equal(SR.GetResourceString(SR.ID2069), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2069), response.ErrorUri);
    }

    [Fact]
    public async Task ValidateTokenRequest_RefreshTokenCausesAnErrorWhenCallerIsNotAPresenter()
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
                        .SetPresenters("Contoso")
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            GrantType = GrantTypes.RefreshToken,
            RefreshToken = "8xLOxBtZp8"
        });

        // Assert
        Assert.Equal(Errors.InvalidGrant, response.Error);
        Assert.Equal(SR.GetResourceString(SR.ID2071), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2071), response.ErrorUri);
    }

    [Fact]
    public async Task ValidateTokenRequest_AuthorizationCodeCausesAnErrorWhenRedirectUriIsMissing()
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
                        .SetClaim(Claims.Subject, "Bob le Bricoleur")
                        .SetClaim(Claims.Private.RedirectUri, "http://www.fabrikam.com/callback");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            Code = "SplxlOBeZQQYbYS6WxSbIA",
            GrantType = GrantTypes.AuthorizationCode,
            RedirectUri = null
        });

        // Assert
        Assert.Equal(Errors.InvalidRequest, response.Error);
        Assert.Equal(SR.FormatID2029(Parameters.RedirectUri), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2029), response.ErrorUri);
    }

    [Fact]
    public async Task ValidateTokenRequest_AuthorizationCodeCausesAnErrorWhenRedirectUriIsInvalid()
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
                        .SetClaim(Claims.Subject, "Bob le Bricoleur")
                        .SetClaim(Claims.Private.RedirectUri, "http://www.fabrikam.com/callback");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            Code = "SplxlOBeZQQYbYS6WxSbIA",
            GrantType = GrantTypes.AuthorizationCode,
            RedirectUri = "http://www.contoso.com/redirect_uri"
        });

        // Assert
        Assert.Equal(Errors.InvalidGrant, response.Error);
        Assert.Equal(SR.FormatID2072(Parameters.RedirectUri), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2072), response.ErrorUri);
    }

    [Fact]
    public async Task ValidateTokenRequest_RequestCausesErrorWhenSendingCodeVerifier()
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
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            Code = "SplxlOBeZQQYbYS6WxSbIA",
            CodeVerifier = "AbCd97394879834759873497549237098273498072304987523948673248972349857982345",
            GrantType = GrantTypes.AuthorizationCode
        });

        // Assert
        Assert.Equal(Errors.InvalidRequest, response.Error);
        Assert.Equal(SR.FormatID2073(Parameters.CodeVerifier, Parameters.CodeChallenge), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2073), response.ErrorUri);
    }

    [Fact]
    public async Task ValidateTokenRequest_AuthorizationCodeCausesAnErrorWhenCodeVerifierIsMissing()
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
                        .SetClaim(Claims.Subject, "Bob le Bricoleur")
                        .SetClaim(Claims.Private.CodeChallenge, "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM")
                        .SetClaim(Claims.Private.CodeChallengeMethod, CodeChallengeMethods.Sha256);

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            Code = "SplxlOBeZQQYbYS6WxSbIA",
            CodeVerifier = null,
            GrantType = GrantTypes.AuthorizationCode
        });

        // Assert
        Assert.Equal(Errors.InvalidRequest, response.Error);
        Assert.Equal(SR.FormatID2029(Parameters.CodeVerifier), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2029), response.ErrorUri);
    }

    [Fact]
    public async Task ValidateTokenRequest_AuthorizationCodeCausesAnErrorWhenCodeChallengeMethodIsMIssing()
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
                        .SetClaim(Claims.Subject, "Bob le Bricoleur")
                        .SetClaim(Claims.Private.CodeChallenge, "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act and assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(delegate
        {
            return client.PostAsync("/connect/token", new OpenIddictRequest
            {
                ClientId = "Fabrikam",
                Code = "SplxlOBeZQQYbYS6WxSbIA",
                CodeVerifier = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",
                GrantType = GrantTypes.AuthorizationCode
            });
        });

        Assert.Equal(SR.GetResourceString(SR.ID0268), exception.Message);
    }

    [Fact]
    public async Task ValidateTokenRequest_AuthorizationCodeCausesAnErrorWhenCodeChallengeMethodIsInvalid()
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
                        .SetClaim(Claims.Subject, "Bob le Bricoleur")
                        .SetClaim(Claims.Private.CodeChallenge, "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM")
                        .SetClaim(Claims.Private.CodeChallengeMethod, "custom_code_challenge_method");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act and assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(delegate
        {
            return client.PostAsync("/connect/token", new OpenIddictRequest
            {
                ClientId = "Fabrikam",
                Code = "SplxlOBeZQQYbYS6WxSbIA",
                CodeVerifier = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",
                GrantType = GrantTypes.AuthorizationCode
            });
        });

        Assert.Equal(SR.GetResourceString(SR.ID0045), exception.Message);
    }

    [Theory]
    [InlineData(CodeChallengeMethods.Plain, "challenge", "invalid_verifier")]
    [InlineData(CodeChallengeMethods.Sha256, "challenge", "invalid_verifier")]
    public async Task ValidateTokenRequest_AuthorizationCodeCausesAnErrorWhenCodeVerifierIsInvalid(string method, string challenge, string verifier)
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
                        .SetClaim(Claims.Subject, "Bob le Bricoleur")
                        .SetClaim(Claims.Private.CodeChallenge, challenge)
                        .SetClaim(Claims.Private.CodeChallengeMethod, method);

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            Code = "SplxlOBeZQQYbYS6WxSbIA",
            CodeVerifier = verifier,
            GrantType = GrantTypes.AuthorizationCode
        });

        // Assert
        Assert.Equal(Errors.InvalidGrant, response.Error);
        Assert.Equal(SR.FormatID2052(Parameters.CodeVerifier), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2052), response.ErrorUri);
    }

    [Theory]
    [InlineData(CodeChallengeMethods.Plain, "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM")]
    [InlineData(CodeChallengeMethods.Sha256, "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk")]
    public async Task ValidateTokenRequest_TokenRequestSucceedsWhenCodeVerifierIsValid(string method, string challenge, string verifier)
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
                        .SetClaim(Claims.Subject, "Bob le Bricoleur")
                        .SetClaim(Claims.Private.CodeChallenge, challenge)
                        .SetClaim(Claims.Private.CodeChallengeMethod, method);

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
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
            ClientId = "Fabrikam",
            Code = "SplxlOBeZQQYbYS6WxSbIA",
            CodeVerifier = verifier,
            GrantType = GrantTypes.AuthorizationCode
        });

        // Assert
        Assert.NotNull(response.AccessToken);
    }

    [Fact]
    public async Task ValidateTokenRequest_AuthorizationCodeCausesAnErrorWhenScopeIsUnexpected()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.RegisterScopes(Scopes.Phone, Scopes.Profile);

            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal("SplxlOBeZQQYbYS6WxSbIA", context.Token);
                    Assert.Equal(new[] { TokenTypeHints.AuthorizationCode }, context.ValidTokenTypes);

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetTokenType(TokenTypeHints.AuthorizationCode)
                        .SetPresenters("Fabrikam")
                        .SetScopes(Enumerable.Empty<string>())
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            Code = "SplxlOBeZQQYbYS6WxSbIA",
            GrantType = GrantTypes.AuthorizationCode,
            Scope = "profile phone"
        });

        // Assert
        Assert.Equal(Errors.InvalidGrant, response.Error);
        Assert.Equal(SR.FormatID2074(Parameters.Scope), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2074), response.ErrorUri);
    }

    [Fact]
    public async Task ValidateTokenRequest_AuthorizationCodeCausesAnErrorWhenScopeIsInvalid()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.RegisterScopes(Scopes.Phone, Scopes.Profile);

            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal("SplxlOBeZQQYbYS6WxSbIA", context.Token);
                    Assert.Equal(new[] { TokenTypeHints.AuthorizationCode }, context.ValidTokenTypes);

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetTokenType(TokenTypeHints.AuthorizationCode)
                        .SetPresenters("Fabrikam")
                        .SetScopes("profile", "email")
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            Code = "SplxlOBeZQQYbYS6WxSbIA",
            GrantType = GrantTypes.AuthorizationCode,
            Scope = "profile phone"
        });

        // Assert
        Assert.Equal(Errors.InvalidGrant, response.Error);
        Assert.Equal(SR.FormatID2052(Parameters.Scope), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2052), response.ErrorUri);
    }

    [Fact]
    public async Task ValidateTokenRequest_RefreshTokenCausesAnErrorWhenScopeIsUnexpected()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.RegisterScopes(Scopes.Phone, Scopes.Profile);

            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal("8xLOxBtZp8", context.Token);
                    Assert.Equal(new[] { TokenTypeHints.RefreshToken }, context.ValidTokenTypes);

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetTokenType(TokenTypeHints.RefreshToken)
                        .SetScopes(Enumerable.Empty<string>())
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            GrantType = GrantTypes.RefreshToken,
            RefreshToken = "8xLOxBtZp8",
            Scope = "profile phone"
        });

        // Assert
        Assert.Equal(Errors.InvalidGrant, response.Error);
        Assert.Equal(SR.FormatID2074(Parameters.Scope), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2074), response.ErrorUri);
    }

    [Fact]
    public async Task ValidateTokenRequest_RefreshTokenCausesAnErrorWhenScopeIsInvalid()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.RegisterScopes(Scopes.Phone, Scopes.Profile);

            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal("8xLOxBtZp8", context.Token);
                    Assert.Equal(new[] { TokenTypeHints.RefreshToken }, context.ValidTokenTypes);

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetTokenType(TokenTypeHints.RefreshToken)
                        .SetScopes("profile", "email")
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            GrantType = GrantTypes.RefreshToken,
            RefreshToken = "8xLOxBtZp8",
            Scope = "profile phone"
        });

        // Assert
        Assert.Equal(Errors.InvalidGrant, response.Error);
        Assert.Equal(SR.FormatID2052(Parameters.Scope), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2052), response.ErrorUri);
    }

    [Theory]
    [InlineData(GrantTypes.AuthorizationCode)]
    [InlineData(GrantTypes.ClientCredentials)]
    [InlineData(GrantTypes.Password)]
    [InlineData(GrantTypes.RefreshToken)]
    public async Task ValidateTokenRequest_RequestIsRejectedWhenFlowIsNotEnabled(string flow)
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.Configure(options => options.GrantTypes.Remove(flow));
            options.Configure(options => options.ResponseTypes.Clear());
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            Code = "SplxlOBeZQQYbYS6WxSbIA",
            GrantType = flow,
            Username = "johndoe",
            Password = "A3ddj3w",
            RefreshToken = "8xLOxBtZp8"
        });

        // Assert
        Assert.Equal(Errors.UnsupportedGrantType, response.Error);
        Assert.Equal(SR.FormatID2032(Parameters.GrantType), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2032), response.ErrorUri);
    }

    [Fact]
    public async Task ValidateTokenRequest_RequestWithOfflineAccessScopeIsRejectedWhenRefreshTokenFlowIsDisabled()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.Configure(options => options.GrantTypes.Remove(GrantTypes.RefreshToken));
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            GrantType = GrantTypes.Password,
            Username = "johndoe",
            Password = "A3ddj3w",
            Scope = Scopes.OfflineAccess
        });

        // Assert
        Assert.Equal(Errors.InvalidRequest, response.Error);
        Assert.Equal(SR.FormatID2035(Scopes.OfflineAccess), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2035), response.ErrorUri);
    }

    [Fact]
    public async Task ValidateTokenRequest_RequestIsRejectedWhenUnregisteredScopeIsSpecified()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.Services.AddSingleton(CreateScopeManager(mock =>
            {
                mock.Setup(manager => manager.FindByNamesAsync(
                    It.Is<ImmutableArray<string>>(scopes => scopes.Length == 1 && scopes[0] == "unregistered_scope"),
                    It.IsAny<CancellationToken>()))
                    .Returns(AsyncEnumerable.Empty<OpenIddictScope>());
            }));
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            GrantType = GrantTypes.Password,
            Username = "johndoe",
            Password = "A3ddj3w",
            Scope = "unregistered_scope"
        });

        // Assert
        Assert.Equal(Errors.InvalidScope, response.Error);
        Assert.Equal(SR.FormatID2052(Parameters.Scope), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2052), response.ErrorUri);
    }

    [Fact]
    public async Task ValidateTokenRequest_RequestIsValidatedWhenScopeRegisteredInOptionsIsSpecified()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.RegisterScopes("registered_scope");

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
            Scope = "registered_scope"
        });

        // Assert
        Assert.Null(response.Error);
        Assert.Null(response.ErrorDescription);
        Assert.Null(response.ErrorUri);
        Assert.NotNull(response.AccessToken);
    }

    [Fact]
    public async Task ValidateTokenRequest_RequestIsValidatedWhenRegisteredScopeIsSpecified()
    {
        // Arrange
        var scope = new OpenIddictScope();

        var manager = CreateScopeManager(mock =>
        {
            mock.Setup(manager => manager.FindByNamesAsync(
                It.Is<ImmutableArray<string>>(scopes => scopes.Length == 1 && scopes[0] == "scope_registered_in_database"),
                It.IsAny<CancellationToken>()))
                .Returns(new[] { scope }.ToAsyncEnumerable());

            mock.Setup(manager => manager.GetNameAsync(scope, It.IsAny<CancellationToken>()))
                .ReturnsAsync("scope_registered_in_database");
        });

        await using var server = await CreateServerAsync(options =>
        {
            options.RegisterScopes("scope_registered_in_options");
            options.SetRevocationEndpointUris(Array.Empty<Uri>());
            options.DisableTokenStorage();
            options.DisableSlidingRefreshTokenExpiration();

            options.Services.AddSingleton(manager);

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
            Scope = "scope_registered_in_database scope_registered_in_options"
        });

        // Assert
        Assert.Null(response.Error);
        Assert.Null(response.ErrorDescription);
        Assert.Null(response.ErrorUri);
        Assert.NotNull(response.AccessToken);
    }

    [Theory]
    [InlineData("client_id", "")]
    [InlineData("", "client_secret")]
    public async Task ValidateTokenRequest_ClientCredentialsRequestIsRejectedWhenCredentialsAreMissing(string identifier, string secret)
    {
        // Arrange
        await using var server = await CreateServerAsync(options => options.EnableDegradedMode());
        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = identifier,
            ClientSecret = secret,
            GrantType = GrantTypes.ClientCredentials
        });

        // Assert
        Assert.Equal(Errors.InvalidRequest, response.Error);
        Assert.Equal(SR.FormatID2057(Parameters.ClientId, Parameters.ClientSecret), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2057), response.ErrorUri);
    }

    [Fact]
    public async Task ValidateTokenRequest_RequestWithoutClientIdIsRejectedWhenClientIdentificationIsRequired()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();
            options.Configure(options => options.AcceptAnonymousClients = false);
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = null,
            GrantType = GrantTypes.Password,
            Username = "johndoe",
            Password = "A3ddj3w"
        });

        // Assert
        Assert.Equal(Errors.InvalidClient, response.Error);
        Assert.Equal(SR.FormatID2029(Parameters.ClientId), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2029), response.ErrorUri);
    }

    [Fact]
    public async Task ValidateTokenRequest_RequestIsRejectedWhenClientCannotBeFound()
    {
        // Arrange
        var manager = CreateApplicationManager(mock =>
        {
            mock.Setup(manager => manager.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                .ReturnsAsync(value: null);
        });

        await using var server = await CreateServerAsync(options =>
        {
            options.Services.AddSingleton(manager);
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            GrantType = GrantTypes.Password,
            Username = "johndoe",
            Password = "A3ddj3w"
        });

        // Assert
        Assert.Equal(Errors.InvalidClient, response.Error);
        Assert.Equal(SR.FormatID2052(Parameters.ClientId), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2052), response.ErrorUri);

        Mock.Get(manager).Verify(manager => manager.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()), Times.AtLeastOnce());
    }

    [Fact]
    public async Task ValidateTokenRequest_ClientCredentialsRequestFromPublicClientIsRejected()
    {
        // Arrange
        var application = new OpenIddictApplication();

        var manager = CreateApplicationManager(mock =>
        {
            mock.Setup(manager => manager.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                .ReturnsAsync(application);

            mock.Setup(manager => manager.HasClientTypeAsync(application, ClientTypes.Public, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        });

        await using var server = await CreateServerAsync(options =>
        {
            options.Services.AddSingleton(manager);
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            ClientSecret = "7Fjfp0ZBr1KtDRbnfVdmIw",
            GrantType = GrantTypes.ClientCredentials
        });

        // Assert
        Assert.Equal(Errors.UnauthorizedClient, response.Error);
        Assert.Equal(SR.FormatID2043(Parameters.GrantType), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2043), response.ErrorUri);

        Mock.Get(manager).Verify(manager => manager.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()), Times.AtLeastOnce());
        Mock.Get(manager).Verify(manager => manager.HasClientTypeAsync(application, ClientTypes.Public, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task ValidateTokenRequest_ClientSecretCannotBeUsedByPublicClients()
    {
        // Arrange
        var application = new OpenIddictApplication();

        var manager = CreateApplicationManager(mock =>
        {
            mock.Setup(manager => manager.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                .ReturnsAsync(application);

            mock.Setup(manager => manager.HasClientTypeAsync(application, ClientTypes.Public, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        });

        await using var server = await CreateServerAsync(options =>
        {
            options.Services.AddSingleton(manager);
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            ClientSecret = "7Fjfp0ZBr1KtDRbnfVdmIw",
            GrantType = GrantTypes.Password,
            Username = "johndoe",
            Password = "A3ddj3w"
        });

        // Assert
        Assert.Equal(Errors.InvalidClient, response.Error);
        Assert.Equal(SR.FormatID2053(Parameters.ClientSecret), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2053), response.ErrorUri);

        Mock.Get(manager).Verify(manager => manager.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()), Times.AtLeastOnce());
        Mock.Get(manager).Verify(manager => manager.HasClientTypeAsync(application, ClientTypes.Public, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task ValidateTokenRequest_ClientSecretIsRequiredForNonPublicClients()
    {
        // Arrange
        var application = new OpenIddictApplication();

        var manager = CreateApplicationManager(mock =>
        {
            mock.Setup(manager => manager.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                .ReturnsAsync(application);

            mock.Setup(manager => manager.HasClientTypeAsync(application, ClientTypes.Public, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        });

        await using var server = await CreateServerAsync(options =>
        {
            options.Services.AddSingleton(manager);
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            ClientSecret = null,
            GrantType = GrantTypes.Password,
            Username = "johndoe",
            Password = "A3ddj3w"
        });

        // Assert
        Assert.Equal(Errors.InvalidClient, response.Error);
        Assert.Equal(SR.FormatID2054(Parameters.ClientSecret), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2054), response.ErrorUri);

        Mock.Get(manager).Verify(manager => manager.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()), Times.AtLeastOnce());
        Mock.Get(manager).Verify(manager => manager.HasClientTypeAsync(application, ClientTypes.Public, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task ValidateTokenRequest_RequestIsRejectedWhenClientCredentialsAreInvalid()
    {
        // Arrange
        var application = new OpenIddictApplication();

        var manager = CreateApplicationManager(mock =>
        {
            mock.Setup(manager => manager.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                .ReturnsAsync(application);

            mock.Setup(manager => manager.HasClientTypeAsync(application, ClientTypes.Public, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            mock.Setup(manager => manager.ValidateClientSecretAsync(application, "7Fjfp0ZBr1KtDRbnfVdmIw", It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        });

        await using var server = await CreateServerAsync(options =>
        {
            options.Services.AddSingleton(manager);
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            ClientSecret = "7Fjfp0ZBr1KtDRbnfVdmIw",
            GrantType = GrantTypes.Password,
            Username = "johndoe",
            Password = "A3ddj3w"
        });

        // Assert
        Assert.Equal(Errors.InvalidClient, response.Error);
        Assert.Equal(SR.GetResourceString(SR.ID2055), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2055), response.ErrorUri);

        Mock.Get(manager).Verify(manager => manager.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()), Times.AtLeastOnce());
        Mock.Get(manager).Verify(manager => manager.HasClientTypeAsync(application, ClientTypes.Public, It.IsAny<CancellationToken>()), Times.AtLeastOnce());
        Mock.Get(manager).Verify(manager => manager.ValidateClientSecretAsync(application, "7Fjfp0ZBr1KtDRbnfVdmIw", It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task ValidateTokenRequest_RequestIsRejectedWhenEndpointPermissionIsNotGranted()
    {
        // Arrange
        var application = new OpenIddictApplication();

        var manager = CreateApplicationManager(mock =>
        {
            mock.Setup(manager => manager.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                .ReturnsAsync(application);

            mock.Setup(manager => manager.HasClientTypeAsync(application, ClientTypes.Public, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            mock.Setup(manager => manager.HasPermissionAsync(application,
                Permissions.Endpoints.Token, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        });

        await using var server = await CreateServerAsync(options =>
        {
            options.Services.AddSingleton(manager);

            options.Configure(options => options.IgnoreEndpointPermissions = false);
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            GrantType = GrantTypes.Password,
            Username = "johndoe",
            Password = "A3ddj3w"
        });

        // Assert
        Assert.Equal(Errors.UnauthorizedClient, response.Error);
        Assert.Equal(SR.GetResourceString(SR.ID2063), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2063), response.ErrorUri);

        Mock.Get(manager).Verify(manager => manager.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()), Times.AtLeastOnce());
        Mock.Get(manager).Verify(manager => manager.HasPermissionAsync(application,
            Permissions.Endpoints.Token, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task ValidateTokenRequest_RequestIsRejectedWhenGrantTypePermissionIsNotGranted()
    {
        // Arrange
        var application = new OpenIddictApplication();

        var manager = CreateApplicationManager(mock =>
        {
            mock.Setup(manager => manager.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                .ReturnsAsync(application);

            mock.Setup(manager => manager.HasClientTypeAsync(application, ClientTypes.Public, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            mock.Setup(manager => manager.HasPermissionAsync(application,
                Permissions.GrantTypes.Password, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        });

        await using var server = await CreateServerAsync(options =>
        {
            options.Services.AddSingleton(manager);

            options.Configure(options => options.IgnoreGrantTypePermissions = false);
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            GrantType = GrantTypes.Password,
            Username = "johndoe",
            Password = "A3ddj3w"
        });

        // Assert
        Assert.Equal(Errors.UnauthorizedClient, response.Error);
        Assert.Equal(SR.GetResourceString(SR.ID2064), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2064), response.ErrorUri);

        Mock.Get(manager).Verify(manager => manager.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()), Times.AtLeastOnce());
        Mock.Get(manager).Verify(manager => manager.HasPermissionAsync(application,
            Permissions.GrantTypes.Password, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task ValidateTokenRequest_RequestWithOfflineAccessScopeIsRejectedWhenRefreshTokenPermissionIsNotGranted()
    {
        // Arrange
        var application = new OpenIddictApplication();

        var manager = CreateApplicationManager(mock =>
        {
            mock.Setup(manager => manager.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                .ReturnsAsync(application);

            mock.Setup(manager => manager.HasClientTypeAsync(application, ClientTypes.Public, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            mock.Setup(manager => manager.HasPermissionAsync(application,
                Permissions.GrantTypes.Password, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            mock.Setup(manager => manager.HasPermissionAsync(application,
                Permissions.GrantTypes.RefreshToken, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        });

        await using var server = await CreateServerAsync(options =>
        {
            options.Services.AddSingleton(manager);

            options.Configure(options => options.IgnoreGrantTypePermissions = false);
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            GrantType = GrantTypes.Password,
            Username = "johndoe",
            Password = "A3ddj3w",
            Scope = Scopes.OfflineAccess
        });

        // Assert
        Assert.Equal(Errors.InvalidRequest, response.Error);
        Assert.Equal(SR.FormatID2065(Scopes.OfflineAccess), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2065), response.ErrorUri);

        Mock.Get(manager).Verify(manager => manager.HasPermissionAsync(application,
            Permissions.GrantTypes.RefreshToken, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task ValidateTokenRequest_RequestIsRejectedWhenScopePermissionIsNotGranted()
    {
        // Arrange
        var application = new OpenIddictApplication();

        var manager = CreateApplicationManager(mock =>
        {
            mock.Setup(manager => manager.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                .ReturnsAsync(application);

            mock.Setup(manager => manager.HasClientTypeAsync(application, ClientTypes.Public, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            mock.Setup(manager => manager.HasPermissionAsync(application,
                Permissions.Prefixes.Scope + Scopes.Profile, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            mock.Setup(manager => manager.HasPermissionAsync(application,
                Permissions.Prefixes.Scope + Scopes.Email, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        });

        await using var server = await CreateServerAsync(options =>
        {
            options.Services.AddSingleton(manager);

            options.RegisterScopes(Scopes.Email, Scopes.Profile);
            options.Configure(options => options.IgnoreScopePermissions = false);
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            GrantType = GrantTypes.Password,
            Username = "johndoe",
            Password = "A3ddj3w",
            Scope = "openid offline_access profile email"
        });

        // Assert
        Assert.Equal(Errors.InvalidRequest, response.Error);
        Assert.Equal(SR.GetResourceString(SR.ID2051), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2051), response.ErrorUri);

        Mock.Get(manager).Verify(manager => manager.HasPermissionAsync(application,
            Permissions.Prefixes.Scope + Scopes.OpenId, It.IsAny<CancellationToken>()), Times.Never());
        Mock.Get(manager).Verify(manager => manager.HasPermissionAsync(application,
            Permissions.Prefixes.Scope + Scopes.OfflineAccess, It.IsAny<CancellationToken>()), Times.Never());
        Mock.Get(manager).Verify(manager => manager.HasPermissionAsync(application,
            Permissions.Prefixes.Scope + Scopes.Profile, It.IsAny<CancellationToken>()), Times.Once());
        Mock.Get(manager).Verify(manager => manager.HasPermissionAsync(application,
            Permissions.Prefixes.Scope + Scopes.Email, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task ValidateTokenRequest_RequestIsRejectedWhenCodeVerifierIsMissingWithPkceFeatureEnforced()
    {
        // Arrange
        var application = new OpenIddictApplication();

        var manager = CreateApplicationManager(mock =>
        {
            mock.Setup(manager => manager.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                .ReturnsAsync(application);

            mock.Setup(manager => manager.HasClientTypeAsync(application, ClientTypes.Public, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            mock.Setup(manager => manager.HasRequirementAsync(application,
                Requirements.Features.ProofKeyForCodeExchange, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        });

        await using var server = await CreateServerAsync(options =>
        {
            options.Services.AddSingleton(manager);
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            Code = "SplxlOBeZQQYbYS6WxSbIA",
            CodeVerifier = null,
            GrantType = GrantTypes.AuthorizationCode,
            RedirectUri = "http://www.fabrikam.com/path"
        });

        // Assert
        Assert.Equal(Errors.InvalidRequest, response.Error);
        Assert.Equal(SR.FormatID2054(Parameters.CodeVerifier), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2054), response.ErrorUri);

        Mock.Get(manager).Verify(manager => manager.HasRequirementAsync(application,
            Requirements.Features.ProofKeyForCodeExchange, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task ValidateTokenRequest_RequestIsValidatedWhenCodeVerifierIsMissingWithPkceFeatureNotEnforced()
    {
        // Arrange
        var application = new OpenIddictApplication();

        var manager = CreateApplicationManager(mock =>
        {
            mock.Setup(manager => manager.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                .ReturnsAsync(application);

            mock.Setup(manager => manager.HasClientTypeAsync(application, ClientTypes.Public, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            mock.Setup(manager => manager.HasRequirementAsync(application,
                Requirements.Features.ProofKeyForCodeExchange, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
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

            options.Services.AddSingleton(manager);

            options.SetRevocationEndpointUris(Array.Empty<Uri>());
            options.DisableTokenStorage();
            options.DisableSlidingRefreshTokenExpiration();
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            Code = "SplxlOBeZQQYbYS6WxSbIA",
            CodeVerifier = null,
            GrantType = GrantTypes.AuthorizationCode,
            RedirectUri = "http://www.fabrikam.com/path"
        });

        // Assert
        Assert.NotNull(response.AccessToken);

        Mock.Get(manager).Verify(manager => manager.HasRequirementAsync(application,
            Requirements.Features.ProofKeyForCodeExchange, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task ValidateTokenRequest_RequestIsValidatedWhenCodeVerifierIsPresentWithPkceFeatureEnforced()
    {
        // Arrange
        var application = new OpenIddictApplication();

        var manager = CreateApplicationManager(mock =>
        {
            mock.Setup(manager => manager.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                .ReturnsAsync(application);

            mock.Setup(manager => manager.HasClientTypeAsync(application, ClientTypes.Public, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            mock.Setup(manager => manager.HasRequirementAsync(application,
                Requirements.Features.ProofKeyForCodeExchange, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
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
                        .SetClaim(Claims.Subject, "Bob le Bricoleur")
                        .SetClaim(Claims.Private.CodeChallenge, "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM")
                        .SetClaim(Claims.Private.CodeChallengeMethod, CodeChallengeMethods.Sha256);

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.Services.AddSingleton(manager);

            options.SetRevocationEndpointUris(Array.Empty<Uri>());
            options.DisableTokenStorage();
            options.DisableSlidingRefreshTokenExpiration();
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            Code = "SplxlOBeZQQYbYS6WxSbIA",
            CodeVerifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk",
            GrantType = GrantTypes.AuthorizationCode,
            RedirectUri = "http://www.fabrikam.com/path"
        });

        // Assert
        Assert.NotNull(response.AccessToken);

        Mock.Get(manager).Verify(manager => manager.HasRequirementAsync(application,
            Requirements.Features.ProofKeyForCodeExchange, It.IsAny<CancellationToken>()), Times.Never());
    }

    [Theory]
    [InlineData("custom_error", null, null)]
    [InlineData("custom_error", "custom_description", null)]
    [InlineData("custom_error", "custom_description", "custom_uri")]
    [InlineData(null, "custom_description", null)]
    [InlineData(null, "custom_description", "custom_uri")]
    [InlineData(null, null, "custom_uri")]
    [InlineData(null, null, null)]
    public async Task ValidateTokenRequest_AllowsRejectingRequest(string error, string description, string uri)
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<ValidateTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Reject(error, description, uri);

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
        Assert.Equal(error ?? Errors.InvalidRequest, response.Error);
        Assert.Equal(description, response.ErrorDescription);
        Assert.Equal(uri, response.ErrorUri);
    }

    [Fact]
    public async Task ValidateTokenRequest_AllowsHandlingResponse()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<ValidateTokenRequestContext>(builder =>
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
    public async Task ValidateTokenRequest_AllowsSkippingHandler()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<ExtractTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SkipRequest();

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
        Assert.Equal("Bob le Magnifique", (string?) response["name"]);
    }

    [Fact]
    public async Task HandleTokenRequest_AuthorizationCodeRevocationIsIgnoredWhenTokenStorageIsDisabled()
    {
        // Arrange
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

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));

            options.Services.AddSingleton(CreateApplicationManager(mock =>
            {
                var application = new OpenIddictApplication();

                mock.Setup(manager => manager.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(application);

                mock.Setup(manager => manager.HasClientTypeAsync(application, ClientTypes.Public, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
            }));

            options.SetRevocationEndpointUris(Array.Empty<Uri>());
            options.DisableTokenStorage();
            options.DisableSlidingRefreshTokenExpiration();
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
        Assert.NotNull(response.AccessToken);
    }

    [Fact]
    public async Task HandleTokenRequest_RefreshTokenRevocationIsIgnoredWhenTokenStorageIsDisabled()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal("8xLOxBtZp8", context.Token);
                    Assert.Equal(new[] { TokenTypeHints.RefreshToken }, context.ValidTokenTypes);

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetTokenType(TokenTypeHints.RefreshToken)
                        .SetTokenId("60FFF7EA-F98E-437B-937E-5073CC313103")
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));

            options.SetRevocationEndpointUris(Array.Empty<Uri>());
            options.DisableTokenStorage();
            options.DisableSlidingRefreshTokenExpiration();
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
    public async Task HandleTokenRequest_RequestIsRejectedWhenAuthorizationCodeIsUnknown()
    {
        // Arrange
        var manager = CreateTokenManager(mock =>
        {
            mock.Setup(manager => manager.FindByIdAsync("3E228451-1555-46F7-A471-951EFBA23A56", It.IsAny<CancellationToken>()))
                .ReturnsAsync(value: null);
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
                        .SetTokenId("3E228451-1555-46F7-A471-951EFBA23A56")
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));

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
        Assert.Equal(Errors.InvalidGrant, response.Error);
        Assert.Equal(SR.GetResourceString(SR.ID2001), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2001), response.ErrorUri);

        Mock.Get(manager).Verify(manager => manager.FindByIdAsync("3E228451-1555-46F7-A471-951EFBA23A56", It.IsAny<CancellationToken>()), Times.AtLeastOnce());
    }

    [Fact]
    public async Task HandleTokenRequest_RequestIsRejectedWhenRefreshTokenIsUnknown()
    {
        // Arrange
        var manager = CreateTokenManager(mock =>
        {
            mock.Setup(manager => manager.FindByIdAsync("60FFF7EA-F98E-437B-937E-5073CC313103", It.IsAny<CancellationToken>()))
                .ReturnsAsync(value: null);
        });

        await using var server = await CreateServerAsync(options =>
        {
            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal("8xLOxBtZp8", context.Token);
                    Assert.Equal(new[] { TokenTypeHints.RefreshToken }, context.ValidTokenTypes);

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetTokenType(TokenTypeHints.RefreshToken)
                        .SetPresenters("Fabrikam")
                        .SetTokenId("60FFF7EA-F98E-437B-937E-5073CC313103")
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));

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
        Assert.Equal(Errors.InvalidGrant, response.Error);
        Assert.Equal(SR.GetResourceString(SR.ID2003), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2003), response.ErrorUri);

        Mock.Get(manager).Verify(manager => manager.FindByIdAsync("60FFF7EA-F98E-437B-937E-5073CC313103", It.IsAny<CancellationToken>()), Times.AtLeastOnce());
    }

    [Fact]
    public async Task HandleTokenRequest_RequestIsRejectedWhenAuthorizationCodeIsAlreadyRedeemed()
    {
        // Arrange
        var token = new OpenIddictToken();

        var manager = CreateTokenManager(mock =>
        {
            mock.Setup(manager => manager.FindByIdAsync("3E228451-1555-46F7-A471-951EFBA23A56", It.IsAny<CancellationToken>()))
                .ReturnsAsync(token);

            mock.Setup(manager => manager.GetIdAsync(token, It.IsAny<CancellationToken>()))
                .ReturnsAsync("3E228451-1555-46F7-A471-951EFBA23A56");

            mock.Setup(manager => manager.HasStatusAsync(token, Statuses.Redeemed, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
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

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));

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
        Assert.Equal(Errors.InvalidGrant, response.Error);
        Assert.Equal(SR.GetResourceString(SR.ID2010), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2010), response.ErrorUri);

        Mock.Get(manager).Verify(manager => manager.FindByIdAsync("3E228451-1555-46F7-A471-951EFBA23A56", It.IsAny<CancellationToken>()), Times.AtLeastOnce());
        Mock.Get(manager).Verify(manager => manager.HasStatusAsync(token, Statuses.Redeemed, It.IsAny<CancellationToken>()), Times.Once());
        Mock.Get(manager).Verify(manager => manager.GetRedemptionDateAsync(token, It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task HandleTokenRequest_RequestIsRejectedWhenRefreshTokenIsAlreadyRedeemedAndLeewayIsNull()
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
                .ReturnsAsync(true);

            mock.Setup(manager => manager.GetRedemptionDateAsync(token, It.IsAny<CancellationToken>()))
                .ReturnsAsync(DateTimeOffset.UtcNow);
        });

        await using var server = await CreateServerAsync(options =>
        {
            options.SetRefreshTokenReuseLeeway(leeway: null);

            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal("8xLOxBtZp8", context.Token);
                    Assert.Equal(new[] { TokenTypeHints.RefreshToken }, context.ValidTokenTypes);

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetTokenType(TokenTypeHints.RefreshToken)
                        .SetTokenId("60FFF7EA-F98E-437B-937E-5073CC313103")
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));

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
        Assert.Equal(Errors.InvalidGrant, response.Error);
        Assert.Equal(SR.GetResourceString(SR.ID2012), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2012), response.ErrorUri);

        Mock.Get(manager).Verify(manager => manager.FindByIdAsync("60FFF7EA-F98E-437B-937E-5073CC313103", It.IsAny<CancellationToken>()), Times.AtLeastOnce());
        Mock.Get(manager).Verify(manager => manager.HasStatusAsync(token, Statuses.Redeemed, It.IsAny<CancellationToken>()), Times.Once());
        Mock.Get(manager).Verify(manager => manager.GetRedemptionDateAsync(token, It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task HandleTokenRequest_RequestIsRejectedWhenRefreshTokenIsAlreadyRedeemedAndCannotBeReused()
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
                .ReturnsAsync(true);

            mock.Setup(manager => manager.GetRedemptionDateAsync(token, It.IsAny<CancellationToken>()))
                .ReturnsAsync(DateTimeOffset.UtcNow - TimeSpan.FromMinutes(1));
        });

        await using var server = await CreateServerAsync(options =>
        {
            options.SetRefreshTokenReuseLeeway(TimeSpan.FromSeconds(5));

            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal("8xLOxBtZp8", context.Token);
                    Assert.Equal(new[] { TokenTypeHints.RefreshToken }, context.ValidTokenTypes);

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetTokenType(TokenTypeHints.RefreshToken)
                        .SetTokenId("60FFF7EA-F98E-437B-937E-5073CC313103")
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));

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
        Assert.Equal(Errors.InvalidGrant, response.Error);
        Assert.Equal(SR.GetResourceString(SR.ID2012), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2012), response.ErrorUri);

        Mock.Get(manager).Verify(manager => manager.FindByIdAsync("60FFF7EA-F98E-437B-937E-5073CC313103", It.IsAny<CancellationToken>()), Times.AtLeastOnce());
        Mock.Get(manager).Verify(manager => manager.HasStatusAsync(token, Statuses.Redeemed, It.IsAny<CancellationToken>()), Times.Once());
        Mock.Get(manager).Verify(manager => manager.GetRedemptionDateAsync(token, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task HandleTokenRequest_RequestIsValidatedWhenRefreshTokenIsAlreadyRedeemedAndCanBeReused()
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
                .ReturnsAsync(true);

            mock.Setup(manager => manager.GetRedemptionDateAsync(token, It.IsAny<CancellationToken>()))
                .ReturnsAsync(DateTimeOffset.UtcNow - TimeSpan.FromMinutes(1));

            mock.Setup(manager => manager.CreateAsync(It.IsAny<OpenIddictTokenDescriptor>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OpenIddictToken());
        });

        await using var server = await CreateServerAsync(options =>
        {
            options.SetRefreshTokenReuseLeeway(TimeSpan.FromMinutes(5));

            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal("8xLOxBtZp8", context.Token);
                    Assert.Equal(new[] { TokenTypeHints.RefreshToken }, context.ValidTokenTypes);

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetTokenType(TokenTypeHints.RefreshToken)
                        .SetTokenId("60FFF7EA-F98E-437B-937E-5073CC313103")
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));

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
        Assert.NotNull(response.AccessToken);

        Mock.Get(manager).Verify(manager => manager.FindByIdAsync("60FFF7EA-F98E-437B-937E-5073CC313103", It.IsAny<CancellationToken>()), Times.AtLeastOnce());
        Mock.Get(manager).Verify(manager => manager.HasStatusAsync(token, Statuses.Redeemed, It.IsAny<CancellationToken>()), Times.Once());
        Mock.Get(manager).Verify(manager => manager.GetRedemptionDateAsync(token, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task HandleTokenRequest_RevokesTokensWhenAuthorizationCodeIsAlreadyRedeemed()
    {
        // Arrange
        var tokens = ImmutableArray.Create(
            new OpenIddictToken(),
            new OpenIddictToken(),
            new OpenIddictToken());

        var manager = CreateTokenManager(mock =>
        {
            mock.Setup(manager => manager.FindByIdAsync("3E228451-1555-46F7-A471-951EFBA23A56", It.IsAny<CancellationToken>()))
                .ReturnsAsync(tokens[0]);

            mock.Setup(manager => manager.GetIdAsync(tokens[0], It.IsAny<CancellationToken>()))
                .ReturnsAsync("3E228451-1555-46F7-A471-951EFBA23A56");

            mock.Setup(manager => manager.GetIdAsync(tokens[1], It.IsAny<CancellationToken>()))
                .ReturnsAsync("47468A64-C9A7-49C7-939C-19CC0F5DD166");

            mock.Setup(manager => manager.GetIdAsync(tokens[2], It.IsAny<CancellationToken>()))
                .ReturnsAsync("3BEA7A94-5ADA-49AF-9F41-8AB6156E31A8");

            mock.Setup(manager => manager.GetAuthorizationIdAsync(tokens[0], It.IsAny<CancellationToken>()))
                .ReturnsAsync("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0");

            mock.Setup(manager => manager.HasStatusAsync(tokens[0], Statuses.Redeemed, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            mock.Setup(manager => manager.FindByAuthorizationIdAsync("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0", It.IsAny<CancellationToken>()))
                .Returns(tokens.ToAsyncEnumerable());
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
                        .SetAuthorizationId("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0")
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));

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
        Assert.Equal(Errors.InvalidGrant, response.Error);
        Assert.Equal(SR.GetResourceString(SR.ID2010), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2010), response.ErrorUri);

        Mock.Get(manager).Verify(manager => manager.FindByIdAsync("3E228451-1555-46F7-A471-951EFBA23A56", It.IsAny<CancellationToken>()), Times.AtLeastOnce());
        Mock.Get(manager).Verify(manager => manager.HasStatusAsync(tokens[0], Statuses.Redeemed, It.IsAny<CancellationToken>()), Times.Once());
        Mock.Get(manager).Verify(manager => manager.TryRevokeAsync(tokens[0], It.IsAny<CancellationToken>()), Times.Once());
        Mock.Get(manager).Verify(manager => manager.TryRevokeAsync(tokens[1], It.IsAny<CancellationToken>()), Times.Once());
        Mock.Get(manager).Verify(manager => manager.TryRevokeAsync(tokens[2], It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task HandleTokenRequest_RevokesTokensWhenRefreshTokenIsAlreadyRedeemedAndLeewayIsNull()
    {
        // Arrange
        var tokens = ImmutableArray.Create(
            new OpenIddictToken(),
            new OpenIddictToken(),
            new OpenIddictToken());

        var manager = CreateTokenManager(mock =>
        {
            mock.Setup(manager => manager.FindByIdAsync("60FFF7EA-F98E-437B-937E-5073CC313103", It.IsAny<CancellationToken>()))
                .ReturnsAsync(tokens[0]);

            mock.Setup(manager => manager.GetIdAsync(tokens[0], It.IsAny<CancellationToken>()))
                .ReturnsAsync("60FFF7EA-F98E-437B-937E-5073CC313103");

            mock.Setup(manager => manager.GetIdAsync(tokens[1], It.IsAny<CancellationToken>()))
                .ReturnsAsync("47468A64-C9A7-49C7-939C-19CC0F5DD166");

            mock.Setup(manager => manager.GetIdAsync(tokens[2], It.IsAny<CancellationToken>()))
                .ReturnsAsync("3BEA7A94-5ADA-49AF-9F41-8AB6156E31A8");

            mock.Setup(manager => manager.GetAuthorizationIdAsync(tokens[0], It.IsAny<CancellationToken>()))
                .ReturnsAsync("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0");

            mock.Setup(manager => manager.HasStatusAsync(tokens[0], Statuses.Redeemed, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            mock.Setup(manager => manager.GetRedemptionDateAsync(tokens[0], It.IsAny<CancellationToken>()))
                .ReturnsAsync(DateTimeOffset.UtcNow);

            mock.Setup(manager => manager.FindByAuthorizationIdAsync("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0", It.IsAny<CancellationToken>()))
                .Returns(tokens.ToAsyncEnumerable());
        });

        await using var server = await CreateServerAsync(options =>
        {
            options.SetRefreshTokenReuseLeeway(leeway: null);

            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal("8xLOxBtZp8", context.Token);
                    Assert.Equal(new[] { TokenTypeHints.RefreshToken }, context.ValidTokenTypes);

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetTokenType(TokenTypeHints.RefreshToken)
                        .SetPresenters("Fabrikam")
                        .SetTokenId("60FFF7EA-F98E-437B-937E-5073CC313103")
                        .SetAuthorizationId("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0")
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));

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
        Assert.Equal(Errors.InvalidGrant, response.Error);
        Assert.Equal(SR.GetResourceString(SR.ID2012), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2012), response.ErrorUri);

        Mock.Get(manager).Verify(manager => manager.FindByIdAsync("60FFF7EA-F98E-437B-937E-5073CC313103", It.IsAny<CancellationToken>()), Times.AtLeastOnce());
        Mock.Get(manager).Verify(manager => manager.HasStatusAsync(tokens[0], Statuses.Redeemed, It.IsAny<CancellationToken>()), Times.Once());
        Mock.Get(manager).Verify(manager => manager.TryRevokeAsync(tokens[0], It.IsAny<CancellationToken>()), Times.Once());
        Mock.Get(manager).Verify(manager => manager.TryRevokeAsync(tokens[1], It.IsAny<CancellationToken>()), Times.Once());
        Mock.Get(manager).Verify(manager => manager.TryRevokeAsync(tokens[2], It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task HandleTokenRequest_RevokesTokensWhenRefreshTokenIsAlreadyRedeemedAndCannotBeReused()
    {
        // Arrange
        var tokens = ImmutableArray.Create(
            new OpenIddictToken(),
            new OpenIddictToken(),
            new OpenIddictToken());

        var manager = CreateTokenManager(mock =>
        {
            mock.Setup(manager => manager.FindByIdAsync("60FFF7EA-F98E-437B-937E-5073CC313103", It.IsAny<CancellationToken>()))
                .ReturnsAsync(tokens[0]);

            mock.Setup(manager => manager.GetIdAsync(tokens[0], It.IsAny<CancellationToken>()))
                .ReturnsAsync("60FFF7EA-F98E-437B-937E-5073CC313103");

            mock.Setup(manager => manager.GetIdAsync(tokens[1], It.IsAny<CancellationToken>()))
                .ReturnsAsync("47468A64-C9A7-49C7-939C-19CC0F5DD166");

            mock.Setup(manager => manager.GetIdAsync(tokens[2], It.IsAny<CancellationToken>()))
                .ReturnsAsync("3BEA7A94-5ADA-49AF-9F41-8AB6156E31A8");

            mock.Setup(manager => manager.GetAuthorizationIdAsync(tokens[0], It.IsAny<CancellationToken>()))
                .ReturnsAsync("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0");

            mock.Setup(manager => manager.HasStatusAsync(tokens[0], Statuses.Redeemed, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            mock.Setup(manager => manager.GetRedemptionDateAsync(tokens[0], It.IsAny<CancellationToken>()))
                .ReturnsAsync(DateTimeOffset.UtcNow - TimeSpan.FromMinutes(1));

            mock.Setup(manager => manager.FindByAuthorizationIdAsync("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0", It.IsAny<CancellationToken>()))
                .Returns(tokens.ToAsyncEnumerable());
        });

        await using var server = await CreateServerAsync(options =>
        {
            options.SetRefreshTokenReuseLeeway(TimeSpan.FromSeconds(5));

            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal("8xLOxBtZp8", context.Token);
                    Assert.Equal(new[] { TokenTypeHints.RefreshToken }, context.ValidTokenTypes);

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetTokenType(TokenTypeHints.RefreshToken)
                        .SetPresenters("Fabrikam")
                        .SetTokenId("60FFF7EA-F98E-437B-937E-5073CC313103")
                        .SetAuthorizationId("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0")
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));

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
        Assert.Equal(Errors.InvalidGrant, response.Error);
        Assert.Equal(SR.GetResourceString(SR.ID2012), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2012), response.ErrorUri);

        Mock.Get(manager).Verify(manager => manager.FindByIdAsync("60FFF7EA-F98E-437B-937E-5073CC313103", It.IsAny<CancellationToken>()), Times.AtLeastOnce());
        Mock.Get(manager).Verify(manager => manager.HasStatusAsync(tokens[0], Statuses.Redeemed, It.IsAny<CancellationToken>()), Times.Once());
        Mock.Get(manager).Verify(manager => manager.TryRevokeAsync(tokens[0], It.IsAny<CancellationToken>()), Times.Once());
        Mock.Get(manager).Verify(manager => manager.TryRevokeAsync(tokens[1], It.IsAny<CancellationToken>()), Times.Once());
        Mock.Get(manager).Verify(manager => manager.TryRevokeAsync(tokens[2], It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task HandleTokenRequest_DoesNotRevokeTokensWhenRefreshTokenIsAlreadyRedeemedAndCanBeReused()
    {
        // Arrange
        var tokens = ImmutableArray.Create(
            new OpenIddictToken(),
            new OpenIddictToken(),
            new OpenIddictToken());

        var manager = CreateTokenManager(mock =>
        {
            mock.Setup(manager => manager.FindByIdAsync("60FFF7EA-F98E-437B-937E-5073CC313103", It.IsAny<CancellationToken>()))
                .ReturnsAsync(tokens[0]);

            mock.Setup(manager => manager.GetIdAsync(tokens[0], It.IsAny<CancellationToken>()))
                .ReturnsAsync("60FFF7EA-F98E-437B-937E-5073CC313103");

            mock.Setup(manager => manager.GetIdAsync(tokens[1], It.IsAny<CancellationToken>()))
                .ReturnsAsync("47468A64-C9A7-49C7-939C-19CC0F5DD166");

            mock.Setup(manager => manager.GetIdAsync(tokens[2], It.IsAny<CancellationToken>()))
                .ReturnsAsync("3BEA7A94-5ADA-49AF-9F41-8AB6156E31A8");

            mock.Setup(manager => manager.GetAuthorizationIdAsync(tokens[0], It.IsAny<CancellationToken>()))
                .ReturnsAsync("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0");

            mock.Setup(manager => manager.HasStatusAsync(tokens[0], Statuses.Redeemed, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            mock.Setup(manager => manager.GetRedemptionDateAsync(tokens[0], It.IsAny<CancellationToken>()))
                .ReturnsAsync(DateTimeOffset.UtcNow - TimeSpan.FromMinutes(1));

            mock.Setup(manager => manager.FindByAuthorizationIdAsync("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0", It.IsAny<CancellationToken>()))
                .Returns(tokens.ToAsyncEnumerable());

            mock.Setup(manager => manager.CreateAsync(It.IsAny<OpenIddictTokenDescriptor>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OpenIddictToken());
        });

        await using var server = await CreateServerAsync(options =>
        {
            options.SetRefreshTokenReuseLeeway(TimeSpan.FromMinutes(5));

            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal("8xLOxBtZp8", context.Token);
                    Assert.Equal(new[] { TokenTypeHints.RefreshToken }, context.ValidTokenTypes);

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetTokenType(TokenTypeHints.RefreshToken)
                        .SetTokenId("60FFF7EA-F98E-437B-937E-5073CC313103")
                        .SetAuthorizationId("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0")
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));

            options.Services.AddSingleton(manager);

            options.Services.AddSingleton(CreateAuthorizationManager(mock =>
            {
                var authorization = new OpenIddictAuthorization();

                mock.Setup(manager => manager.FindByIdAsync("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(authorization);

                mock.Setup(manager => manager.HasStatusAsync(authorization, Statuses.Valid, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
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
        Assert.NotNull(response.AccessToken);

        Mock.Get(manager).Verify(manager => manager.FindByIdAsync("60FFF7EA-F98E-437B-937E-5073CC313103", It.IsAny<CancellationToken>()), Times.AtLeastOnce());
        Mock.Get(manager).Verify(manager => manager.HasStatusAsync(tokens[0], Statuses.Redeemed, It.IsAny<CancellationToken>()), Times.Once());
        Mock.Get(manager).Verify(manager => manager.TryRevokeAsync(tokens[0], It.IsAny<CancellationToken>()), Times.Never());
        Mock.Get(manager).Verify(manager => manager.TryRevokeAsync(tokens[1], It.IsAny<CancellationToken>()), Times.Never());
        Mock.Get(manager).Verify(manager => manager.TryRevokeAsync(tokens[2], It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task HandleTokenRequest_RequestIsRejectedWhenAuthorizationCodeIsInvalid()
    {
        // Arrange
        var token = new OpenIddictToken();

        var manager = CreateTokenManager(mock =>
        {
            mock.Setup(manager => manager.FindByIdAsync("3E228451-1555-46F7-A471-951EFBA23A56", It.IsAny<CancellationToken>()))
                .ReturnsAsync(token);

            mock.Setup(manager => manager.GetIdAsync(token, It.IsAny<CancellationToken>()))
                .ReturnsAsync("3E228451-1555-46F7-A471-951EFBA23A56");

            mock.Setup(manager => manager.HasStatusAsync(token, Statuses.Redeemed, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            mock.Setup(manager => manager.HasStatusAsync(token, Statuses.Valid, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
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
                        .SetAuthorizationId("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0")
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));

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
        Assert.Equal(Errors.InvalidGrant, response.Error);
        Assert.Equal(SR.GetResourceString(SR.ID2016), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2016), response.ErrorUri);

        Mock.Get(manager).Verify(manager => manager.FindByIdAsync("3E228451-1555-46F7-A471-951EFBA23A56", It.IsAny<CancellationToken>()), Times.AtLeastOnce());
        Mock.Get(manager).Verify(manager => manager.HasStatusAsync(token, Statuses.Redeemed, It.IsAny<CancellationToken>()), Times.Once());
        Mock.Get(manager).Verify(manager => manager.HasStatusAsync(token, Statuses.Valid, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task HandleTokenRequest_RequestIsRejectedWhenRefreshTokenIsInvalid()
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
                .ReturnsAsync(false);
        });

        await using var server = await CreateServerAsync(options =>
        {
            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal("8xLOxBtZp8", context.Token);
                    Assert.Equal(new[] { TokenTypeHints.RefreshToken }, context.ValidTokenTypes);

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetTokenType(TokenTypeHints.RefreshToken)
                        .SetPresenters("Fabrikam")
                        .SetTokenId("60FFF7EA-F98E-437B-937E-5073CC313103")
                        .SetAuthorizationId("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0")
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));

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
        Assert.Equal(Errors.InvalidGrant, response.Error);
        Assert.Equal(SR.GetResourceString(SR.ID2018), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2018), response.ErrorUri);

        Mock.Get(manager).Verify(manager => manager.FindByIdAsync("60FFF7EA-F98E-437B-937E-5073CC313103", It.IsAny<CancellationToken>()), Times.AtLeastOnce());
        Mock.Get(manager).Verify(manager => manager.HasStatusAsync(token, Statuses.Valid, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task HandleTokenRequest_AuthorizationAssociatedWithCodeIsIgnoredWhenAuthorizationStorageIsDisabled()
    {
        // Arrange
        var manager = CreateAuthorizationManager(mock =>
        {
            mock.Setup(manager => manager.FindByIdAsync("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OpenIddictAuthorization());
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
                        .SetAuthorizationId("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0")
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));

            options.Services.AddSingleton(CreateApplicationManager(mock =>
            {
                var application = new OpenIddictApplication();

                mock.Setup(manager => manager.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(application);

                mock.Setup(manager => manager.HasClientTypeAsync(application, ClientTypes.Public, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
            }));

            options.Services.AddSingleton(CreateTokenManager(mock =>
            {
                var token = new OpenIddictToken();

                mock.Setup(manager => manager.FindByIdAsync("3E228451-1555-46F7-A471-951EFBA23A56", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                mock.Setup(manager => manager.GetIdAsync(token, It.IsAny<CancellationToken>()))
                    .ReturnsAsync("3E228451-1555-46F7-A471-951EFBA23A56");

                mock.Setup(manager => manager.HasStatusAsync(token, Statuses.Redeemed, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(false);

                mock.Setup(manager => manager.HasStatusAsync(token, Statuses.Valid, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                mock.Setup(manager => manager.GetAuthorizationIdAsync(token, It.IsAny<CancellationToken>()))
                    .ReturnsAsync("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0");

                mock.Setup(manager => manager.TryRedeemAsync(token, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                mock.Setup(manager => manager.CreateAsync(It.IsAny<OpenIddictTokenDescriptor>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new OpenIddictToken());
            }));

            options.Services.AddSingleton(manager);

            options.DisableAuthorizationStorage();
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
        Assert.NotNull(response.AccessToken);

        Mock.Get(manager).Verify(manager => manager.FindByIdAsync("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0", It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task HandleTokenRequest_AuthorizationAssociatedWithRefreshTokenIsIgnoredWhenAuthorizationStorageIsDisabled()
    {
        // Arrange
        var authorization = new OpenIddictAuthorization();

        var manager = CreateAuthorizationManager(mock =>
        {
            mock.Setup(manager => manager.FindByIdAsync("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OpenIddictAuthorization());
        });

        await using var server = await CreateServerAsync(options =>
        {
            options.DisableRollingRefreshTokens();

            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal("8xLOxBtZp8", context.Token);
                    Assert.Equal(new[] { TokenTypeHints.RefreshToken }, context.ValidTokenTypes);

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetTokenType(TokenTypeHints.RefreshToken)
                        .SetTokenId("60FFF7EA-F98E-437B-937E-5073CC313103")
                        .SetAuthorizationId("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0")
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));

            options.Services.AddSingleton(CreateTokenManager(mock =>
            {
                var token = new OpenIddictToken();

                mock.Setup(manager => manager.FindByIdAsync("60FFF7EA-F98E-437B-937E-5073CC313103", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                mock.Setup(manager => manager.GetIdAsync(token, It.IsAny<CancellationToken>()))
                    .ReturnsAsync("60FFF7EA-F98E-437B-937E-5073CC313103");

                mock.Setup(manager => manager.HasStatusAsync(token, Statuses.Redeemed, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(false);

                mock.Setup(manager => manager.HasStatusAsync(token, Statuses.Valid, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                mock.Setup(manager => manager.GetAuthorizationIdAsync(token, It.IsAny<CancellationToken>()))
                    .ReturnsAsync("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0");

                mock.Setup(manager => manager.CreateAsync(It.IsAny<OpenIddictTokenDescriptor>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new OpenIddictToken());
            }));

            options.Services.AddSingleton(manager);

            options.DisableAuthorizationStorage();
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

        Mock.Get(manager).Verify(manager => manager.FindByIdAsync("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0", It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task HandleTokenRequest_RequestIsRejectedWhenAuthorizationAssociatedWithAuthorizationCodeCannotBeFound()
    {
        // Arrange
        var manager = CreateAuthorizationManager(mock =>
        {
            mock.Setup(manager => manager.FindByIdAsync("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0", It.IsAny<CancellationToken>()))
                .ReturnsAsync(value: null);
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
                        .SetAuthorizationId("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0")
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));

            options.Services.AddSingleton(CreateApplicationManager(mock =>
            {
                var application = new OpenIddictApplication();

                mock.Setup(manager => manager.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(application);

                mock.Setup(manager => manager.HasClientTypeAsync(application, ClientTypes.Public, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
            }));

            options.Services.AddSingleton(CreateTokenManager(mock =>
            {
                var token = new OpenIddictToken();

                mock.Setup(manager => manager.FindByIdAsync("3E228451-1555-46F7-A471-951EFBA23A56", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                mock.Setup(manager => manager.GetIdAsync(token, It.IsAny<CancellationToken>()))
                    .ReturnsAsync("3E228451-1555-46F7-A471-951EFBA23A56");

                mock.Setup(manager => manager.GetTypeAsync(token, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(TokenTypeHints.AuthorizationCode);

                mock.Setup(manager => manager.HasStatusAsync(token, Statuses.Redeemed, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(false);

                mock.Setup(manager => manager.HasStatusAsync(token, Statuses.Valid, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                mock.Setup(manager => manager.GetAuthorizationIdAsync(token, It.IsAny<CancellationToken>()))
                    .ReturnsAsync("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0");
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
        Assert.Equal(Errors.InvalidGrant, response.Error);
        Assert.Equal(SR.GetResourceString(SR.ID2020), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2020), response.ErrorUri);

        Mock.Get(manager).Verify(manager => manager.FindByIdAsync("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0", It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task HandleTokenRequest_RequestIsRejectedWhenAuthorizationAssociatedWithAuthorizationCodeIsInvalid()
    {
        // Arrange
        var authorization = new OpenIddictAuthorization();

        var manager = CreateAuthorizationManager(mock =>
        {
            mock.Setup(manager => manager.FindByIdAsync("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0", It.IsAny<CancellationToken>()))
                .ReturnsAsync(authorization);

            mock.Setup(manager => manager.HasStatusAsync(authorization, Statuses.Valid, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
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
                        .SetAuthorizationId("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0")
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));

            options.Services.AddSingleton(CreateApplicationManager(mock =>
            {
                var application = new OpenIddictApplication();

                mock.Setup(manager => manager.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(application);

                mock.Setup(manager => manager.HasClientTypeAsync(application, ClientTypes.Public, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
            }));

            options.Services.AddSingleton(CreateTokenManager(mock =>
            {
                var token = new OpenIddictToken();

                mock.Setup(manager => manager.FindByIdAsync("3E228451-1555-46F7-A471-951EFBA23A56", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                mock.Setup(manager => manager.GetIdAsync(token, It.IsAny<CancellationToken>()))
                    .ReturnsAsync("3E228451-1555-46F7-A471-951EFBA23A56");

                mock.Setup(manager => manager.GetTypeAsync(token, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(TokenTypeHints.AuthorizationCode);

                mock.Setup(manager => manager.HasStatusAsync(token, Statuses.Redeemed, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(false);

                mock.Setup(manager => manager.HasStatusAsync(token, Statuses.Valid, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                mock.Setup(manager => manager.GetAuthorizationIdAsync(token, It.IsAny<CancellationToken>()))
                    .ReturnsAsync("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0");
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
        Assert.Equal(Errors.InvalidGrant, response.Error);
        Assert.Equal(SR.GetResourceString(SR.ID2020), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2020), response.ErrorUri);

        Mock.Get(manager).Verify(manager => manager.FindByIdAsync("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0", It.IsAny<CancellationToken>()), Times.Once());
        Mock.Get(manager).Verify(manager => manager.HasStatusAsync(authorization, Statuses.Valid, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task HandleTokenRequest_RequestIsRejectedWhenAuthorizationAssociatedWithRefreshTokenCannotBeFound()
    {
        // Arrange
        var manager = CreateAuthorizationManager(mock =>
        {
            mock.Setup(manager => manager.FindByIdAsync("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0", It.IsAny<CancellationToken>()))
                .ReturnsAsync(value: null);
        });

        await using var server = await CreateServerAsync(options =>
        {
            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal("8xLOxBtZp8", context.Token);
                    Assert.Equal(new[] { TokenTypeHints.RefreshToken }, context.ValidTokenTypes);

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetTokenType(TokenTypeHints.RefreshToken)
                        .SetTokenId("60FFF7EA-F98E-437B-937E-5073CC313103")
                        .SetAuthorizationId("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0")
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));

            options.Services.AddSingleton(CreateTokenManager(mock =>
            {
                var token = new OpenIddictToken();

                mock.Setup(manager => manager.FindByIdAsync("60FFF7EA-F98E-437B-937E-5073CC313103", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                mock.Setup(manager => manager.GetIdAsync(token, It.IsAny<CancellationToken>()))
                    .ReturnsAsync("60FFF7EA-F98E-437B-937E-5073CC313103");

                mock.Setup(manager => manager.GetTypeAsync(token, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(TokenTypeHints.RefreshToken);

                mock.Setup(manager => manager.HasStatusAsync(token, Statuses.Redeemed, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(false);

                mock.Setup(manager => manager.HasStatusAsync(token, Statuses.Valid, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                mock.Setup(manager => manager.GetAuthorizationIdAsync(token, It.IsAny<CancellationToken>()))
                    .ReturnsAsync("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0");
            }));

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
        Assert.Equal(Errors.InvalidGrant, response.Error);
        Assert.Equal(SR.GetResourceString(SR.ID2022), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2022), response.ErrorUri);

        Mock.Get(manager).Verify(manager => manager.FindByIdAsync("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0", It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task HandleTokenRequest_RequestIsRejectedWhenAuthorizationAssociatedWithRefreshTokenIsInvalid()
    {
        // Arrange
        var authorization = new OpenIddictAuthorization();

        var manager = CreateAuthorizationManager(mock =>
        {
            mock.Setup(manager => manager.FindByIdAsync("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0", It.IsAny<CancellationToken>()))
                .ReturnsAsync(authorization);

            mock.Setup(manager => manager.HasStatusAsync(authorization, Statuses.Valid, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        });

        await using var server = await CreateServerAsync(options =>
        {
            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    Assert.Equal("8xLOxBtZp8", context.Token);
                    Assert.Equal(new[] { TokenTypeHints.RefreshToken }, context.ValidTokenTypes);

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetTokenType(TokenTypeHints.RefreshToken)
                        .SetTokenId("60FFF7EA-F98E-437B-937E-5073CC313103")
                        .SetAuthorizationId("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0")
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));

            options.Services.AddSingleton(CreateTokenManager(mock =>
            {
                var token = new OpenIddictToken();

                mock.Setup(manager => manager.FindByIdAsync("60FFF7EA-F98E-437B-937E-5073CC313103", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                mock.Setup(manager => manager.GetIdAsync(token, It.IsAny<CancellationToken>()))
                    .ReturnsAsync("60FFF7EA-F98E-437B-937E-5073CC313103");

                mock.Setup(manager => manager.GetTypeAsync(token, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(TokenTypeHints.RefreshToken);

                mock.Setup(manager => manager.HasStatusAsync(token, Statuses.Redeemed, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(false);

                mock.Setup(manager => manager.HasStatusAsync(token, Statuses.Valid, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                mock.Setup(manager => manager.GetAuthorizationIdAsync(token, It.IsAny<CancellationToken>()))
                    .ReturnsAsync("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0");
            }));

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
        Assert.Equal(Errors.InvalidGrant, response.Error);
        Assert.Equal(SR.GetResourceString(SR.ID2022), response.ErrorDescription);
        Assert.Equal(SR.FormatID8000(SR.ID2022), response.ErrorUri);

        Mock.Get(manager).Verify(manager => manager.FindByIdAsync("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0", It.IsAny<CancellationToken>()), Times.Once());
        Mock.Get(manager).Verify(manager => manager.HasStatusAsync(authorization, Statuses.Valid, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Theory]
    [InlineData(GrantTypes.AuthorizationCode)]
    [InlineData(GrantTypes.ClientCredentials)]
    [InlineData(GrantTypes.Password)]
    [InlineData(GrantTypes.RefreshToken)]
    [InlineData("urn:ietf:params:oauth:grant-type:custom_grant")]
    public async Task HandleTokenRequest_RequestsAreSuccessfullyHandled(string flow)
    {
        // Arrange
        var manager = CreateTokenManager(mock =>
        {
            var token = new OpenIddictToken();

            mock.Setup(manager => manager.FindByIdAsync("0270F515-C5B1-4FBF-B673-D7CAF7CCDABC", It.IsAny<CancellationToken>()))
                .ReturnsAsync(token);

            mock.Setup(manager => manager.GetIdAsync(token, It.IsAny<CancellationToken>()))
                .ReturnsAsync("0270F515-C5B1-4FBF-B673-D7CAF7CCDABC");

            mock.Setup(manager => manager.HasStatusAsync(token, Statuses.Valid, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            mock.Setup(manager => manager.GetAuthorizationIdAsync(token, It.IsAny<CancellationToken>()))
                .ReturnsAsync("18D15F73-BE2B-6867-DC01-B3C1E8AFDED0");

            mock.Setup(manager => manager.TryRedeemAsync(token, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            mock.Setup(manager => manager.CreateAsync(It.IsAny<OpenIddictTokenDescriptor>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OpenIddictToken());
        });

        await using var server = await CreateServerAsync(options =>
        {
            options.DisableRollingRefreshTokens();

            options.AddEventHandler<ValidateTokenContext>(builder =>
            {
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetTokenType(context.Request!.IsAuthorizationCodeGrantType() ?
                            TokenTypeHints.AuthorizationCode :
                            TokenTypeHints.RefreshToken)
                        .SetPresenters("Fabrikam")
                        .SetTokenId("0270F515-C5B1-4FBF-B673-D7CAF7CCDABC")
                        .SetClaim(Claims.Subject, "Bob le Bricoleur");

                    return default;
                });

                builder.SetOrder(ValidateIdentityModelToken.Descriptor.Order - 500);
            });

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"))
                        .SetClaim(Claims.Subject, "Bob le Magnifique");

                    return default;
                }));

            options.Services.AddSingleton(CreateApplicationManager(mock =>
            {
                var application = new OpenIddictApplication();

                mock.Setup(manager => manager.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(application);

                mock.Setup(manager => manager.HasClientTypeAsync(application, ClientTypes.Confidential, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                mock.Setup(manager => manager.ValidateClientSecretAsync(application, "7Fjfp0ZBr1KtDRbnfVdmIw", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
            }));

            options.Services.AddSingleton(manager);

            options.AllowCustomFlow("urn:ietf:params:oauth:grant-type:custom_grant");
            options.DisableAuthorizationStorage();
        });

        await using var client = await server.CreateClientAsync();

        // Act
        var response = await client.PostAsync("/connect/token", new OpenIddictRequest
        {
            ClientId = "Fabrikam",
            ClientSecret = "7Fjfp0ZBr1KtDRbnfVdmIw",
            Code = "8xLOxBtZp8",
            GrantType = flow,
            RedirectUri = "http://www.fabrikam.com/path",
            RefreshToken = "8xLOxBtZp8",
            Username = "johndoe",
            Password = "A3ddj3w"
        });

        // Assert
        Assert.NotNull(response.AccessToken);
    }

    [Theory]
    [InlineData("custom_error", null, null)]
    [InlineData("custom_error", "custom_description", null)]
    [InlineData("custom_error", "custom_description", "custom_uri")]
    [InlineData(null, "custom_description", null)]
    [InlineData(null, "custom_description", "custom_uri")]
    [InlineData(null, null, "custom_uri")]
    [InlineData(null, null, null)]
    public async Task HandleTokenRequest_AllowsRejectingRequest(string error, string description, string uri)
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Reject(error, description, uri);

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
        Assert.Equal(error ?? Errors.InvalidGrant, response.Error);
        Assert.Equal(description, response.ErrorDescription);
        Assert.Equal(uri, response.ErrorUri);
    }

    [Fact]
    public async Task HandleTokenRequest_AllowsHandlingResponse()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<ValidateTokenRequestContext>(builder =>
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
    public async Task HandleTokenRequest_AllowsSkippingHandler()
    {
        // Arrange
        await using var server = await CreateServerAsync(options =>
        {
            options.EnableDegradedMode();

            options.AddEventHandler<ValidateTokenRequestContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.SkipRequest();

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
        Assert.Equal("Bob le Magnifique", (string?) response["name"]);
    }

    [Fact]
    public async Task HandleTokenRequest_ResponseContainsCustomParameters()
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

                    context.Parameters["custom_parameter"] = "custom_value";
                    context.Parameters["parameter_with_multiple_values"] = new[]
                    {
                        "custom_value_1",
                        "custom_value_2"
                    };

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
        Assert.Null(response.Error);
        Assert.Null(response.ErrorDescription);
        Assert.Null(response.ErrorUri);
        Assert.NotNull(response.AccessToken);
        Assert.Equal("custom_value", (string?) response["custom_parameter"]);
        Assert.Equal(new[] { "custom_value_1", "custom_value_2" }, (string[]?) response["parameter_with_multiple_values"]);
    }

    [Fact]
    public async Task ApplyTokenResponse_AllowsHandlingResponse()
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

            options.AddEventHandler<ApplyTokenResponseContext>(builder =>
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
    public async Task ApplyTokenResponse_ResponseContainsCustomParameters()
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

            options.AddEventHandler<ApplyTokenResponseContext>(builder =>
                builder.UseInlineHandler(context =>
                {
                    context.Response["custom_parameter"] = "custom_value";
                    context.Response["parameter_with_multiple_values"] = new[]
                    {
                        "custom_value_1",
                        "custom_value_2"
                    };

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
        Assert.Equal("custom_value", (string?) response["custom_parameter"]);
        Assert.Equal(new[] { "custom_value_1", "custom_value_2" }, (string[]?) response["parameter_with_multiple_values"]);
    }
}
