﻿/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System.ComponentModel;
using OpenIddict.Client.Owin;
using Owin;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Exposes the necessary methods required to configure
/// the OpenIddict client OWIN/Katana integration.
/// </summary>
public class OpenIddictClientOwinBuilder
{
    /// <summary>
    /// Initializes a new instance of <see cref="OpenIddictClientOwinBuilder"/>.
    /// </summary>
    /// <param name="services">The services collection.</param>
    public OpenIddictClientOwinBuilder(IServiceCollection services)
        => Services = services ?? throw new ArgumentNullException(nameof(services));

    /// <summary>
    /// Gets the services collection.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public IServiceCollection Services { get; }

    /// <summary>
    /// Amends the default OpenIddict client OWIN/Katana configuration.
    /// </summary>
    /// <param name="configuration">The delegate used to configure the OpenIddict options.</param>
    /// <remarks>This extension can be safely called multiple times.</remarks>
    /// <returns>The <see cref="OpenIddictClientOwinBuilder"/>.</returns>
    public OpenIddictClientOwinBuilder Configure(Action<OpenIddictClientOwinOptions> configuration)
    {
        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        Services.Configure(configuration);

        return this;
    }

    /// <summary>
    /// Enables the pass-through mode for the OpenID Connect redirection endpoint.
    /// When the pass-through mode is used, OpenID Connect requests are initially handled by OpenIddict.
    /// Once validated, the rest of the request processing pipeline is invoked, so that OpenID Connect requests
    /// can be handled at a later stage (in a custom middleware or in a MVC controller, for instance).
    /// </summary>
    /// <returns>The <see cref="OpenIddictClientOwinBuilder"/>.</returns>
    public OpenIddictClientOwinBuilder EnableRedirectionEndpointPassthrough()
        => Configure(options => options.EnableRedirectionEndpointPassthrough = true);

    /// <summary>
    /// Enables error pass-through support, so that the rest of the request processing pipeline is
    /// automatically invoked when returning an error from the interactive authorization and logout endpoints.
    /// When this option is enabled, special logic must be added to these actions to handle errors, that can be
    /// retrieved using <see cref="OpenIddictClientOwinHelpers.GetOpenIddictClientResponse(IOwinContext)"/>.
    /// </summary>
    /// <remarks>
    /// Important: the error pass-through mode cannot be used when the status code pages integration is enabled.
    /// </remarks>
    /// <returns>The <see cref="OpenIddictClientOwinBuilder"/>.</returns>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public OpenIddictClientOwinBuilder EnableErrorPassthrough()
        => Configure(options => options.EnableErrorPassthrough = true);

    /// <summary>
    /// Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns><see langword="true"/> if the specified object is equal to the current object; otherwise, false.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override bool Equals(object? obj) => base.Equals(obj);

    /// <summary>
    /// Serves as the default hash function.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override int GetHashCode() => base.GetHashCode();

    /// <summary>
    /// Returns a string that represents the current object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override string? ToString() => base.ToString();
}
