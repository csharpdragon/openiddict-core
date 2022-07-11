﻿/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using Properties = OpenIddict.Validation.AspNetCore.OpenIddictValidationAspNetCoreConstants.Properties;

#if SUPPORTS_JSON_NODES
using System.Text.Json.Nodes;
#endif

namespace OpenIddict.Validation.AspNetCore;

[EditorBrowsable(EditorBrowsableState.Never)]
public static partial class OpenIddictValidationAspNetCoreHandlers
{
    public static ImmutableArray<OpenIddictValidationHandlerDescriptor> DefaultHandlers { get; } = ImmutableArray.Create(
        /*
         * Request top-level processing:
         */
        InferIssuerFromHost.Descriptor,

        /*
         * Authentication processing:
         */
        ExtractAccessTokenFromAuthorizationHeader.Descriptor,
        ExtractAccessTokenFromBodyForm.Descriptor,
        ExtractAccessTokenFromQueryString.Descriptor,

        /*
         * Challenge processing:
         */
        AttachHostChallengeError.Descriptor,
        ResolveHostChallengeParameters.Descriptor,

        /*
         * Response processing:
         */
        AttachHttpResponseCode<ProcessChallengeContext>.Descriptor,
        AttachCacheControlHeader<ProcessChallengeContext>.Descriptor,
        AttachWwwAuthenticateHeader<ProcessChallengeContext>.Descriptor,
        ProcessChallengeErrorResponse<ProcessChallengeContext>.Descriptor,

        AttachHttpResponseCode<ProcessErrorContext>.Descriptor,
        AttachCacheControlHeader<ProcessErrorContext>.Descriptor,
        AttachWwwAuthenticateHeader<ProcessErrorContext>.Descriptor,
        ProcessChallengeErrorResponse<ProcessErrorContext>.Descriptor,

        /*
         * Error processing:
         */
        AttachErrorParameters.Descriptor);

    /// <summary>
    /// Contains the logic responsible for infering the default issuer from the HTTP request host and validating it.
    /// Note: this handler is not used when the OpenID Connect request is not initially handled by ASP.NET Core.
    /// </summary>
    public class InferIssuerFromHost : IOpenIddictValidationHandler<ProcessRequestContext>
    {
        /// <summary>
        /// Gets the default descriptor definition assigned to this handler.
        /// </summary>
        public static OpenIddictValidationHandlerDescriptor Descriptor { get; }
            = OpenIddictValidationHandlerDescriptor.CreateBuilder<ProcessRequestContext>()
                .AddFilter<RequireHttpRequest>()
                .UseSingletonHandler<InferIssuerFromHost>()
                .SetOrder(int.MinValue + 100_000)
                .SetType(OpenIddictValidationHandlerType.BuiltIn)
                .Build();

        /// <inheritdoc/>
        public ValueTask HandleAsync(ProcessRequestContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            // This handler only applies to ASP.NET Core requests. If the HTTP context cannot be resolved,
            // this may indicate that the request was incorrectly processed by another server stack.
            var request = context.Transaction.GetHttpRequest() ??
                throw new InvalidOperationException(SR.GetResourceString(SR.ID0114));

            // Only use the current host as the issuer if the
            // issuer was not explicitly set in the options.
            if (context.Issuer is not null)
            {
                return default;
            }

            if (!request.Host.HasValue)
            {
                context.Reject(
                    error: Errors.InvalidRequest,
                    description: SR.FormatID2081(HeaderNames.Host),
                    uri: SR.FormatID8000(SR.ID2081));

                return default;
            }

            if (!Uri.TryCreate(request.Scheme + Uri.SchemeDelimiter + request.Host + request.PathBase, UriKind.Absolute, out Uri? issuer) ||
                !issuer.IsWellFormedOriginalString())
            {
                context.Reject(
                    error: Errors.InvalidRequest,
                    description: SR.FormatID2082(HeaderNames.Host),
                    uri: SR.FormatID8000(SR.ID2082));

                return default;
            }

            context.Issuer = issuer;

            return default;
        }
    }

