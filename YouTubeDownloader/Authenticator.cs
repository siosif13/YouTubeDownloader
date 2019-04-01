using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using YouTubeDownloader;

namespace YouTubeDownloader
{
    class Authenticator
    {
        readonly string clientID = Properties.Settings.Default.ClientID;
        readonly string clientSecret = Properties.Settings.Default.ClientSecret;
        const string authorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
        const string tokenEndpoint = "https://www.googleapis.com/oauth2/v4/token";
        const string userInfoEndpoint = "https://www.googleapis.com/oauth2/v3/userinfo";

        private static int GetRandomUnusedPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        public async Task<AccountModel> GetAccountAccessAsync()
        {
            //Generate state and PKCE values.
            string state = randomDataBase64url(32);
            string code_verifier = randomDataBase64url(32);
            string code_challenge = base64urlencodeNoPadding(sha256(code_verifier));
            const string code_challenge_method = "S256";

            //Creates a redirect URI using an available port on the loopback address.
            string redirectURI = string.Format("http://{0}:{1}/", IPAddress.Loopback, GetRandomUnusedPort());
            System.Diagnostics.Debug.WriteLine("redirect URI: " + redirectURI);

            //Creates an HttpListener to listen for requests on that redirect URI.
            var http = new HttpListener();
            http.Prefixes.Add(redirectURI);
            System.Diagnostics.Debug.WriteLine("Listening...");
            http.Start();

            //Creates the OAuth 2.0 authorization requet.
            string authorizationRequest = string.Format("{0}?response_type=code&scope=email%20https://www.googleapis.com/auth/youtube.readonly%20https://www.googleapis.com/auth/youtube&redirect_uri={1}&client_id={2}&state={3}&code_challenge={4}&code_challenge_method={5}",
                authorizationEndpoint,
                System.Uri.EscapeDataString(redirectURI),
                clientID,
                state,
                code_challenge,
                code_challenge_method);

            //Opens request in the browser.
            System.Diagnostics.Process.Start(authorizationRequest);

            //Waits for the OAuth authorization response.
            var context = await http.GetContextAsync();

            // Sends an HTTP response to the browser on the loopback ip.
            var response = context.Response;
            string responseString = string.Format("<html><head><meta http-equiv='refresh' content='10;url=https://google.com'></head><body>Please return to the app.</body></html>");
            var buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            var responseOutput = response.OutputStream;
            Task responseTask = responseOutput.WriteAsync(buffer, 0, buffer.Length).ContinueWith((task) =>
            {
                responseOutput.Close();
                http.Stop();
                System.Diagnostics.Debug.WriteLine("HTTP server stopped.");
            });

            // Checks for errors.
            if (context.Request.QueryString.Get("error") != null)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("OAuth authorization error: {0}.", context.Request.QueryString.Get("error")));
              //  return "Auth error check output.";
            }
            if (context.Request.QueryString.Get("code") == null 
                || context.Request.QueryString.Get("state") == null)
            {
                System.Diagnostics.Debug.WriteLine("Malformed authorization response." + context.Request.QueryString);
             //   return "Auth error check output.";
            }

            // Extracts the code and initializes the Account object
            var code = context.Request.QueryString.Get("code");
            var incoming_state = context.Request.QueryString.Get("state");

