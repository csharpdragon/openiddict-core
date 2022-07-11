﻿/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OpenIddict.EntityFrameworkCore.Models;
using Xunit;
using static OpenIddict.EntityFrameworkCore.OpenIddictEntityFrameworkCoreApplicationStoreResolver;

namespace OpenIddict.EntityFrameworkCore.Tests;

public class OpenIddictEntityFrameworkCoreApplicationStoreResolverTests
{
    [Fact]
    public void Get_ReturnsCustomStoreCorrespondingToTheSpecifiedTypeWhenAvailable()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IOpenIddictApplicationStore<CustomApplication>>());

        var options = Mock.Of<IOptionsMonitor<OpenIddictEntityFrameworkCoreOptions>>();
        var provider = services.BuildServiceProvider();
        var resolver = new OpenIddictEntityFrameworkCoreApplicationStoreResolver(new TypeResolutionCache(), options, provider);

        // Act and assert
        Assert.NotNull(resolver.Get<CustomApplication>());
    }

    [Fact]
    public void Get_ThrowsAnExceptionForInvalidEntityType()
    {
        // Arrange
        var services = new ServiceCollection();

        var options = Mock.Of<IOptionsMonitor<OpenIddictEntityFrameworkCoreOptions>>();
        var provider = services.BuildServiceProvider();
        var resolver = new OpenIddictEntityFrameworkCoreApplicationStoreResolver(new TypeResolutionCache(), options, provider);

        // Act and assert
        var exception = Assert.Throws<InvalidOperationException>(resolver.Get<CustomApplication>);

        Assert.Equal(SR.GetResourceString(SR.ID0252), exception.Message);
    }

    [Fact]
    public void Get_ThrowsAnExceptionWhenDbContextTypeIsNotAvailable()
    {
        // Arrange
        var services = new ServiceCollection();

        var options = Mock.Of<IOptionsMonitor<OpenIddictEntityFrameworkCoreOptions>>(
            mock => mock.CurrentValue == new OpenIddictEntityFrameworkCoreOptions
            {
                DbContextType = null
            });

        var provider = services.BuildServiceProvider();
        var resolver = new OpenIddictEntityFrameworkCoreApplicationStoreResolver(new TypeResolutionCache(), options, provider);

        // Act and assert
        var exception = Assert.Throws<InvalidOperationException>(resolver.Get<OpenIddictEntityFrameworkCoreApplication>);

        Assert.Equal(SR.GetResourceString(SR.ID0253), exception.Message);
    }

    [Fact]
    public void Get_ReturnsDefaultStoreCorrespondingToTheSpecifiedTypeWhenAvailable()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IOpenIddictApplicationStore<CustomApplication>>());
        services.AddSingleton(CreateStore());

        var options = Mock.Of<IOptionsMonitor<OpenIddictEntityFrameworkCoreOptions>>(
            mock => mock.CurrentValue == new OpenIddictEntityFrameworkCoreOptions
            {
                DbContextType = typeof(DbContext)
            });

        var provider = services.BuildServiceProvider();
        var resolver = new OpenIddictEntityFrameworkCoreApplicationStoreResolver(new TypeResolutionCache(), options, provider);

        // Act and assert
        Assert.NotNull(resolver.Get<MyApplication>());
    }

    private static OpenIddictEntityFrameworkCoreApplicationStore<MyApplication, MyAuthorization, MyToken, DbContext, long> CreateStore()
        => new Mock<OpenIddictEntityFrameworkCoreApplicationStore<MyApplication, MyAuthorization, MyToken, DbContext, long>>(
            Mock.Of<IMemoryCache>(),
            Mock.Of<DbContext>(),
            Mock.Of<IOptionsMonitor<OpenIddictEntityFrameworkCoreOptions>>()).Object;

    public class CustomApplication { }

    public class MyApplication : OpenIddictEntityFrameworkCoreApplication<long, MyAuthorization, MyToken> { }
    public class MyAuthorization : OpenIddictEntityFrameworkCoreAuthorization<long, MyApplication, MyToken> { }
    public class MyScope : OpenIddictEntityFrameworkCoreScope<long> { }
    public class MyToken : OpenIddictEntityFrameworkCoreToken<long, MyApplication, MyAuthorization> { }
}