    /// <summary>
    /// Contains the logic responsible for extracting the access token from the standard HTTP Authorization header.
    /// Note: this handler is not used when the OpenID Connect request is not initially handled by ASP.NET Core.
    /// </summary>
    public class ExtractAccessTokenFromAuthorizationHeader : IOpenIddictValidationHandler<ProcessAuthenticationContext>
    {
        /// <summary>
        /// Gets the default descriptor definition assigned to this handler.
        /// </summary>
        public static OpenIddictValidationHandlerDescriptor Descriptor { get; }
            = OpenIddictValidationHandlerDescriptor.CreateBuilder<ProcessAuthenticationContext>()
                .AddFilter<RequireHttpRequest>()
                .AddFilter<RequireAccessTokenExtracted>()
                .UseSingletonHandler<ExtractAccessTokenFromAuthorizationHeader>()
                .SetOrder(EvaluateValidatedTokens.Descriptor.Order + 500)
                .SetType(OpenIddictValidationHandlerType.BuiltIn)
                .Build();

        /// <inheritdoc/>
        public ValueTask HandleAsync(ProcessAuthenticationContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            // If a token was already resolved, don't overwrite it.
            if (!string.IsNullOrEmpty(context.AccessToken))
            {
                return default;
            }

            // This handler only applies to ASP.NET Core requests. If the HTTP context cannot be resolved,
            // this may indicate that the request was incorrectly processed by another server stack.
            var request = context.Transaction.GetHttpRequest() ??
                throw new InvalidOperationException(SR.GetResourceString(SR.ID0114));

            // Resolve the access token from the standard Authorization header.
            // See https://tools.ietf.org/html/rfc6750#section-2.1 for more information.
            string header = request.Headers[HeaderNames.Authorization];
            if (!string.IsNullOrEmpty(header) && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                context.AccessToken = header.Substring("Bearer ".Length);

                return default;
            }

            return default;
        }
    }

    /// <summary>
    /// Contains the logic responsible for extracting the access token from the standard access_token form parameter.
    /// Note: this handler is not used when the OpenID Connect request is not initially handled by ASP.NET Core.
    /// </summary>
    public class ExtractAccessTokenFromBodyForm : IOpenIddictValidationHandler<ProcessAuthenticationContext>
    {
        /// <summary>
        /// Gets the default descriptor definition assigned to this handler.
        /// </summary>
        public static OpenIddictValidationHandlerDescriptor Descriptor { get; }
            = OpenIddictValidationHandlerDescriptor.CreateBuilder<ProcessAuthenticationContext>()
                .AddFilter<RequireHttpRequest>()
                .AddFilter<RequireAccessTokenExtracted>()
                .UseSingletonHandler<ExtractAccessTokenFromBodyForm>()
                .SetOrder(ExtractAccessTokenFromAuthorizationHeader.Descriptor.Order + 1_000)
                .SetType(OpenIddictValidationHandlerType.BuiltIn)
                .Build();

        /// <inheritdoc/>
        public async ValueTask HandleAsync(ProcessAuthenticationContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            // If a token was already resolved, don't overwrite it.
            if (!string.IsNullOrEmpty(context.AccessToken))
            {
                return;
            }

            // This handler only applies to ASP.NET Core requests. If the HTTP context cannot be resolved,
            // this may indicate that the request was incorrectly processed by another server stack.
            var request = context.Transaction.GetHttpRequest() ??
                throw new InvalidOperationException(SR.GetResourceString(SR.ID0114));

            if (string.IsNullOrEmpty(request.ContentType) ||
                !request.ContentType.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Resolve the access token from the standard access_token form parameter.
            // See https://tools.ietf.org/html/rfc6750#section-2.2 for more information.
            var form = await request.ReadFormAsync(request.HttpContext.RequestAborted);
            if (form.TryGetValue(Parameters.AccessToken, out StringValues token))
            {
                context.AccessToken = token;

                return;
            }
        }
    }

    /// <summary>
    /// Contains the logic responsible for extracting the access token from the standard access_token query parameter.
    /// Note: this handler is not used when the OpenID Connect request is not initially handled by ASP.NET Core.
    /// </summary>
    public class ExtractAccessTokenFromQueryString : IOpenIddictValidationHandler<ProcessAuthenticationContext>
    {
        /// <summary>
        /// Gets the default descriptor definition assigned to this handler.
        /// </summary>
        public static OpenIddictValidationHandlerDescriptor Descriptor { get; }
            = OpenIddictValidationHandlerDescriptor.CreateBuilder<ProcessAuthenticationContext>()
                .AddFilter<RequireHttpRequest>()
                .AddFilter<RequireAccessTokenExtracted>()
                .UseSingletonHandler<ExtractAccessTokenFromQueryString>()
                .SetOrder(ExtractAccessTokenFromBodyForm.Descriptor.Order + 1_000)
                .SetType(OpenIddictValidationHandlerType.BuiltIn)
                .Build();

