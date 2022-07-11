﻿/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OpenIddict.MongoDb.Models;

namespace OpenIddict.MongoDb;

/// <summary>
/// Provides methods allowing to manage the authorizations stored in a database.
/// </summary>
/// <typeparam name="TAuthorization">The type of the Authorization entity.</typeparam>
public class OpenIddictMongoDbAuthorizationStore<TAuthorization> : IOpenIddictAuthorizationStore<TAuthorization>
    where TAuthorization : OpenIddictMongoDbAuthorization
{
    public OpenIddictMongoDbAuthorizationStore(
        IOpenIddictMongoDbContext context,
        IOptionsMonitor<OpenIddictMongoDbOptions> options)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Gets the database context associated with the current store.
    /// </summary>
    protected IOpenIddictMongoDbContext Context { get; }

    /// <summary>
    /// Gets the options associated with the current store.
    /// </summary>
    protected IOptionsMonitor<OpenIddictMongoDbOptions> Options { get; }

    /// <inheritdoc/>
    public virtual async ValueTask<long> CountAsync(CancellationToken cancellationToken)
    {
        var database = await Context.GetDatabaseAsync(cancellationToken);
        var collection = database.GetCollection<TAuthorization>(Options.CurrentValue.AuthorizationsCollectionName);

        return await collection.CountDocumentsAsync(FilterDefinition<TAuthorization>.Empty, null, cancellationToken);
    }

    /// <inheritdoc/>
    public virtual async ValueTask<long> CountAsync<TResult>(
        Func<IQueryable<TAuthorization>, IQueryable<TResult>> query, CancellationToken cancellationToken)
    {
        if (query is null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        var database = await Context.GetDatabaseAsync(cancellationToken);
        var collection = database.GetCollection<TAuthorization>(Options.CurrentValue.AuthorizationsCollectionName);

        return await ((IMongoQueryable<TAuthorization>) query(collection.AsQueryable())).LongCountAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public virtual async ValueTask CreateAsync(TAuthorization authorization, CancellationToken cancellationToken)
    {
        if (authorization is null)
        {
            throw new ArgumentNullException(nameof(authorization));
        }

        var database = await Context.GetDatabaseAsync(cancellationToken);
        var collection = database.GetCollection<TAuthorization>(Options.CurrentValue.AuthorizationsCollectionName);

        await collection.InsertOneAsync(authorization, null, cancellationToken);
    }

    /// <inheritdoc/>
    public virtual async ValueTask DeleteAsync(TAuthorization authorization, CancellationToken cancellationToken)
    {
        if (authorization is null)
        {
            throw new ArgumentNullException(nameof(authorization));
        }

        var database = await Context.GetDatabaseAsync(cancellationToken);
        var collection = database.GetCollection<TAuthorization>(Options.CurrentValue.AuthorizationsCollectionName);

        if ((await collection.DeleteOneAsync(entity =>
            entity.Id == authorization.Id &&
            entity.ConcurrencyToken == authorization.ConcurrencyToken, cancellationToken)).DeletedCount == 0)
        {
            throw new OpenIddictExceptions.ConcurrencyException(SR.GetResourceString(SR.ID0241));
        }

        // Delete the tokens associated with the authorization.
        await database.GetCollection<OpenIddictMongoDbToken>(Options.CurrentValue.TokensCollectionName)
            .DeleteManyAsync(token => token.AuthorizationId == authorization.Id, cancellationToken);
    }

    /// <inheritdoc/>
    public virtual IAsyncEnumerable<TAuthorization> FindAsync(
        string subject, string client, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(subject))
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0198), nameof(subject));
        }

        if (string.IsNullOrEmpty(client))
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0124), nameof(client));
        }

        return ExecuteAsync(cancellationToken);

        async IAsyncEnumerable<TAuthorization> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var database = await Context.GetDatabaseAsync(cancellationToken);
            var collection = database.GetCollection<TAuthorization>(Options.CurrentValue.AuthorizationsCollectionName);

            await foreach (var authorization in collection.Find(authorization =>
                authorization.Subject == subject &&
                authorization.ApplicationId == ObjectId.Parse(client)).ToAsyncEnumerable(cancellationToken))
            {
                yield return authorization;
            }
        }
    }

    /// <inheritdoc/>
    public virtual IAsyncEnumerable<TAuthorization> FindAsync(
        string subject, string client,
        string status, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(subject))
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0198), nameof(subject));
        }

        if (string.IsNullOrEmpty(client))
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0124), nameof(client));
        }

        if (string.IsNullOrEmpty(status))
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0199), nameof(status));
        }

        return ExecuteAsync(cancellationToken);

        async IAsyncEnumerable<TAuthorization> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var database = await Context.GetDatabaseAsync(cancellationToken);
            var collection = database.GetCollection<TAuthorization>(Options.CurrentValue.AuthorizationsCollectionName);

            await foreach (var authorization in collection.Find(authorization =>
                authorization.Subject == subject &&
                authorization.ApplicationId == ObjectId.Parse(client) &&
                authorization.Status == status).ToAsyncEnumerable(cancellationToken))
            {
                yield return authorization;
            }
        }
    }

    /// <inheritdoc/>
    public virtual IAsyncEnumerable<TAuthorization> FindAsync(
        string subject, string client,
        string status, string type, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(subject))
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0198), nameof(subject));
        }

        if (string.IsNullOrEmpty(client))
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0124), nameof(client));
        }

        if (string.IsNullOrEmpty(status))
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0199), nameof(status));
        }

        if (string.IsNullOrEmpty(type))
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0200), nameof(type));
        }

        return ExecuteAsync(cancellationToken);

        async IAsyncEnumerable<TAuthorization> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var database = await Context.GetDatabaseAsync(cancellationToken);
            var collection = database.GetCollection<TAuthorization>(Options.CurrentValue.AuthorizationsCollectionName);

            await foreach (var authorization in collection.Find(authorization =>
                authorization.Subject == subject &&
                authorization.ApplicationId == ObjectId.Parse(client) &&
                authorization.Status == status &&
                authorization.Type == type).ToAsyncEnumerable(cancellationToken))
            {
                yield return authorization;
            }
        }
    }

    /// <inheritdoc/>
    public virtual IAsyncEnumerable<TAuthorization> FindAsync(
        string subject, string client,
        string status, string type,
        ImmutableArray<string> scopes, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(subject))
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0198), nameof(subject));
        }

        if (string.IsNullOrEmpty(client))
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0124), nameof(client));
        }

        if (string.IsNullOrEmpty(status))
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0199), nameof(status));
        }

        if (string.IsNullOrEmpty(type))
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0200), nameof(type));
        }

        return ExecuteAsync(cancellationToken);

        async IAsyncEnumerable<TAuthorization> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var database = await Context.GetDatabaseAsync(cancellationToken);
            var collection = database.GetCollection<TAuthorization>(Options.CurrentValue.AuthorizationsCollectionName);

            // Note: Enumerable.All() is deliberately used without the extension method syntax to ensure
            // ImmutableArrayExtensions.All() (which is not supported by MongoDB) is not used instead.
            await foreach (var authorization in collection.Find(authorization =>
                authorization.Subject == subject &&
                authorization.ApplicationId == ObjectId.Parse(client) &&
                authorization.Status == status &&
                authorization.Type == type &&
                Enumerable.All(scopes, scope => authorization.Scopes.Contains(scope))).ToAsyncEnumerable(cancellationToken))
            {
                yield return authorization;
            }
        }
    }

    /// <inheritdoc/>
    public virtual IAsyncEnumerable<TAuthorization> FindByApplicationIdAsync(
        string identifier, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0195), nameof(identifier));
        }

        return ExecuteAsync(cancellationToken);

        async IAsyncEnumerable<TAuthorization> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var database = await Context.GetDatabaseAsync(cancellationToken);
            var collection = database.GetCollection<TAuthorization>(Options.CurrentValue.AuthorizationsCollectionName);

            await foreach (var authorization in collection.Find(authorization =>
                authorization.ApplicationId == ObjectId.Parse(identifier)).ToAsyncEnumerable(cancellationToken))
            {
                yield return authorization;
            }
        }
    }

    /// <inheritdoc/>
    public virtual async ValueTask<TAuthorization?> FindByIdAsync(string identifier, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0195), nameof(identifier));
        }

        var database = await Context.GetDatabaseAsync(cancellationToken);
        var collection = database.GetCollection<TAuthorization>(Options.CurrentValue.AuthorizationsCollectionName);

        return await collection.Find(authorization => authorization.Id == ObjectId.Parse(identifier))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public virtual IAsyncEnumerable<TAuthorization> FindBySubjectAsync(
        string subject, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(subject))
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0198), nameof(subject));
        }

        return ExecuteAsync(cancellationToken);

        async IAsyncEnumerable<TAuthorization> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var database = await Context.GetDatabaseAsync(cancellationToken);
            var collection = database.GetCollection<TAuthorization>(Options.CurrentValue.AuthorizationsCollectionName);

            await foreach (var authorization in collection.Find(authorization =>
                authorization.Subject == subject).ToAsyncEnumerable(cancellationToken))
            {
                yield return authorization;
            }
        }
    }

    /// <inheritdoc/>
    public virtual ValueTask<string?> GetApplicationIdAsync(TAuthorization authorization, CancellationToken cancellationToken)
    {
        if (authorization is null)
        {
            throw new ArgumentNullException(nameof(authorization));
        }

        if (authorization.ApplicationId == ObjectId.Empty)
        {
            return new(result: null);
        }

        return new(authorization.ApplicationId.ToString());
    }

    /// <inheritdoc/>
    public virtual async ValueTask<TResult?> GetAsync<TState, TResult>(
        Func<IQueryable<TAuthorization>, TState, IQueryable<TResult>> query,
        TState state, CancellationToken cancellationToken)
    {
        if (query is null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        var database = await Context.GetDatabaseAsync(cancellationToken);
        var collection = database.GetCollection<TAuthorization>(Options.CurrentValue.AuthorizationsCollectionName);

        return await ((IMongoQueryable<TResult>) query(collection.AsQueryable(), state)).FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public virtual ValueTask<DateTimeOffset?> GetCreationDateAsync(TAuthorization authorization, CancellationToken cancellationToken)
    {
        if (authorization is null)
        {
            throw new ArgumentNullException(nameof(authorization));
        }

        if (authorization.CreationDate is null)
        {
            return new(result: null);
        }

        return new(DateTime.SpecifyKind(authorization.CreationDate.Value, DateTimeKind.Utc));
    }

    /// <inheritdoc/>
    public virtual ValueTask<string?> GetIdAsync(TAuthorization authorization, CancellationToken cancellationToken)
    {
        if (authorization is null)
        {
            throw new ArgumentNullException(nameof(authorization));
        }

        return new(authorization.Id.ToString());
    }

    /// <inheritdoc/>
    public virtual ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(TAuthorization authorization, CancellationToken cancellationToken)
    {
        if (authorization is null)
        {
            throw new ArgumentNullException(nameof(authorization));
        }

        if (authorization.Properties is null)
        {
            return new(ImmutableDictionary.Create<string, JsonElement>());
        }

        using var document = JsonDocument.Parse(authorization.Properties.ToJson());
        var builder = ImmutableDictionary.CreateBuilder<string, JsonElement>();

        foreach (var property in document.RootElement.EnumerateObject())
        {
            builder[property.Name] = property.Value.Clone();
        }

        return new(builder.ToImmutable());
    }

    /// <inheritdoc/>
    public virtual ValueTask<ImmutableArray<string>> GetScopesAsync(TAuthorization authorization, CancellationToken cancellationToken)
    {
        if (authorization is null)
        {
            throw new ArgumentNullException(nameof(authorization));
        }

        if (authorization.Scopes is null || authorization.Scopes.Count == 0)
        {
            return new(ImmutableArray.Create<string>());
        }

        return new(authorization.Scopes.ToImmutableArray());
    }

    /// <inheritdoc/>
    public virtual ValueTask<string?> GetStatusAsync(TAuthorization authorization, CancellationToken cancellationToken)
    {
        if (authorization is null)
        {
            throw new ArgumentNullException(nameof(authorization));
        }

        return new(authorization.Status);
    }

    /// <inheritdoc/>
    public virtual ValueTask<string?> GetSubjectAsync(TAuthorization authorization, CancellationToken cancellationToken)
    {
        if (authorization is null)
        {
            throw new ArgumentNullException(nameof(authorization));
        }

        return new(authorization.Subject);
    }

    /// <inheritdoc/>
    public virtual ValueTask<string?> GetTypeAsync(TAuthorization authorization, CancellationToken cancellationToken)
    {
        if (authorization is null)
        {
            throw new ArgumentNullException(nameof(authorization));
        }

        return new(authorization.Type);
    }

    /// <inheritdoc/>
    public virtual ValueTask<TAuthorization> InstantiateAsync(CancellationToken cancellationToken)
    {
        try
        {
            return new(Activator.CreateInstance<TAuthorization>());
        }

        catch (MemberAccessException exception)
        {
            return new(Task.FromException<TAuthorization>(
                new InvalidOperationException(SR.GetResourceString(SR.ID0242), exception)));
        }
    }

    /// <inheritdoc/>
    public virtual async IAsyncEnumerable<TAuthorization> ListAsync(
        int? count, int? offset, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var database = await Context.GetDatabaseAsync(cancellationToken);
        var collection = database.GetCollection<TAuthorization>(Options.CurrentValue.AuthorizationsCollectionName);

        var query = (IMongoQueryable<TAuthorization>) collection.AsQueryable().OrderBy(authorization => authorization.Id);

        if (offset.HasValue)
        {
            query = query.Skip(offset.Value);
        }

        if (count.HasValue)
        {
            query = query.Take(count.Value);
        }

        await foreach (var authorization in ((IAsyncCursorSource<TAuthorization>) query).ToAsyncEnumerable(cancellationToken))
        {
            yield return authorization;
        }
    }

    /// <inheritdoc/>
    public virtual IAsyncEnumerable<TResult> ListAsync<TState, TResult>(
        Func<IQueryable<TAuthorization>, TState, IQueryable<TResult>> query,
        TState state, CancellationToken cancellationToken)
    {
        if (query is null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        return ExecuteAsync(cancellationToken);

        async IAsyncEnumerable<TResult> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var database = await Context.GetDatabaseAsync(cancellationToken);
            var collection = database.GetCollection<TAuthorization>(Options.CurrentValue.AuthorizationsCollectionName);

            await foreach (var element in query(collection.AsQueryable(), state).ToAsyncEnumerable(cancellationToken))
            {
                yield return element;
            }
        }
    }

    /// <inheritdoc/>
    public virtual async ValueTask PruneAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
    {
        var database = await Context.GetDatabaseAsync(cancellationToken);
        var collection = database.GetCollection<TAuthorization>(Options.CurrentValue.AuthorizationsCollectionName);

        // Note: directly deleting the resulting set of an aggregate query is not supported by MongoDB.
        // To work around this limitation, the authorization identifiers are stored in an intermediate
        // list and delete requests are sent to remove the documents corresponding to these identifiers.

        var identifiers =
            await (from authorization in collection.AsQueryable()
                   join token in database.GetCollection<OpenIddictMongoDbToken>(Options.CurrentValue.TokensCollectionName).AsQueryable()
                              on authorization.Id equals token.AuthorizationId into tokens
                   where authorization.CreationDate < threshold.UtcDateTime
                   where authorization.Status != Statuses.Valid ||
                        (authorization.Type == AuthorizationTypes.AdHoc && !tokens.Any())
                   select authorization.Id).ToListAsync(cancellationToken);

        // Note: to avoid generating delete requests with very large filters, a buffer is used here and the
        // maximum number of elements that can be removed by a single call to PruneAsync() is deliberately limited.
        foreach (var buffer in Buffer(identifiers.Take(1_000_000), 1_000))
        {
            await collection.DeleteManyAsync(authorization => buffer.Contains(authorization.Id), cancellationToken);
        }

        static IEnumerable<List<TSource>> Buffer<TSource>(IEnumerable<TSource> source, int count)
        {
            List<TSource>? buffer = null;

            foreach (var element in source)
            {
                buffer ??= new List<TSource>(capacity: 1);
                buffer.Add(element);

                if (buffer.Count == count)
                {
                    yield return buffer;

                    buffer = null;
                }
            }

            if (buffer is not null)
            {
                yield return buffer;
            }
        }
    }

    /// <inheritdoc/>
    public virtual ValueTask SetApplicationIdAsync(TAuthorization authorization,
        string? identifier, CancellationToken cancellationToken)
    {
        if (authorization is null)
        {
            throw new ArgumentNullException(nameof(authorization));
        }

        if (!string.IsNullOrEmpty(identifier))
        {
            authorization.ApplicationId = ObjectId.Parse(identifier);
        }

        else
        {
            authorization.ApplicationId = ObjectId.Empty;
        }

        return default;
    }

    /// <inheritdoc/>
    public virtual ValueTask SetCreationDateAsync(TAuthorization authorization,
        DateTimeOffset? date, CancellationToken cancellationToken)
    {
        if (authorization is null)
        {
            throw new ArgumentNullException(nameof(authorization));
        }

        authorization.CreationDate = date?.UtcDateTime;

        return default;
    }

    /// <inheritdoc/>
    public virtual ValueTask SetPropertiesAsync(TAuthorization authorization,
        ImmutableDictionary<string, JsonElement> properties, CancellationToken cancellationToken)
    {
        if (authorization is null)
        {
            throw new ArgumentNullException(nameof(authorization));
        }

        if (properties is not { Count: > 0 })
        {
            authorization.Properties = null;

            return default;
        }

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Indented = false
        });

        writer.WriteStartObject();

        foreach (var property in properties)
        {
            writer.WritePropertyName(property.Key);
            property.Value.WriteTo(writer);
        }

        writer.WriteEndObject();
        writer.Flush();

        authorization.Properties = BsonDocument.Parse(Encoding.UTF8.GetString(stream.ToArray()));

        return default;
    }

    /// <inheritdoc/>
    public virtual ValueTask SetScopesAsync(TAuthorization authorization,
        ImmutableArray<string> scopes, CancellationToken cancellationToken)
    {
        if (authorization is null)
        {
            throw new ArgumentNullException(nameof(authorization));
        }

        if (scopes.IsDefaultOrEmpty)
        {
            authorization.Scopes = ImmutableList.Create<string>();

            return default;
        }

        authorization.Scopes = scopes.ToImmutableList();

        return default;
    }

    /// <inheritdoc/>
    public virtual ValueTask SetStatusAsync(TAuthorization authorization, string? status, CancellationToken cancellationToken)
    {
        if (authorization is null)
        {
            throw new ArgumentNullException(nameof(authorization));
        }

        authorization.Status = status;

        return default;
    }

    /// <inheritdoc/>
    public virtual ValueTask SetSubjectAsync(TAuthorization authorization, string? subject, CancellationToken cancellationToken)
    {
        if (authorization is null)
        {
            throw new ArgumentNullException(nameof(authorization));
        }

        authorization.Subject = subject;

        return default;
    }

    /// <inheritdoc/>
    public virtual ValueTask SetTypeAsync(TAuthorization authorization, string? type, CancellationToken cancellationToken)
    {
        if (authorization is null)
        {
            throw new ArgumentNullException(nameof(authorization));
        }

        authorization.Type = type;

        return default;
    }

    /// <inheritdoc/>
    public virtual async ValueTask UpdateAsync(TAuthorization authorization, CancellationToken cancellationToken)
    {
        if (authorization is null)
        {
            throw new ArgumentNullException(nameof(authorization));
        }

        // Generate a new concurrency token and attach it
        // to the authorization before persisting the changes.
        var timestamp = authorization.ConcurrencyToken;
        authorization.ConcurrencyToken = Guid.NewGuid().ToString();

        var database = await Context.GetDatabaseAsync(cancellationToken);
        var collection = database.GetCollection<TAuthorization>(Options.CurrentValue.AuthorizationsCollectionName);

        if ((await collection.ReplaceOneAsync(entity =>
            entity.Id == authorization.Id &&
            entity.ConcurrencyToken == timestamp, authorization, null as ReplaceOptions, cancellationToken)).MatchedCount == 0)
        {
            throw new OpenIddictExceptions.ConcurrencyException(SR.GetResourceString(SR.ID0241));
        }
    }
}
