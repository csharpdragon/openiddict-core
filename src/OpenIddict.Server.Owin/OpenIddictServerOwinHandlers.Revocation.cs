﻿/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System.Collections.Immutable;

namespace OpenIddict.Server.Owin;

public static partial class OpenIddictServerOwinHandlers
{
    public static class Revocation
    {
        public static ImmutableArray<OpenIddictServerHandlerDescriptor> DefaultHandlers { get; } = ImmutableArray.Create(
            /*
             * Revocation request extraction:
             */
            ExtractPostRequest<ExtractRevocationRequestContext>.Descriptor,
            ExtractBasicAuthenticationCredentials<ExtractRevocationRequestContext>.Descriptor,

            /*
             * Revocation response processing:
             */
            AttachHttpResponseCode<ApplyRevocationResponseContext>.Descriptor,
            AttachOwinResponseChallenge<ApplyRevocationResponseContext>.Descriptor,
            SuppressFormsAuthenticationRedirect<ApplyRevocationResponseContext>.Descriptor,
            AttachCacheControlHeader<ApplyRevocationResponseContext>.Descriptor,
            AttachWwwAuthenticateHeader<ApplyRevocationResponseContext>.Descriptor,
            ProcessJsonResponse<ApplyRevocationResponseContext>.Descriptor);
    }
}