            // Compares the receieved state to the expected value, to ensure that
            // this app made the request which resulted in authorization.
            if (incoming_state != state)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("Received request with invalid state ({0})", incoming_state));
              //  return "State error check output.";
            }

            var account = await performCodeExchange(code, code_verifier, redirectURI);
            persistAccount(account);
            return account;
        }

        async Task<AccountModel> performCodeExchange(string code, string code_verifier, string redirectURI)
        {
            System.Diagnostics.Debug.WriteLine("Echanging auth code for tokens..");

            //build the request
            string tokenRequestURI = "https://www.googleapis.com/oauth2/v4/token";
            string tokenRequestBody = string.Format("code={0}&redirect_uri={1}&client_id={2}&code_verifier={3}&client_secret={4}&grant_type=authorization_code",
                code,
                System.Uri.EscapeDataString(redirectURI),
                clientID,
                code_verifier,
                clientSecret
                );

            // sends the request
            HttpWebRequest tokenRequest = (HttpWebRequest)WebRequest.Create(tokenRequestURI);
            tokenRequest.Method = "POST";
            tokenRequest.ContentType = "application/x-www-form-urlencoded";
            tokenRequest.Accept = "Accept=text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
            byte[] _byteVersion = Encoding.ASCII.GetBytes(tokenRequestBody);
            tokenRequest.ContentLength = _byteVersion.Length;
            Stream stream = tokenRequest.GetRequestStream();
            await stream.WriteAsync(_byteVersion, 0, _byteVersion.Length);
            stream.Close();

            try
            {
                // gets the response
                WebResponse tokenResponse = await tokenRequest.GetResponseAsync();
                using (StreamReader reader = new StreamReader(tokenResponse.GetResponseStream()))
                {
                    // reads response body
                    string responseText = await reader.ReadToEndAsync();

                    // converts to dictionary
                    Dictionary<string, string> tokenEndpointDecoded = JsonConvert.DeserializeObject<Dictionary<string, string>>(responseText);

                    string access_token = tokenEndpointDecoded["access_token"];

                    Dictionary<string, string> userInfo = await userInfoCall(access_token);
                    string tmpFix;
                    if (!userInfo.ContainsKey("name"))
                        tmpFix = "Blank";
                    else
                        tmpFix = userInfo["name"];
                    AccountModel accountModel = new AccountModel(
                        tmpFix, 
                        userInfo["email"],
                        tokenEndpointDecoded["refresh_token"],
                        tokenEndpointDecoded["access_token"],
                        DateTime.Now,
                        userInfo["picture"]);

                    return accountModel;
                }
            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.ProtocolError)
                {
                    var response = ex.Response as HttpWebResponse;
                    if (response != null)
                    {
                        using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                        {
                            // reads response body
                            string responseText = await reader.ReadToEndAsync();
                           System.Diagnostics.Debug.WriteLine(responseText);
                        }
                    }
                }
                return null;
            }
        }

        async Task<Dictionary<string, string>> userInfoCall(string access_token)
        {
            Dictionary<string, string> JsonResponse;
            // build the request
            string userInfoRequestURI = "https://www.googleapis.com/oauth2/v3/userinfo";

            // sends the request
            HttpWebRequest userInfoRequest = (HttpWebRequest)WebRequest.Create(userInfoRequestURI);
            userInfoRequest.Method = "GET";
            userInfoRequest.Headers.Add(string.Format("Authorization: Bearer {0}", access_token));
            userInfoRequest.ContentType = "application/x-www-form-urlencoded";
            userInfoRequest.Accept = "Accept=text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";

            // gets the response
            WebResponse userInfoResponse = await userInfoRequest.GetResponseAsync();
            using (StreamReader userInfoResponseReader = new StreamReader(userInfoResponse.GetResponseStream()))
            {
                // reads response body
                string userInfoResponseText = await userInfoResponseReader.ReadToEndAsync();
                System.Diagnostics.Debug.WriteLine(userInfoResponseText);
                JsonResponse = JsonConvert.DeserializeObject<Dictionary<string, string>>(userInfoResponseText);
            }
            return JsonResponse;
        }

        public static void RefreshToken()
        {
            string requestUri = "https://www.googleapis.com/oauth2/v4/token";
            string tokenRequestBody = string.Format("client_id={0}&client_secret={1}&refresh_token={2}&grant_type=refresh_token",
                Properties.Settings.Default.ClientID,
                Properties.Settings.Default.ClientSecret,
                Properties.Settings.Default.RefreshToken);

            HttpWebRequest refreshTokenRequest = (HttpWebRequest)WebRequest.Create(requestUri);
            refreshTokenRequest.Method = "POST";
            refreshTokenRequest.ContentType = "application/x-www-form-urlencoded";
            refreshTokenRequest.Accept = "Accept=text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
            byte[] _byteVersion = Encoding.ASCII.GetBytes(tokenRequestBody);
            refreshTokenRequest.ContentLength = _byteVersion.Length;
            Stream stream = refreshTokenRequest.GetRequestStream();
            stream.Write(_byteVersion, 0, _byteVersion.Length);
            stream.Close();

            try
            {
                // gets the response
                WebResponse tokenResponse = refreshTokenRequest.GetResponse();
                using (StreamReader reader = new StreamReader(tokenResponse.GetResponseStream()))
                {
                    // reads response body
                    string responseText = reader.ReadToEnd();
                    // converts to dictionary
                    Dictionary<string, string> tokenEndpointDecoded = JsonConvert.DeserializeObject<Dictionary<string, string>>(responseText);

                    Properties.Settings.Default.AccessToken = tokenEndpointDecoded["access_token"];
                    Properties.Settings.Default.AccessTokenCreationDate = DateTime.Now;
                    Properties.Settings.Default.Upgrade();
                }
            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.ProtocolError)
                {
                    var response = ex.Response as HttpWebResponse;
                    if (response != null)
                    {
                        System.Diagnostics.Debug.WriteLine("HTTP: " + response.StatusCode);
                        using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                        {
                            // reads response body
                            string responseText = reader.ReadToEnd();
                        }
                    }
                }
            }
        }

        private static void persistAccount(AccountModel account)
        {
            Properties.Settings.Default.RefreshToken = account.RefreshToken;
            Properties.Settings.Default.AccessToken = account.AccessToken;
            Properties.Settings.Default.AccessTokenCreationDate = account.AccessTokenCreationDate;
            Properties.Settings.Default.IsLoggedIn = true;
            Properties.Settings.Default.AccountPicture = account.PictureUrl;
            Properties.Settings.Default.AccountName = account.Name;
            Properties.Settings.Default.AccountEmail = account.Email;
            Properties.Settings.Default.Save();
        }

        /// <summary>
        /// Returns URI-safe data with a given input length.
        /// </summary>
        /// <param name="length">Input length (nb. output will be longer)</param>
        /// <returns></returns>

        public static string randomDataBase64url(uint length)
        {
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            byte[] bytes = new byte[length];
            rng.GetBytes(bytes);
            return base64urlencodeNoPadding(bytes);
        }

        /// <summary>
        /// Returns the SHA256 hash of the input string.
        /// </summary>
        /// <param name="inputStirng"></param>
        /// <returns></returns>
        public static byte[] sha256(string inputStirng)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(inputStirng);
            SHA256Managed sha256 = new SHA256Managed();
            return sha256.ComputeHash(bytes);
        }

        /// <summary>
        /// Base64url no-padding encodes the given input buffer.
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public static string base64urlencodeNoPadding(byte[] buffer)
        {
            string base64 = Convert.ToBase64String(buffer);

            // Converts base64 to base64url.
            base64 = base64.Replace("+", "-");
            base64 = base64.Replace("/", "_");
            // Strips padding.
            base64 = base64.Replace("=", "");

            return base64;
        }
    }
}
