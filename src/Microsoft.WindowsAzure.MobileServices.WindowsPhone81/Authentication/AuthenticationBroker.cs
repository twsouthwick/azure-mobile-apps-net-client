﻿// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using Windows.Security.Authentication.Web;

namespace Microsoft.WindowsAzure.MobileServices
{
    /// <summary>
    /// AuthenticationBroker for the Windows Store Platform that uses the Windows Store
    /// <see cref="WebAuthenticationBroker"/> APIs.
    /// </summary>
    internal class AuthenticationBroker
    {
        static private TaskCompletionSource<string> pendingLoginTask;

        /// <summary>
        /// Begins a server-side authentication flow by navigating the 
        /// <see cref="WebAuthenticationBroker"/> to the <paramref name="startUrl"/>.
        /// </summary>
        /// <param name="startUrl">The URL that the browser-based control should 
        /// first navigate to in order to start the authenication flow.
        /// </param>
        /// <param name="endUrl">The URL that indicates the authentication flow has 
        /// completed. Upon being redirected to any URL that starts with the 
        /// endUrl, the browser-based control must stop navigating and
        /// return the response data to the <see cref="AuthenticationBroker"/>.
        /// </param>
        /// <param name="useSingleSignOn">Indicates if single sign-on should be used so 
        /// that users do not have to re-enter his/her credentials every time.
        /// </param>
        /// <returns>
        /// The response data from the authentication flow that contains a string of JSON 
        /// that represents a Mobile Services authentication token.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the user cancels the authentication flow or an error occurs during
        /// the authentication flow.
        /// </exception>
        public Task<string> AuthenticateAsync(Uri startUrl, Uri endUrl, bool useSingleSignOn)
        {
            return AuthenticateWithBroker(startUrl, endUrl, useSingleSignOn);
        }

        public void AuthenticationComplete(WebAuthenticationResult result)
        {
            if (pendingLoginTask == null)
            {
                throw new InvalidOperationException("Authentication has not been started.");
            }

            if (result.ResponseStatus != WebAuthenticationStatus.Success)
            {
                string message;
                if (result.ResponseStatus == WebAuthenticationStatus.UserCancel)
                {
                    message = "Authentication was cancelled by the user.";
                }
                else
                {
                    message = string.Format(CultureInfo.InvariantCulture,
                                            "Authentication failed with HTTP response code {0}.",
                                            result.ResponseErrorDetail);
                }
                pendingLoginTask.SetException(new InvalidOperationException(message));                
            }
            else
            {
                string tokenString = GetTokenStringFromResult(result);
                pendingLoginTask.SetResult(tokenString);
            }

            pendingLoginTask = null;            
        }

        /// <summary>
        /// Begins a server-side authentication flow by navigating the 
        /// <see cref="WebAuthenticationBroker"/> to the <paramref name="startUrl"/>.
        /// Considers if the <paramref name="useSingleSignOn"/> is being used and calls the
        /// correct overload of the <see cref="WebAuthenticationBroker"/>.
        /// </summary>
        /// <param name="startUrl">The URL that the browser-based control should 
        /// first navigate to in order to start the authenication flow.
        /// </param>
        /// <param name="endUrl">The URL that indicates the authentication flow has 
        /// completed. Upon being redirected to any URL that starts with the 
        /// <paramref name="endUrl"/>, the browser-based control must stop navigating and
        /// return the response data to the <see cref="AuthenticationBroker"/>.
        /// </param>
        /// <param name="useSingleSignOn">Indicates if single sign-on should be used so 
        /// that users do not have to re-enter his/her credentials every time.
        /// </param>
        /// <returns>
        /// The <see cref="WebAuthenticationResult"/> returned by the 
        /// <see cref="WebAuthenticationBroker"/>.
        /// </returns>
        private Task<string> AuthenticateWithBroker(Uri startUrl, Uri endUrl, bool useSingleSignOn)
        {
            Debug.Assert(startUrl != null);
            Debug.Assert(endUrl != null);

            if (pendingLoginTask != null)
            {
                throw new InvalidOperationException("Authentication is already in progress.");
            }

            pendingLoginTask = new TaskCompletionSource<string>();

            if (useSingleSignOn)
            {
                Uri ssoEndUri = WebAuthenticationBroker.GetCurrentApplicationCallbackUri();
                Uri ssoStartUrl = GetUrlWithQueryStringParameter(startUrl, "sso_end_uri", ssoEndUri.AbsoluteUri);
                WebAuthenticationBroker.AuthenticateAndContinue(ssoStartUrl, null, null, WebAuthenticationOptions.None);
            }
            else
            {
                WebAuthenticationBroker.AuthenticateAndContinue(startUrl, endUrl, null, WebAuthenticationOptions.None);
            }

            return pendingLoginTask.Task;
        }

