﻿/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System.Collections.Immutable;

namespace OpenIddict.Server.Owin;

public static partial class OpenIddictServerOwinHandlers
{
    public static class Introspection
    {
        public static ImmutableArray<OpenIddictServerHandlerDescriptor> DefaultHandlers { get; } = ImmutableArray.Create(
            /*
             * Introspection request extraction:
             */
            ExtractGetOrPostRequest<ExtractIntrospectionRequestContext>.Descriptor,
            ExtractBasicAuthenticationCredentials<ExtractIntrospectionRequestContext>.Descriptor,

            /*
             * Introspection response processing:
             */
            AttachHttpResponseCode<ApplyIntrospectionResponseContext>.Descriptor,
            AttachOwinResponseChallenge<ApplyIntrospectionResponseContext>.Descriptor,
            SuppressFormsAuthenticationRedirect<ApplyIntrospectionResponseContext>.Descriptor,
            AttachWwwAuthenticateHeader<ApplyIntrospectionResponseContext>.Descriptor,
            ProcessJsonResponse<ApplyIntrospectionResponseContext>.Descriptor);
    }
}
