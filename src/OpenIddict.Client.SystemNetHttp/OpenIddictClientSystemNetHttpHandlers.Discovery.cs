﻿/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System.Collections.Immutable;

namespace OpenIddict.Client.SystemNetHttp;

public static partial class OpenIddictClientSystemNetHttpHandlers
{
    public static class Discovery
    {
        public static ImmutableArray<OpenIddictClientHandlerDescriptor> DefaultHandlers { get; } = ImmutableArray.Create(
            /*
             * Configuration request processing:
             */
            PrepareGetHttpRequest<PrepareConfigurationRequestContext>.Descriptor,
            AttachUserAgent<PrepareConfigurationRequestContext>.Descriptor,
            AttachQueryStringParameters<PrepareConfigurationRequestContext>.Descriptor,
            SendHttpRequest<ApplyConfigurationRequestContext>.Descriptor,
            DisposeHttpRequest<ApplyConfigurationRequestContext>.Descriptor,

            /*
             * Configuration response processing:
             */
            ExtractJsonHttpResponse<ExtractConfigurationResponseContext>.Descriptor,
            ExtractWwwAuthenticateHeader<ExtractConfigurationResponseContext>.Descriptor,
            ValidateHttpResponse<ExtractConfigurationResponseContext>.Descriptor,
            DisposeHttpResponse<ExtractConfigurationResponseContext>.Descriptor,

            /*
             * Cryptography request processing:
             */
            PrepareGetHttpRequest<PrepareCryptographyRequestContext>.Descriptor,
            AttachUserAgent<PrepareCryptographyRequestContext>.Descriptor,
            AttachQueryStringParameters<PrepareCryptographyRequestContext>.Descriptor,
            SendHttpRequest<ApplyCryptographyRequestContext>.Descriptor,
            DisposeHttpRequest<ApplyCryptographyRequestContext>.Descriptor,

            /*
             * Configuration response processing:
             */
            ExtractJsonHttpResponse<ExtractCryptographyResponseContext>.Descriptor,
            ExtractWwwAuthenticateHeader<ExtractCryptographyResponseContext>.Descriptor,
            ValidateHttpResponse<ExtractCryptographyResponseContext>.Descriptor,
            DisposeHttpResponse<ExtractCryptographyResponseContext>.Descriptor);
    }
}
