﻿/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System.Security.Claims;
using System.Text.Json;
using Properties = OpenIddict.Validation.DataProtection.OpenIddictValidationDataProtectionConstants.Properties;

namespace OpenIddict.Validation.DataProtection;

public class OpenIddictValidationDataProtectionFormatter : IOpenIddictValidationDataProtectionFormatter
{
    public ClaimsPrincipal ReadToken(BinaryReader reader)
    {
        if (reader is null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        var (principal, properties) = Read(reader);

        // Tokens serialized using the ASP.NET Core Data Protection stack are compound
        // of both claims and special authentication properties. To ensure existing tokens
        // can be reused, well-known properties are manually mapped to their claims equivalents.

        return principal
            .SetClaims(Claims.Private.Audience,  GetJsonProperty(properties, Properties.Audiences))
            .SetClaims(Claims.Private.Presenter, GetJsonProperty(properties, Properties.Presenters))
            .SetClaims(Claims.Private.Resource,  GetJsonProperty(properties, Properties.Resources))
            .SetClaims(Claims.Private.Scope,     GetJsonProperty(properties, Properties.Scopes))

            .SetClaim(Claims.Private.HostProperties, GetJsonProperty(properties, Properties.HostProperties))

            .SetClaim(Claims.Private.AccessTokenLifetime,       GetProperty(properties, Properties.AccessTokenLifetime))
            .SetClaim(Claims.Private.AuthorizationCodeLifetime, GetProperty(properties, Properties.AuthorizationCodeLifetime))
            .SetClaim(Claims.Private.AuthorizationId,           GetProperty(properties, Properties.InternalAuthorizationId))
            .SetClaim(Claims.Private.CodeChallenge,             GetProperty(properties, Properties.CodeChallenge))
            .SetClaim(Claims.Private.CodeChallengeMethod,       GetProperty(properties, Properties.CodeChallengeMethod))
            .SetClaim(Claims.Private.CreationDate,              GetProperty(properties, Properties.Issued))
            .SetClaim(Claims.Private.DeviceCodeId,              GetProperty(properties, Properties.DeviceCodeId))
            .SetClaim(Claims.Private.DeviceCodeLifetime,        GetProperty(properties, Properties.DeviceCodeLifetime))
            .SetClaim(Claims.Private.IdentityTokenLifetime,     GetProperty(properties, Properties.IdentityTokenLifetime))
            .SetClaim(Claims.Private.ExpirationDate,            GetProperty(properties, Properties.Expires))
            .SetClaim(Claims.Private.Nonce,                     GetProperty(properties, Properties.Nonce))
            .SetClaim(Claims.Private.RedirectUri,               GetProperty(properties, Properties.OriginalRedirectUri))
            .SetClaim(Claims.Private.RefreshTokenLifetime,      GetProperty(properties, Properties.RefreshTokenLifetime))
            .SetClaim(Claims.Private.TokenId,                   GetProperty(properties, Properties.InternalTokenId))
            .SetClaim(Claims.Private.UserCodeLifetime,          GetProperty(properties, Properties.UserCodeLifetime));

        static (ClaimsPrincipal principal, IReadOnlyDictionary<string, string> properties) Read(BinaryReader reader)
        {
            // Read the version of the format used to serialize the ticket.
            var version = reader.ReadInt32();
            if (version != 5)
            {
                throw new InvalidOperationException(SR.GetResourceString(SR.ID0287));
            }

            // Read the authentication scheme associated to the ticket.
            _ = reader.ReadString();

            // Read the number of identities stored in the serialized payload.
            var count = reader.ReadInt32();

            var identities = new ClaimsIdentity[count];
            for (var index = 0; index != count; ++index)
            {
                identities[index] = ReadIdentity(reader);
            }

            var properties = ReadProperties(reader);

            return (new ClaimsPrincipal(identities), properties);
        }

        static ClaimsIdentity ReadIdentity(BinaryReader reader)
        {
            var identity = new ClaimsIdentity(
                authenticationType: reader.ReadString(),
                nameType: ReadWithDefault(reader, ClaimsIdentity.DefaultNameClaimType),
                roleType: ReadWithDefault(reader, ClaimsIdentity.DefaultRoleClaimType));

            // Read the number of claims contained in the serialized identity.
            var count = reader.ReadInt32();

            for (int index = 0; index != count; ++index)
            {
                var claim = ReadClaim(reader, identity);

                identity.AddClaim(claim);
            }

            // Determine whether the identity has a bootstrap context attached.
            if (reader.ReadBoolean())
            {
                identity.BootstrapContext = reader.ReadString();
            }

            // Determine whether the identity has an actor identity attached.
            if (reader.ReadBoolean())
            {
                identity.Actor = ReadIdentity(reader);
            }

            return identity;
        }

        static Claim ReadClaim(BinaryReader reader, ClaimsIdentity identity)
        {
            var type = ReadWithDefault(reader, identity.NameClaimType);
            var value = reader.ReadString();
            var valueType = ReadWithDefault(reader, ClaimValueTypes.String);
            var issuer = ReadWithDefault(reader, ClaimsIdentity.DefaultIssuer);
            var originalIssuer = ReadWithDefault(reader, issuer);

            var claim = new Claim(type, value, valueType, issuer, originalIssuer, identity);

            // Read the number of properties stored in the claim.
            var count = reader.ReadInt32();

            for (var index = 0; index != count; ++index)
            {
                var key = reader.ReadString();
                var propertyValue = reader.ReadString();

                claim.Properties.Add(key, propertyValue);
            }

            return claim;
        }

        static IReadOnlyDictionary<string, string> ReadProperties(BinaryReader reader)
        {
            // Read the version of the format used to serialize the properties.
            var version = reader.ReadInt32();
            if (version != 1)
            {
                throw new InvalidOperationException(SR.GetResourceString(SR.ID0287));
            }

            var count = reader.ReadInt32();
            var properties = new Dictionary<string, string>(count, StringComparer.Ordinal);
            for (var index = 0; index != count; ++index)
            {
                properties.Add(reader.ReadString(), reader.ReadString());
            }

            return properties;
        }

        static string ReadWithDefault(BinaryReader reader, string defaultValue)
        {
            var value = reader.ReadString();

            if (string.Equals(value, "\0", StringComparison.Ordinal))
            {
                return defaultValue;
            }

            return value;
        }

        static string? GetProperty(IReadOnlyDictionary<string, string> properties, string name)
            => properties.TryGetValue(name, out var value) ? value : null;

        static JsonElement GetJsonProperty(IReadOnlyDictionary<string, string> properties, string name)
        {
            if (properties.TryGetValue(name, out var value))
            {
                using var document = JsonDocument.Parse(value);
                return document.RootElement.Clone();
            }

            return default;
        }
    }
}