        /// <summary>
        /// Gets the JSON string that represents the Mobile Service authentication token
        /// from the <see cref="WebAuthenticationResult"/>.
        /// </summary>
        /// <param name="result">The <see cref="WebAuthenticationResult"/> returned
        /// from the <see cref="WebAuthenticationBroker"/>.</param>
        /// <returns>
        /// A JSON string that represents a Mobile Service authentication token.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the authentication flow resulted in an error message or an invalid response.
        /// </exception>
        private string GetTokenStringFromResult(WebAuthenticationResult result)
        {
            Debug.Assert(result != null);
            Debug.Assert(result.ResponseStatus == WebAuthenticationStatus.Success);

            string tokenString = null;

            string responseData = result.ResponseData;
            if (!string.IsNullOrEmpty(responseData))
            {
                tokenString = GetSubStringAfterMatch(responseData, "#token=");
            }

            if (string.IsNullOrEmpty(tokenString))
            {
                string message = null;
                string errorString = GetSubStringAfterMatch(responseData, "#error=");
                if (string.IsNullOrEmpty(errorString))
                {
                    message = "Invalid format of the authentication response.";
                }
                else
                {
                    message = string.Format(CultureInfo.InvariantCulture,
                                            "Login failed: {0}",
                                            errorString);
                }

                throw new InvalidOperationException(message);
            }

            return tokenString;
        }

        /// <summary>
        /// Returns a substring from the <paramref name="stringToSearch"/> starting from
        /// the first character after the <paramref name="matchString"/> if the 
        /// <paramref name="stringToSearch"/> contains the <paramref name="matchString"/>;
        /// otherwise, returns <c>null</c>.
        /// </summary>
        /// <param name="stringToSearch">The string to search for the <paramref name="matchString"/>.
        /// </param>
        /// <param name="matchString">The string to look for in the <paramref name="stringToSearch"/>
        /// </param>
        /// <returns>The substring from <paramref name="stringToSearch"/> that follows the
        /// <paramref name="matchString"/> if the <paramref name="stringToSearch"/> contains 
        /// the <paramref name="matchString"/>; otherwise, returns <c>null</c>.
        /// </returns>
        private string GetSubStringAfterMatch(string stringToSearch, string matchString)
        {
            Debug.Assert(stringToSearch != null);
            Debug.Assert(matchString != null);

            string value = null;

            int index = stringToSearch.IndexOf(matchString);
            if (index > 0)
            {
                value = Uri.UnescapeDataString(stringToSearch.Substring(index + matchString.Length));
            }

            return value;
        }

        /// <summary>
        /// Returns a URL that is equivalent to the <paramref name="url"/> provided but which
        /// includes in the query string of the URL the <paramref name="queryParameter"/>
        /// with the value given by <paramref name="queryValue"/>.
        /// </summary>
        /// <param name="url">The URL to add the query string parameter and value to.
        /// </param>
        /// <param name="queryParameter">The name of the query string parameter to add to 
        /// the URL.
        /// </param>
        /// <param name="queryValue">The value of the query string parameter to add to the URL.
        /// </param>
        /// <returns>
        /// A URL that is equivalent to the <paramref name="url"/> provided but which
        /// includes in the query string of the URL the <paramref name="queryParameter"/>
        /// with the value given by <paramref name="queryValue"/>.
        /// </returns>
        internal Uri GetUrlWithQueryStringParameter(Uri url, string queryParameter, string queryValue)
        {
            if (url == null)
            {
                throw new ArgumentNullException("url");
            }
            if (queryParameter == null)
            {
                throw new ArgumentNullException("queryParameter");
            }
            if (queryValue == null)
            {
                throw new ArgumentNullException("queryValue");
            }

            string queryParameterEscaped = Uri.EscapeDataString(queryParameter);
            string queryValueEscaped = Uri.EscapeDataString(queryValue);

            UriBuilder uriBuilder = new UriBuilder(url);

            string queryToAppend = string.Format(CultureInfo.InvariantCulture, "{0}={1}", queryParameterEscaped, queryValueEscaped);
            string query = uriBuilder.Query;

            // Must strip off "?" prefix of query before setting it back to avoid "??" in the query.
            // Because UriBuild.Query property (https://msdn.microsoft.com/en-us/library/system.uribuilder.query) 
            // getter starts with "?", but property setter starts without "?".
            if (!string.IsNullOrEmpty(query) && query.Length > 1)
            {
                query = query.Substring(1) + "&" + queryToAppend;
            }
            else
            {
                query = queryToAppend;
            }

            uriBuilder.Query = query;

            return uriBuilder.Uri;
        }
    }
}