        /// <inheritdoc/>
        public ValueTask HandleAsync(ProcessAuthenticationContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            // If a token was already resolved, don't overwrite it.
            if (!string.IsNullOrEmpty(context.AccessToken))
            {
                return default;
            }

            // This handler only applies to ASP.NET Core requests. If the HTTP context cannot be resolved,
            // this may indicate that the request was incorrectly processed by another server stack.
            var request = context.Transaction.GetHttpRequest() ??
                throw new InvalidOperationException(SR.GetResourceString(SR.ID0114));

            // Resolve the access token from the standard access_token query parameter.
            // See https://tools.ietf.org/html/rfc6750#section-2.3 for more information.
            if (request.Query.TryGetValue(Parameters.AccessToken, out StringValues token))
            {
                context.AccessToken = token;

                return default;
            }

            return default;
        }
    }

    /// <summary>
    /// Contains the logic responsible for attaching the error details using the ASP.NET Core authentication properties.
    /// Note: this handler is not used when the OpenID Connect request is not initially handled by ASP.NET Core.
    /// </summary>
    public class AttachHostChallengeError : IOpenIddictValidationHandler<ProcessChallengeContext>
    {
        /// <summary>
        /// Gets the default descriptor definition assigned to this handler.
        /// </summary>
        public static OpenIddictValidationHandlerDescriptor Descriptor { get; }
            = OpenIddictValidationHandlerDescriptor.CreateBuilder<ProcessChallengeContext>()
                .AddFilter<RequireHttpRequest>()
                .UseSingletonHandler<AttachHostChallengeError>()
                .SetOrder(AttachDefaultChallengeError.Descriptor.Order - 500)
                .SetType(OpenIddictValidationHandlerType.BuiltIn)
                .Build();

        /// <inheritdoc/>
        public ValueTask HandleAsync(ProcessChallengeContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var properties = context.Transaction.GetProperty<AuthenticationProperties>(typeof(AuthenticationProperties).FullName!);
            if (properties is not null)
            {
                context.Response.Error = properties.GetString(Properties.Error);
                context.Response.ErrorDescription = properties.GetString(Properties.ErrorDescription);
                context.Response.ErrorUri = properties.GetString(Properties.ErrorUri);
                context.Response.Scope = properties.GetString(Properties.Scope);
            }

            return default;
        }
    }

    /// <summary>
    /// Contains the logic responsible for resolving the additional sign-in parameters stored in the ASP.NET
    /// Core authentication properties specified by the application that triggered the sign-in operation.
    /// Note: this handler is not used when the OpenID Connect request is not initially handled by ASP.NET Core.
    /// </summary>
    public class ResolveHostChallengeParameters : IOpenIddictValidationHandler<ProcessChallengeContext>
    {
        /// <summary>
        /// Gets the default descriptor definition assigned to this handler.
        /// </summary>
        public static OpenIddictValidationHandlerDescriptor Descriptor { get; }
            = OpenIddictValidationHandlerDescriptor.CreateBuilder<ProcessChallengeContext>()
                .AddFilter<RequireHttpRequest>()
                .UseSingletonHandler<ResolveHostChallengeParameters>()
                .SetOrder(AttachChallengeParameters.Descriptor.Order - 500)
                .SetType(OpenIddictValidationHandlerType.BuiltIn)
                .Build();

        /// <inheritdoc/>
        public ValueTask HandleAsync(ProcessChallengeContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var properties = context.Transaction.GetProperty<AuthenticationProperties>(typeof(AuthenticationProperties).FullName!);
            if (properties is null)
            {
                return default;
            }

            foreach (var parameter in properties.Parameters)
            {
                context.Parameters[parameter.Key] = parameter.Value switch
                {
                    OpenIddictParameter value => value,
                    JsonElement         value => new OpenIddictParameter(value),
                    bool                value => new OpenIddictParameter(value),
                    int                 value => new OpenIddictParameter(value),
                    long                value => new OpenIddictParameter(value),
                    string              value => new OpenIddictParameter(value),
                    string[]            value => new OpenIddictParameter(value),

#if SUPPORTS_JSON_NODES
                    JsonNode            value => new OpenIddictParameter(value),
#endif
                    _ => throw new InvalidOperationException(SR.GetResourceString(SR.ID0115))
                };
            }

            return default;
        }
    }

