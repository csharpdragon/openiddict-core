﻿/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System.ComponentModel;

namespace OpenIddict.Validation;

[EditorBrowsable(EditorBrowsableState.Advanced)]
public static class OpenIddictValidationHandlerFilters
{
    /// <summary>
    /// Represents a filter that excludes the associated handlers if no access token is extracted.
    /// </summary>
    public class RequireAccessTokenExtracted : IOpenIddictValidationHandlerFilter<ProcessAuthenticationContext>
    {
        public ValueTask<bool> IsActiveAsync(ProcessAuthenticationContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return new(context.ExtractAccessToken);
        }
    }

    /// <summary>
    /// Represents a filter that excludes the associated handlers if no access token is validated.
    /// </summary>
    public class RequireAccessTokenValidated : IOpenIddictValidationHandlerFilter<ProcessAuthenticationContext>
    {
        public ValueTask<bool> IsActiveAsync(ProcessAuthenticationContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return new(context.ValidateAccessToken);
        }
    }

    /// <summary>
    /// Represents a filter that excludes the associated handlers if authorization validation was not enabled.
    /// </summary>
    public class RequireAuthorizationEntryValidationEnabled : IOpenIddictValidationHandlerFilter<BaseContext>
    {
        public ValueTask<bool> IsActiveAsync(BaseContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return new(context.Options.EnableAuthorizationEntryValidation);
        }
    }

    /// <summary>
    /// Represents a filter that excludes the associated handlers if local validation is not used.
    /// </summary>
    public class RequireLocalValidation : IOpenIddictValidationHandlerFilter<BaseContext>
    {
        public ValueTask<bool> IsActiveAsync(BaseContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return new(context.Options.ValidationType is OpenIddictValidationType.Direct);
        }
    }

    /// <summary>
    /// Represents a filter that excludes the associated handlers if introspection is not used.
    /// </summary>
    public class RequireIntrospectionValidation : IOpenIddictValidationHandlerFilter<BaseContext>
    {
        public ValueTask<bool> IsActiveAsync(BaseContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return new(context.Options.ValidationType is OpenIddictValidationType.Introspection);
        }
    }

    /// <summary>
    /// Represents a filter that excludes the associated handlers if token validation was not enabled.
    /// </summary>
    public class RequireTokenEntryValidationEnabled : IOpenIddictValidationHandlerFilter<BaseContext>
    {
        public ValueTask<bool> IsActiveAsync(BaseContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return new(context.Options.EnableTokenEntryValidation);
        }
    }
}
