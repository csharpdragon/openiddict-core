﻿/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System.Collections.Immutable;
using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using static OpenIddict.Client.DataProtection.OpenIddictClientDataProtectionConstants.Purposes;
using static OpenIddict.Client.OpenIddictClientHandlers.Protection;
using Schemes = OpenIddict.Client.DataProtection.OpenIddictClientDataProtectionConstants.Purposes.Schemes;

namespace OpenIddict.Client.DataProtection;

public static partial class OpenIddictClientDataProtectionHandlers
{
    public static class Protection
    {
        public static ImmutableArray<OpenIddictClientHandlerDescriptor> DefaultHandlers { get; } = ImmutableArray.Create(
            /*
             * Token validation:
             */
            ValidateDataProtectionToken.Descriptor,

            /*
             * Token generation:
             */
            OverrideGeneratedTokenFormat.Descriptor,
            GenerateDataProtectionToken.Descriptor);

        /// <summary>
        /// Contains the logic responsible for validating tokens generated using Data Protection.
        /// </summary>
        public class ValidateDataProtectionToken : IOpenIddictClientHandler<ValidateTokenContext>
        {
            private readonly IOptionsMonitor<OpenIddictClientDataProtectionOptions> _options;

            public ValidateDataProtectionToken(IOptionsMonitor<OpenIddictClientDataProtectionOptions> options)
                => _options = options ?? throw new ArgumentNullException(nameof(options));

            /// <summary>
            /// Gets the default descriptor definition assigned to this handler.
            /// </summary>
            public static OpenIddictClientHandlerDescriptor Descriptor { get; }
                = OpenIddictClientHandlerDescriptor.CreateBuilder<ValidateTokenContext>()
                    .UseSingletonHandler<ValidateDataProtectionToken>()
                    .SetOrder(ValidateIdentityModelToken.Descriptor.Order + 500)
                    .SetType(OpenIddictClientHandlerType.BuiltIn)
                    .Build();

            /// <inheritdoc/>
            public ValueTask HandleAsync(ValidateTokenContext context)
            {
                // If a principal was already attached, don't overwrite it.
                if (context.Principal is not null)
                {
                    return default;
                }

                // Note: ASP.NET Core Data Protection tokens always start with "CfDJ8", that corresponds
                // to the base64 representation of the magic "09 F0 C9 F0" header identifying DP payloads.
                if (!context.Token.StartsWith("CfDJ8", StringComparison.Ordinal))
                {
                    return default;
                }

                // Note: unlike the equivalent handler in the server stack, the logic used here
                // is simpler as only state tokens are currently supported by the client stack.
                var principal = context.ValidTokenTypes.Count switch
                {
                    // If no valid token type was set, all supported token types are allowed.
                    0 => ValidateToken(TokenTypeHints.StateToken),

                    _ when context.ValidTokenTypes.Contains(TokenTypeHints.StateToken)
                        => ValidateToken(TokenTypeHints.StateToken),

                    // The token type is not supported by the Data Protection integration (e.g client assertion tokens).
                    _ => null
                };

                if (principal is null)
                {
                    context.Reject(
                        error: Errors.InvalidToken,
                        description: SR.GetResourceString(SR.ID2004),
                        uri: SR.FormatID8000(SR.ID2004));

                    return default;
                }

                context.Principal = principal;

                context.Logger.LogTrace(SR.GetResourceString(SR.ID6152), context.Token, context.Principal.Claims);

                return default;

                ClaimsPrincipal? ValidateToken(string type)
                {
                    // Create a Data Protection protector using the provider registered in the options.
                    //
                    // Note: reference tokens are encrypted using a different "purpose" string than non-reference tokens.
                    var protector = _options.CurrentValue.DataProtectionProvider.CreateProtector(
                        (type, context.TokenId) switch
                        {
                            (TokenTypeHints.StateToken, { Length: not 0 })
                                => new[] { Handlers.Client, Formats.StateToken, Features.ReferenceTokens, Schemes.Server },
                            (TokenTypeHints.StateToken, null or { Length: 0 })
                                => new[] { Handlers.Client, Formats.StateToken, Schemes.Server },

                            _ => throw new InvalidOperationException(SR.GetResourceString(SR.ID0003))
                        });

                    try
                    {
                        using var buffer = new MemoryStream(protector.Unprotect(Base64UrlEncoder.DecodeBytes(context.Token)));
                        using var reader = new BinaryReader(buffer);

                        // Note: since the data format relies on a data protector using different "purposes" strings
                        // per token type, the token processed at this stage is guaranteed to be of the expected type.
                        return _options.CurrentValue.Formatter.ReadToken(reader)?.SetTokenType(type);
                    }

                    catch (Exception exception)
                    {
                        context.Logger.LogTrace(exception, SR.GetResourceString(SR.ID6153), context.Token);

                        return null;
                    }
                }
            }
        }