    /// <summary>
    /// Contains the logic responsible for attaching an appropriate HTTP status code.
    /// Note: this handler is not used when the OpenID Connect request is not initially handled by ASP.NET Core.
    /// </summary>
    public class AttachHttpResponseCode<TContext> : IOpenIddictValidationHandler<TContext> where TContext : BaseRequestContext
    {
        /// <summary>
        /// Gets the default descriptor definition assigned to this handler.
        /// </summary>
        public static OpenIddictValidationHandlerDescriptor Descriptor { get; }
            = OpenIddictValidationHandlerDescriptor.CreateBuilder<TContext>()
                .AddFilter<RequireHttpRequest>()
                .UseSingletonHandler<AttachHttpResponseCode<TContext>>()
                .SetOrder(100_000)
                .SetType(OpenIddictValidationHandlerType.BuiltIn)
                .Build();

        /// <inheritdoc/>
        public ValueTask HandleAsync(TContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            Debug.Assert(context.Transaction.Response is not null, SR.GetResourceString(SR.ID4007));

            // This handler only applies to ASP.NET Core requests. If the HTTP context cannot be resolved,
            // this may indicate that the request was incorrectly processed by another server stack.
            var response = context.Transaction.GetHttpRequest()?.HttpContext.Response ??
                throw new InvalidOperationException(SR.GetResourceString(SR.ID0114));

            response.StatusCode = context.Transaction.Response.Error switch
            {
                // Note: the default code may be replaced by another handler (e.g when doing redirects).
                null or { Length: 0 } => 200,

                Errors.InvalidToken or Errors.MissingToken => 401,

                Errors.InsufficientAccess or Errors.InsufficientScope => 403,

                Errors.ServerError => 500,

                _ => 400
            };

            return default;
        }
    }

    /// <summary>
    /// Contains the logic responsible for attaching the appropriate HTTP response cache headers.
    /// Note: this handler is not used when the OpenID Connect request is not initially handled by ASP.NET Core.
    /// </summary>
    public class AttachCacheControlHeader<TContext> : IOpenIddictValidationHandler<TContext> where TContext : BaseRequestContext
    {
        /// <summary>
        /// Gets the default descriptor definition assigned to this handler.
        /// </summary>
        public static OpenIddictValidationHandlerDescriptor Descriptor { get; }
            = OpenIddictValidationHandlerDescriptor.CreateBuilder<TContext>()
                .AddFilter<RequireHttpRequest>()
                .UseSingletonHandler<AttachCacheControlHeader<TContext>>()
                .SetOrder(AttachHttpResponseCode<TContext>.Descriptor.Order + 1_000)
                .SetType(OpenIddictValidationHandlerType.BuiltIn)
                .Build();

        /// <inheritdoc/>
        public ValueTask HandleAsync(TContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            // This handler only applies to ASP.NET Core requests. If the HTTP context cannot be resolved,
            // this may indicate that the request was incorrectly processed by another server stack.
            var response = context.Transaction.GetHttpRequest()?.HttpContext.Response ??
                throw new InvalidOperationException(SR.GetResourceString(SR.ID0114));

            // Prevent the response from being cached.
            response.Headers[HeaderNames.CacheControl] = "no-store";
            response.Headers[HeaderNames.Pragma] = "no-cache";
            response.Headers[HeaderNames.Expires] = "Thu, 01 Jan 1970 00:00:00 GMT";

            return default;
        }
    }

    /// <summary>
    /// Contains the logic responsible for attaching errors details to the WWW-Authenticate header.
    /// Note: this handler is not used when the OpenID Connect request is not initially handled by ASP.NET Core.
    /// </summary>
    public class AttachWwwAuthenticateHeader<TContext> : IOpenIddictValidationHandler<TContext> where TContext : BaseRequestContext
    {
        private readonly IOptionsMonitor<OpenIddictValidationAspNetCoreOptions> _options;

        public AttachWwwAuthenticateHeader(IOptionsMonitor<OpenIddictValidationAspNetCoreOptions> options)
            => _options = options ?? throw new ArgumentNullException(nameof(options));

        /// <summary>
        /// Gets the default descriptor definition assigned to this handler.
        /// </summary>
        public static OpenIddictValidationHandlerDescriptor Descriptor { get; }
            = OpenIddictValidationHandlerDescriptor.CreateBuilder<TContext>()
                .AddFilter<RequireHttpRequest>()
                .UseSingletonHandler<AttachWwwAuthenticateHeader<TContext>>()
                .SetOrder(AttachCacheControlHeader<TContext>.Descriptor.Order + 1_000)
                .SetType(OpenIddictValidationHandlerType.BuiltIn)
                .Build();

