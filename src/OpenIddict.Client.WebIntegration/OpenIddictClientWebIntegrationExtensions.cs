﻿/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenIddict.Client;
using OpenIddict.Client.WebIntegration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Exposes extensions allowing to register the OpenIddict client Web integration services.
/// </summary>
public static class OpenIddictClientWebIntegrationExtensions
{
    /// <summary>
    /// Registers the OpenIddict client Web integration services in the DI container.
    /// </summary>
    /// <param name="builder">The services builder used by OpenIddict to register new services.</param>
    /// <remarks>This extension can be safely called multiple times.</remarks>
    /// <returns>The <see cref="OpenIddictClientWebIntegrationBuilder"/>.</returns>
    public static OpenIddictClientWebIntegrationBuilder UseWebProviders(this OpenIddictClientBuilder builder)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        // Register the System.Net.Http integration.
        builder.UseSystemNetHttp();

        // Register the built-in event handlers used by the OpenIddict client Web components.
        // Note: the order used here is not important, as the actual order is set in the options.
        builder.Services.TryAdd(OpenIddictClientWebIntegrationHandlers.DefaultHandlers
            .Select(descriptor => descriptor.ServiceDescriptor));

        // Note: TryAddEnumerable() is used here to ensure the initializers are registered only once.
        builder.Services.TryAddEnumerable(new[]
        {
            ServiceDescriptor.Singleton<IConfigureOptions<OpenIddictClientOptions>, OpenIddictClientWebIntegrationConfiguration>()
        });

        return new OpenIddictClientWebIntegrationBuilder(builder.Services);
    }

    /// <summary>
    /// Registers the OpenIddict client Web integration services in the DI container.
    /// </summary>
    /// <param name="builder">The services builder used by OpenIddict to register new services.</param>
    /// <param name="configuration">The configuration delegate used to configure the validation services.</param>
    /// <remarks>This extension can be safely called multiple times.</remarks>
    /// <returns>The <see cref="OpenIddictClientBuilder"/>.</returns>
    public static OpenIddictClientBuilder UseWebProviders(
        this OpenIddictClientBuilder builder, Action<OpenIddictClientWebIntegrationBuilder> configuration)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        configuration(builder.UseWebProviders());

        return builder;
    }
}