        /// <summary>
        /// Contains the logic responsible for overriding the default token format
        /// to generate ASP.NET Core Data Protection tokens instead of JSON Web Tokens.
        /// </summary>
        public class OverrideGeneratedTokenFormat : IOpenIddictClientHandler<GenerateTokenContext>
        {
            private readonly IOptionsMonitor<OpenIddictClientDataProtectionOptions> _options;

            public OverrideGeneratedTokenFormat(IOptionsMonitor<OpenIddictClientDataProtectionOptions> options)
                => _options = options ?? throw new ArgumentNullException(nameof(options));

            /// <summary>
            /// Gets the default descriptor definition assigned to this handler.
            /// </summary>
            public static OpenIddictClientHandlerDescriptor Descriptor { get; }
                = OpenIddictClientHandlerDescriptor.CreateBuilder<GenerateTokenContext>()
                    .UseSingletonHandler<OverrideGeneratedTokenFormat>()
                    .SetOrder(AttachSecurityCredentials.Descriptor.Order + 500)
                    .SetType(OpenIddictClientHandlerType.BuiltIn)
                    .Build();

            /// <inheritdoc/>
            public ValueTask HandleAsync(GenerateTokenContext context)
            {
                if (context is null)
                {
                    throw new ArgumentNullException(nameof(context));
                }

                // ASP.NET Core Data Protection can be used to format certain types of tokens in lieu
                // of the default token format (typically, JSON Web Token). By default, Data Protection
                // is automatically used for all the supported token types once the integration is enabled
                // but the default token format can be re-enabled in the options. Alternatively, the token
                // format can be overriden manually using a custom event handler registered after this one.

                context.TokenFormat = context.TokenType switch
                {
                    TokenTypeHints.StateToken when !_options.CurrentValue.PreferDefaultStateTokenFormat
                        => TokenFormats.Private.DataProtection,

                    _ => context.TokenFormat // Don't override the format if the token type is not supported.
                };

                return default;
            }
        }

        /// <summary>
        /// Contains the logic responsible for generating a token using Data Protection.
        /// </summary>
        public class GenerateDataProtectionToken : IOpenIddictClientHandler<GenerateTokenContext>
        {
            private readonly IOptionsMonitor<OpenIddictClientDataProtectionOptions> _options;

            public GenerateDataProtectionToken(IOptionsMonitor<OpenIddictClientDataProtectionOptions> options)
                => _options = options ?? throw new ArgumentNullException(nameof(options));

            /// <summary>
            /// Gets the default descriptor definition assigned to this handler.
            /// </summary>
            public static OpenIddictClientHandlerDescriptor Descriptor { get; }
                = OpenIddictClientHandlerDescriptor.CreateBuilder<GenerateTokenContext>()
                    .AddFilter<RequireDataProtectionTokenFormat>()
                    .UseSingletonHandler<GenerateDataProtectionToken>()
                    .SetOrder(GenerateIdentityModelToken.Descriptor.Order - 500)
                    .SetType(OpenIddictClientHandlerType.BuiltIn)
                    .Build();

            /// <inheritdoc/>
            public ValueTask HandleAsync(GenerateTokenContext context)
            {
                if (context is null)
                {
                    throw new ArgumentNullException(nameof(context));
                }

                // If an access token was already attached by another handler, don't overwrite it.
                if (!string.IsNullOrEmpty(context.Token))
                {
                    return default;
                }

                // Create a Data Protection protector using the provider registered in the options.
                //
                // Note: reference tokens are encrypted using a different "purpose" string than non-reference tokens.
                var protector = _options.CurrentValue.DataProtectionProvider.CreateProtector(
                    (context.TokenType, context.PersistTokenPayload) switch
                    {
                        (TokenTypeHints.StateToken, true)
                            => new[] { Handlers.Client, Formats.StateToken, Features.ReferenceTokens, Schemes.Server },
                        (TokenTypeHints.StateToken, false)
                            => new[] { Handlers.Client, Formats.StateToken, Schemes.Server },

                        _ => throw new InvalidOperationException(SR.GetResourceString(SR.ID0003))
                    });

                using var buffer = new MemoryStream();
                using var writer = new BinaryWriter(buffer);

                _options.CurrentValue.Formatter.WriteToken(writer, context.Principal);

                context.Token = Base64UrlEncoder.Encode(protector.Protect(buffer.ToArray()));

                context.Logger.LogTrace(SR.GetResourceString(SR.ID6013), context.TokenType,
                    context.Token, context.Principal.Claims);

                return default;
            }
        }
    }
}