        /// <inheritdoc/>
        public ValueTask HandleAsync(TContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            Debug.Assert(context.Transaction.Response is not null, SR.GetResourceString(SR.ID4007));

            // This handler only applies to ASP.NET Core requests. If the HTTP context cannot be resolved,
            // this may indicate that the request was incorrectly processed by another server stack.
            var response = context.Transaction.GetHttpRequest()?.HttpContext.Response ??
                throw new InvalidOperationException(SR.GetResourceString(SR.ID0114));

            if (string.IsNullOrEmpty(context.Transaction.Response.Error))
            {
                return default;
            }

            // Note: unlike the server stack, the validation stack doesn't expose any endpoint
            // and thus never returns responses containing a formatted body (e.g a JSON response).
            //
            // As such, all errors - even errors indicating an invalid request - are returned
            // as part of the standard WWW-Authenticate header, as defined by the specification.
            // See https://datatracker.ietf.org/doc/html/rfc6750#section-3 for more information.

            var parameters = new Dictionary<string, string>(StringComparer.Ordinal);

            // If a realm was configured in the options, attach it to the parameters.
            if (_options.CurrentValue.Realm is string { Length: > 0 } realm)
            {
                parameters[Parameters.Realm] = realm;
            }

            foreach (var parameter in context.Transaction.Response.GetParameters())
            {
                // Note: the error details are only included if the error was not caused by a missing token, as recommended
                // by the OAuth 2.0 bearer specification: https://tools.ietf.org/html/rfc6750#section-3.1.
                if (context.Transaction.Response.Error is Errors.MissingToken &&
                    parameter.Key is Parameters.Error            or
                                     Parameters.ErrorDescription or
                                     Parameters.ErrorUri)
                {
                    continue;
                }

                // Ignore values that can't be represented as unique strings.
                var value = (string?) parameter.Value;
                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }

                parameters[parameter.Key] = value;
            }

            var builder = new StringBuilder(Schemes.Bearer);

            foreach (var parameter in parameters)
            {
                builder.Append(' ');
                builder.Append(parameter.Key);
                builder.Append('=');
                builder.Append('"');
                builder.Append(parameter.Value.Replace("\"", "\\\""));
                builder.Append('"');
                builder.Append(',');
            }

            // If the WWW-Authenticate header ends with a comma, remove it.
            if (builder[builder.Length - 1] == ',')
            {
                builder.Remove(builder.Length - 1, 1);
            }

            response.Headers.Append(HeaderNames.WWWAuthenticate, builder.ToString());

            return default;
        }
    }

    /// <summary>
    /// Contains the logic responsible for processing challenge responses that contain a WWW-Authenticate header.
    /// Note: this handler is not used when the OpenID Connect request is not initially handled by ASP.NET Core.
    /// </summary>
    public class ProcessChallengeErrorResponse<TContext> : IOpenIddictValidationHandler<TContext> where TContext : BaseRequestContext
    {
        /// <summary>
        /// Gets the default descriptor definition assigned to this handler.
        /// </summary>
        public static OpenIddictValidationHandlerDescriptor Descriptor { get; }
            = OpenIddictValidationHandlerDescriptor.CreateBuilder<TContext>()
                .AddFilter<RequireHttpRequest>()
                .UseSingletonHandler<ProcessChallengeErrorResponse<TContext>>()
                .SetOrder(AttachWwwAuthenticateHeader<TContext>.Descriptor.Order + 1_000)
                .SetType(OpenIddictValidationHandlerType.BuiltIn)
                .Build();

        /// <inheritdoc/>
        public ValueTask HandleAsync(TContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            // This handler only applies to ASP.NET Core requests. If the HTTP context cannot be resolved,
            // this may indicate that the request was incorrectly processed by another server stack.
            var response = context.Transaction.GetHttpRequest()?.HttpContext.Response ??
                throw new InvalidOperationException(SR.GetResourceString(SR.ID0114));

            // If the response doesn't contain a WWW-Authenticate header, don't return an empty response.
            if (!response.Headers.ContainsKey(HeaderNames.WWWAuthenticate))
            {
                return default;
            }

            context.Logger.LogInformation(SR.GetResourceString(SR.ID6141), context.Transaction.Response);
            context.HandleRequest();

            return default;
        }
    }
}
