using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
/*
 * Based on https://callumhoughton18.github.io/Personal-Site/blog/digest-auth-in-dotnet/
 */
namespace RLForwarderConsole
{
    public static class HttpClientExtensions
    {
        /// <summary>
        /// Executes a secondary call to <paramref name="serviceDir"/> if the call to <paramref name="request"/> fails due to a 401 error, using Digest authentication
        /// </summary>
        /// <param name="client">HTTPClient object, this should have the BaseAddress property set</param>
        /// <param name="serviceDir">Relative path to the resource from the host, e.g /services/service1?param=value</param>
        /// <param name="request"></param>
        /// <param name="httpCompletionOption"></param>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public static async Task<HttpResponseMessage> SendWithDigestAuthAsync(this HttpClient client,
            HttpRequestMessage request, HttpCompletionOption httpCompletionOption,
            string username, string password)
        {
            var newRequest = Clone(request);
            var response = await client.SendAsync(request, httpCompletionOption);
            if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized) return response;

            var wwwAuthenticateHeaderValue = response.Headers.GetValues("WWW-Authenticate").FirstOrDefault();

            var realm = GetChallengeValueFromHeader("realm", wwwAuthenticateHeaderValue);
            var nonce = GetChallengeValueFromHeader("nonce", wwwAuthenticateHeaderValue);
            var qop = GetChallengeValueFromHeader("qop", wwwAuthenticateHeaderValue);

            // Must be fresh on every request, so low chance of same client nonce here by just using a random number.
            var clientNonce = new Random().Next(123400, 9999999).ToString();
            // 
            var opaque = GetChallengeValueFromHeader("opaque", wwwAuthenticateHeaderValue);

            // The nonce count 'nc' doesn't really matter, so we just set this to one. Why we always sending two requests per 1 request
            var digestHeader = new DigestAuthHeader(realm, username, password, nonce, qop, nonceCount: 1, clientNonce, opaque);//opaque
            var digestRequestHeader = GetDigestHeader(digestHeader, newRequest.RequestUri.ToString(), request.Method);

            newRequest.Headers.Add("Authorization", digestRequestHeader);
            var authRes = await client.SendAsync(newRequest, httpCompletionOption);
            return authRes;
        }

        private static string GetChallengeValueFromHeader(string challengeName, string fullHeaderValue)
        {
            // if variableName = qop, the below regex would look like qop="([^""]*)"
            // So it matches anything with the challenge name and then gets the challenge value
            var regHeader = new Regex($@"{challengeName}=""([^""]*)""");
            var matchHeader = regHeader.Match(fullHeaderValue);

            if (matchHeader.Success) return matchHeader.Groups[1].Value;

            throw new ApplicationException($"Header {challengeName} not found");
        }

        private static string GenerateMD5Hash(string input)
        {
            // x2 formatter is for hexadecimal in the ToString function
            MD5 hash = MD5.Create();
            string result  = string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(input))
                                                    .Select(x => x.ToString("x2"))
                                                               );
            hash.Clear();
            hash.Dispose();
            return result; 
            
        }

        private static string GetDigestHeader(DigestAuthHeader digest, string digestUri, HttpMethod method)
        {
            var ha1 = GenerateMD5Hash($"{digest.Username}:{digest.Realm}:{digest.Password}");
            var ha2 = GenerateMD5Hash($"{method}:{digestUri}");
            var digestResponse =
                GenerateMD5Hash($"{ha1}:{digest.Nonce}:{digest.NonceCount:00000000}:{digest.ClientNonce}:{digest.QualityOfProtection}:{ha2}");

            var headerString =
                $"Digest username=\"{digest.Username}\", realm=\"{digest.Realm}\", nonce=\"{digest.Nonce}\", uri=\"{digestUri}\", " +
                $"algorithm=MD5, qop={digest.QualityOfProtection}, nc={digest.NonceCount:00000000}, cnonce=\"{digest.ClientNonce}\", " +
               // $"response=\"{digestResponse}\""; // Some camera models do not send back opaque in header 
               $"response=\"{digestResponse}\", opaque=\"{digest.Opaque}\"";


            return headerString;
        }

        
        private static HttpRequestMessage CloneBeforeContentSet(this HttpRequestMessage req)
        {
            // Deep clone of a given request, outlined here:
            // https://stackoverflow.com/questions/18000583/re-send-httprequestmessage-exception/18014515#18014515
            HttpRequestMessage clone = new HttpRequestMessage(req.Method, req.RequestUri);

            clone.Content = req.Content;
            clone.Version = req.Version;

            foreach (KeyValuePair<string, object> prop in req.Properties)
            {
                clone.Properties.Add(prop);
            }

            foreach (KeyValuePair<string, IEnumerable<string>> header in req.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return clone;
        }

        public static HttpRequestMessage Clone(this HttpRequestMessage request)
        {
            /// Request Clone method, outlined here:
            /// https://stackoverflow.com/questions/18000583/re-send-httprequestmessage-exception/18014515#18014515
            var clone = new HttpRequestMessage(request.Method, request.RequestUri)
            {
                Content = request.Content.Clone(),
                Version = request.Version
            };
            foreach (KeyValuePair<string, object> prop in request.Properties)
            {
                clone.Properties.Add(prop);
            }
            foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return clone;
        }

        public static HttpContent Clone(this HttpContent content)
        {
            if (content == null) return null;

            var ms = new MemoryStream();
            content.CopyToAsync(ms).Wait();
            ms.Position = 0;

            var clone = new StreamContent(ms);
            foreach (KeyValuePair<string, IEnumerable<string>> header in content.Headers)
            {
                clone.Headers.Add(header.Key, header.Value);
            }
            return clone;
        }
    }
}
