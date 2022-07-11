﻿/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenIddict.Validation;
using OpenIddict.Validation.Owin;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Exposes extensions allowing to register the OpenIddict validation services.
/// </summary>
public static class OpenIddictValidationOwinExtensions
{
    /// <summary>
    /// Registers the OpenIddict validation services for OWIN in the DI container.
    /// </summary>
    /// <param name="builder">The services builder used by OpenIddict to register new services.</param>
    /// <remarks>This extension can be safely called multiple times.</remarks>
    /// <returns>The <see cref="OpenIddictValidationOwinBuilder"/>.</returns>
    public static OpenIddictValidationOwinBuilder UseOwin(this OpenIddictValidationBuilder builder)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        // Note: unlike regular OWIN middleware, the OpenIddict validation middleware is registered
        // as a scoped service in the DI container. This allows containers that support middleware
        // resolution (like Autofac) to use it without requiring additional configuration.
        builder.Services.TryAddScoped<OpenIddictValidationOwinMiddleware>();

        // Register the built-in event handlers used by the OpenIddict OWIN validation components.
        // Note: the order used here is not important, as the actual order is set in the options.
        builder.Services.TryAdd(OpenIddictValidationOwinHandlers.DefaultHandlers.Select(descriptor => descriptor.ServiceDescriptor));

        // Register the built-in filters used by the default OpenIddict OWIN validation event handlers.
        builder.Services.TryAddSingleton<RequireOwinRequest>();

        // Register the option initializers used by the OpenIddict OWIN validation integration services.
        // Note: TryAddEnumerable() is used here to ensure the initializers are only registered once.
        builder.Services.TryAddEnumerable(new[]
        {
            ServiceDescriptor.Singleton<IConfigureOptions<OpenIddictValidationOptions>, OpenIddictValidationOwinConfiguration>()
        });

        return new OpenIddictValidationOwinBuilder(builder.Services);
    }

    /// <summary>
    /// Registers the OpenIddict validation services for OWIN in the DI container.
    /// </summary>
    /// <param name="builder">The services builder used by OpenIddict to register new services.</param>
    /// <param name="configuration">The configuration delegate used to configure the validation services.</param>
    /// <remarks>This extension can be safely called multiple times.</remarks>
    /// <returns>The <see cref="OpenIddictValidationBuilder"/>.</returns>
    public static OpenIddictValidationBuilder UseOwin(
        this OpenIddictValidationBuilder builder, Action<OpenIddictValidationOwinBuilder> configuration)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        configuration(builder.UseOwin());

        return builder;
    }
}
