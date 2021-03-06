﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Web;
using DotNetOpenAuth.AspNet;
using DotNetOpenAuth.AspNet.Clients;

namespace PromoRepublic.Oauth.Clients
{
    /// <summary>
    /// The facebook client.
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Facebook", Justification = "Brand name")]
    public sealed class FbClient : OAuth2Client
    {
        #region Constants and Fields

        /// <summary>
        /// The authorization endpoint.
        /// </summary>
        private const string AuthorizationEndpoint = "https://www.facebook.com/dialog/oauth";

        /// <summary>
        /// The token endpoint.
        /// </summary>
        private const string TokenEndpoint = "https://graph.facebook.com/oauth/access_token";

        /// <summary>
        /// The _app id.
        /// </summary>
        private readonly string _appId;

        /// <summary>
        /// The _app secret.
        /// </summary>
        private readonly string _appSecret;

        private readonly List<string> _scopes = new List<string>(new []{"email"});

        #endregion

        #region Properties
        public List<string> Scopes { get { return _scopes; } }
        #endregion

        #region Constructors and Destructors

        /// <summary>
        /// Initializes a new instance of the <see cref="FacebookClient"/> class.
        /// </summary>
        /// <param name="appId">
        /// The app id.
        /// </param>
        /// <param name="appSecret">
        /// The app secret.
        /// </param>
        public FbClient(string appId, string appSecret)
            : base("facebook")
        {
            if (String.IsNullOrEmpty(appId)) throw new ArgumentNullException("appId");
            if (String.IsNullOrEmpty(appSecret)) throw new ArgumentNullException("appSecret");

            _appId = appId;
            _appSecret = appSecret;
        }
        
        public FbClient(string appId, string appSecret, IEnumerable<string> extraScopes)
            : this(appId, appSecret)
        {
            if (extraScopes != null)
            {
                Scopes.AddRange(extraScopes);
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// The get service login url.
        /// </summary>
        /// <param name="returnUrl">
        /// The return url.
        /// </param>
        /// <returns>An absolute URI.</returns>
        protected override Uri GetServiceLoginUrl(Uri returnUrl)
        {
            // Note: Facebook doesn't like us to url-encode the redirect_uri value
            var builder = new UriBuilder(AuthorizationEndpoint);
            builder.AppendQueryArgs(
                new Dictionary<string, string> {
					{ "client_id", _appId },
					{ "redirect_uri", returnUrl.AbsoluteUri },
					{ "scope", String.Join(",", Scopes) },
				});
            return builder.Uri;
        }

        /// <summary>
        /// The get user data.
        /// </summary>
        /// <param name="accessToken">
        /// The access token.
        /// </param>
        /// <returns>A dictionary of profile data.</returns>
        protected override IDictionary<string, string> GetUserData(string accessToken)
        {
            var graphData = new FacebookGraphData();
            var request =
                WebRequest.Create(
                    "https://graph.facebook.com/me?access_token=" + accessToken);
            
            using (var response = request.GetResponse())
            {
                using (var responseStream = response.GetResponseStream())
                {
                    var serializer = new DataContractJsonSerializer(typeof (FacebookGraphData));

                    if (responseStream != null)
                        graphData = (FacebookGraphData) serializer.ReadObject(responseStream);
                }
            }

            // this dictionary must contains 
            var userData = new Dictionary<string, string>();
            userData.AddItemIfNotEmpty("id", graphData.Id);
            userData.AddItemIfNotEmpty("username", graphData.Email);
            userData.AddItemIfNotEmpty("name", graphData.Name);
            userData.AddItemIfNotEmpty("link", graphData.Link == null ? null : graphData.Link.AbsoluteUri);
            userData.AddItemIfNotEmpty("gender", graphData.Gender);
            userData.AddItemIfNotEmpty("birthday", graphData.Birthday);
            userData.AddItemIfNotEmpty("photo", String.Format("http://graph.facebook.com/{0}/picture?width=156&height=156", graphData.Id));
            return userData;
        }

        /// <summary>
        /// Obtains an access token given an authorization code and callback URL.
        /// </summary>
        /// <param name="returnUrl">
        /// The return url.
        /// </param>
        /// <param name="authorizationCode">
        /// The authorization code.
        /// </param>
        /// <returns>
        /// The access token.
        /// </returns>
        protected override string QueryAccessToken(Uri returnUrl, string authorizationCode)
        {
            // Note: Facebook doesn't like us to url-encode the redirect_uri value
            var builder = new UriBuilder(TokenEndpoint);
            builder.AppendQueryArgs(
                new Dictionary<string, string> {
					{ "client_id", _appId },
					{ "redirect_uri", NormalizeHexEncoding(returnUrl.AbsoluteUri) },
					{ "client_secret", _appSecret },
					{ "code", authorizationCode },
					{ "scope", String.Join(",", Scopes) },
				});

            using (var client = new WebClient())
            {
                string data;
                try
                {
                    data = client.DownloadString(builder.Uri);
                }
                catch (WebException)
                {
                    //try once again
                    data = client.DownloadString(builder.Uri);
                }
                if (string.IsNullOrEmpty(data))
                {
                    return null;
                }

                var parsedQueryString = HttpUtility.ParseQueryString(data);
                return parsedQueryString["access_token"];
            }
        }

        /// <summary>
        /// Converts any % encoded values in the URL to uppercase.
        /// </summary>
        /// <param name="url">The URL string to normalize</param>
        /// <returns>The normalized url</returns>
        /// <example>NormalizeHexEncoding("Login.aspx?ReturnUrl=%2fAccount%2fManage.aspx") returns "Login.aspx?ReturnUrl=%2FAccount%2FManage.aspx"</example>
        /// <remarks>
        /// There is an issue in Facebook whereby it will rejects the redirect_uri value if
        /// the url contains lowercase % encoded values.
        /// </remarks>
        private static string NormalizeHexEncoding(string url)
        {
            var chars = url.ToCharArray();
            for (int i = 0; i < chars.Length - 2; i++)
            {
                if (chars[i] == '%')
                {
                    chars[i + 1] = char.ToUpperInvariant(chars[i + 1]);
                    chars[i + 2] = char.ToUpperInvariant(chars[i + 2]);
                    i += 2;
                }
            }
            return new string(chars);
        }

        #endregion
    }
}

