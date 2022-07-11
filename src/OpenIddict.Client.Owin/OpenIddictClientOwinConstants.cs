﻿/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

namespace OpenIddict.Client.Owin;

/// <summary>
/// Exposes common constants used by the OpenIddict OWIN host.
/// </summary>
public static class OpenIddictClientOwinConstants
{
    public static class Headers
    {
        public const string CacheControl = "Cache-Control";
        public const string ContentType = "Content-Type";
        public const string Expires = "Expires";
        public const string Pragma = "Pragma";
    }

    public static class Properties
    {
        public const string AuthorizationCodePrincipal = ".authorization_code_principal";
        public const string BackchannelAccessTokenPrincipal = ".backchannel_access_token_principal";
        public const string BackchannelIdentityTokenPrincipal = ".backchannel_id_token_principal";
        public const string FrontchannelAccessTokenPrincipal = ".frontchannel_access_token_principal";
        public const string FrontchannelIdentityTokenPrincipal = ".frontchannel_id_token_principal";
        public const string Issuer = ".issuer";
        public const string Error = ".error";
        public const string ErrorDescription = ".error_description";
        public const string ErrorUri = ".error_uri";
        public const string RefreshTokenPrincipal = ".refresh_token_principal";
        public const string StateTokenPrincipal = ".state_token_principal";
        public const string UserinfoTokenPrincipal = ".userinfo_token_principal";
    }

    public static class PropertyTypes
    {
        public const string Boolean = "#boolean";
        public const string Integer = "#integer";
        public const string Json = "#json";
        public const string String = "#string";
    }

    public static class Tokens
    {
        public const string AuthorizationCode = "authorization_code";
        public const string BackchannelAccessToken = "backchannel_access_token";
        public const string BackchannelIdentityToken = "backchannel_id_token";
        public const string FrontchannelAccessToken = "frontchannel_access_token";
        public const string FrontchannelIdentityToken = "frontchannel_id_token";
        public const string RefreshToken = "refresh_token";
        public const string StateToken = "state_token";
        public const string UserinfoToken = "userinfo_token";
    }
}
