﻿/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System.Security.Claims;

namespace OpenIddict.Client;

public static partial class OpenIddictClientEvents
{
    /// <summary>
    /// Represents an event called for each request to the authorization endpoint to give the user code
    /// a chance to manually update the authorization request before it is sent to the identity provider.
    /// </summary>
    public class PrepareAuthorizationRequestContext : BaseValidatingContext
    {
        /// <summary>
        /// Creates a new instance of the <see cref="PrepareAuthorizationRequestContext"/> class.
        /// </summary>
        public PrepareAuthorizationRequestContext(OpenIddictClientTransaction transaction)
            : base(transaction)
        {
        }

        /// <summary>
        /// Gets or sets the request.
        /// </summary>
        public OpenIddictRequest Request
        {
            get => Transaction.Request!;
            set => Transaction.Request = value;
        }

        /// <summary>
        /// Gets or sets the principal containing the claims stored in the state object.
        /// </summary>
        public ClaimsPrincipal StatePrincipal { get; set; } = new ClaimsPrincipal(new ClaimsIdentity());
    }

    /// <summary>
    /// Represents an event called for each request to the authorization endpoint
    /// to give the user code a chance to manually send the authorization request.
    /// </summary>
    public class ApplyAuthorizationRequestContext : BaseValidatingContext
    {
        /// <summary>
        /// Creates a new instance of the <see cref="PrepareAuthorizationRequestContext"/> class.
        /// </summary>
        public ApplyAuthorizationRequestContext(OpenIddictClientTransaction transaction)
            : base(transaction)
        {
        }

        /// <summary>
        /// Gets or sets the request.
        /// </summary>
        public OpenIddictRequest Request
        {
            get => Transaction.Request!;
            set => Transaction.Request = value;
        }

        public string AuthorizationEndpoint { get; set; } = null!;
    }

    /// <summary>
    /// Represents an event called for each request to the redirection endpoint to give the user code
    /// a chance to manually extract the redirection request from the ambient HTTP context.
    /// </summary>
    public class ExtractRedirectionRequestContext : BaseValidatingContext
    {
        /// <summary>
        /// Creates a new instance of the <see cref="ExtractRedirectionRequestContext"/> class.
        /// </summary>
        public ExtractRedirectionRequestContext(OpenIddictClientTransaction transaction)
            : base(transaction)
        {
        }

        /// <summary>
        /// Gets or sets the request or <see langword="null"/> if it was extracted yet.
        /// </summary>
        public OpenIddictRequest? Request
        {
            get => Transaction.Request;
            set => Transaction.Request = value;
        }
    }

    /// <summary>
    /// Represents an event called for each request to the redirection endpoint
    /// to determine if the request is valid and should continue to be processed.
    /// </summary>
    public class ValidateRedirectionRequestContext : BaseValidatingContext
    {
        /// <summary>
        /// Creates a new instance of the <see cref="ValidateRedirectionRequestContext"/> class.
        /// </summary>
        public ValidateRedirectionRequestContext(OpenIddictClientTransaction transaction)
            : base(transaction)
        {
        }

        /// <summary>
        /// Gets or sets the request.
        /// </summary>
        public OpenIddictRequest Request
        {
            get => Transaction.Request!;
            set => Transaction.Request = value;
        }

        /// <summary>
        /// Gets or sets the security principal extracted from the identity token,
        /// if applicable to the current redirection request. If no identity token
        /// is available at the validation stage, a token request will typically be
        /// sent to retrieve a complete set of tokens (e.g authorization code flow).
        /// </summary>
        public ClaimsPrincipal? Principal { get; set; }

        /// <summary>
        /// Gets or sets the security principal extracted from the state token.
        /// </summary>
        public ClaimsPrincipal? StateTokenPrincipal { get; set; }
    }

    /// <summary>
    /// Represents an event called for each validated redirection request
    /// to allow the user code to decide how the request should be handled.
    /// </summary>
    public class HandleRedirectionRequestContext : BaseValidatingTicketContext
    {
        /// <summary>
        /// Creates a new instance of the <see cref="HandleRedirectionRequestContext"/> class.
        /// </summary>
        public HandleRedirectionRequestContext(OpenIddictClientTransaction transaction)
            : base(transaction)
        {
        }

        /// <summary>
        /// Gets or sets the request.
        /// </summary>
        public OpenIddictRequest Request
        {
            get => Transaction.Request!;
            set => Transaction.Request = value;
        }

        /// <summary>
        /// Gets the additional parameters returned to the client application.
        /// </summary>
        public Dictionary<string, OpenIddictParameter> Parameters { get; private set; }
            = new(StringComparer.Ordinal);
    }

    /// <summary>
    /// Represents an event called before the redirection response is returned to the caller.
    /// </summary>
    public class ApplyRedirectionResponseContext : BaseRequestContext
    {
        /// <summary>
        /// Creates a new instance of the <see cref="ApplyRedirectionResponseContext"/> class.
        /// </summary>
        public ApplyRedirectionResponseContext(OpenIddictClientTransaction transaction)
            : base(transaction)
        {
        }

        /// <summary>
        /// Gets or sets the request, or <see langword="null"/> if it couldn't be extracted.
        /// </summary>
        public OpenIddictRequest? Request
        {
            get => Transaction.Request;
            set => Transaction.Request = value;
        }

        /// <summary>
        /// Gets or sets the response.
        /// </summary>
        public OpenIddictResponse Response
        {
            get => Transaction.Response!;
            set => Transaction.Response = value;
        }
    }
}
