﻿/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System.Collections.Immutable;
using System.Diagnostics;

namespace OpenIddict.MongoDb.Models;

/// <summary>
/// Represents an OpenIddict authorization.
/// </summary>
[DebuggerDisplay("Id = {Id.ToString(),nq} ; Subject = {Subject,nq} ; Type = {Type,nq} ; Status = {Status,nq}")]
public class OpenIddictMongoDbAuthorization
{
    /// <summary>
    /// Gets or sets the identifier of the application associated with the current authorization.
    /// </summary>
    [BsonElement("application_id"), BsonIgnoreIfDefault]
    public virtual ObjectId ApplicationId { get; set; }

    /// <summary>
    /// Gets or sets the concurrency token.
    /// </summary>
    [BsonElement("concurrency_token"), BsonIgnoreIfNull]
    public virtual string? ConcurrencyToken { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the UTC creation date of the current authorization.
    /// </summary>
    public virtual DateTime? CreationDate { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier associated with the current authorization.
    /// </summary>
    [BsonId, BsonRequired]
    public virtual ObjectId Id { get; set; }

    /// <summary>
    /// Gets or sets the additional properties associated with the current authorization.
    /// </summary>
    [BsonElement("properties"), BsonIgnoreIfNull]
    public virtual BsonDocument? Properties { get; set; }

    /// <summary>
    /// Gets or sets the scopes associated with the current authorization.
    /// </summary>
    [BsonElement("scopes"), BsonIgnoreIfDefault]
    public virtual IReadOnlyList<string> Scopes { get; set; } = ImmutableList.Create<string>();

    /// <summary>
    /// Gets or sets the status of the current authorization.
    /// </summary>
    [BsonElement("status"), BsonIgnoreIfNull]
    public virtual string? Status { get; set; }

    /// <summary>
    /// Gets or sets the subject associated with the current authorization.
    /// </summary>
    [BsonElement("subject"), BsonIgnoreIfNull]
    public virtual string? Subject { get; set; }

    /// <summary>
    /// Gets or sets the type of the current authorization.
    /// </summary>
    [BsonElement("type"), BsonIgnoreIfNull]
    public virtual string? Type { get; set; }
}
