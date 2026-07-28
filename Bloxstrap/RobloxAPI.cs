using static System.Convert;
using System.Security.Cryptography;
using System.Net.Http.Json;
using System.Reflection;

namespace Bloxstrap
{
    /*
        Handler for everything that uses the Roblox API using credentials, in this case, asset loading through .ROBLOSECURITY
        
        All uses of the cookie are listed below:
            - Download public roblox assets (Just like your client does)
    */

    // TODO: Maybe delete old assets from %temp% on game switch?
    
    // gubby this gubby that gubby server gubby lan gubby wi-fi gubby ram

    static class RobloxAPI
    {
        private const string GET_ASSET_URL = "https://assetdelivery.roblox.com/v2/assetId/";

        private static readonly HttpClient client = new HttpClient( new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All } );
        private static bool hasRetrievedCookie = false;

        private static async Task<HttpResponseMessage> doRequest(string url, HttpMethod? method)
        {
            const string LOG_IDENT = "RobloxAPI::doRequest";

            HttpResponseMessage response = await client.SendAsync(new HttpRequestMessage(method == null ? HttpMethod.Get : method, url));
            response.EnsureSuccessStatusCode();

            // Set-Cookie compliance ig
            // https://devforum.roblox.com/t/upcoming-roblosecurity-cookie-format-changes/4328913
            if (response.Headers.TryGetValues("Set-Cookie", out var cookieHeaders))
            {
                foreach (var headerValue in cookieHeaders)
                {
                    var cookieInfo = headerValue.Split(';').FirstOrDefault();

                    if (string.IsNullOrWhiteSpace(cookieInfo))
                        continue;
                    
                    var cookieValues = cookieInfo.Split('=', 2);

                    if (cookieValues[0].Trim() != ".ROBLOSECURITY")
                        continue;

                    client.DefaultRequestHeaders.Add("Cookie", $".ROBLOSECURITY={cookieValues[1].Trim()}");

                    App.Logger.WriteLine(LOG_IDENT, "Updated cookie based on set-cookie header");
                }
            }

            return response;
        }

        private static void retrieveCookie() // this looks scary
        {
            const string LOG_IDENT = "RobloxAPI::retrieveCookie";

            if (hasRetrievedCookie)
                return;

            string? encodedCookies;

            try {
                // Read cookie files (%localappdata%/.../RobloxCookies.dat)
                string fileContent = File.ReadAllText(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "LocalStorage", "RobloxCookies.dat"),
                    Encoding.UTF8
                );

                // Read json and get CookiesData entry
                JsonSerializer.Deserialize<Dictionary<string, string>>(fileContent)!.TryGetValue("CookiesData", out encodedCookies);

                if (encodedCookies == null) {
                    throw new Exception("CookiesData not found");
                }
            } catch (Exception e) {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to read RobloxCookies.dat: {e.Message}");
                return;
            }
        
            // Decode from base64
            byte[] decodedCookies = FromBase64String(encodedCookies);

            // Decrypt and get string
            byte[] decryptedBytes = ProtectedData.Unprotect(decodedCookies, null, DataProtectionScope.CurrentUser);
            string cookieData = Encoding.UTF8.GetString(decryptedBytes);

            // Find .ROBLOSECURITY cookie
            Match match = Regex.Match(cookieData ,@"\.ROBLOSECURITY\t([^;]+)");

            if (match.Success) { // If found
                App.Logger.WriteLine(LOG_IDENT, "Successfully retrieved .ROBLOSECURITY cookie");

                // Store and mark and retrieved
                hasRetrievedCookie = true;
                client.DefaultRequestHeaders.Add("Cookie", $".ROBLOSECURITY={match.Groups[1].Value}");
            } else {
                App.Logger.WriteLine(LOG_IDENT, "Failed to retrieve .ROBLOSECURITY cookie");
            }
        }

        async public static Task<string> getAsset(ulong assetId)
        {
            // Attempt to find prev loaded image
            // Psst.. you could... replace this image with your own.. this is as close of "game modding" you can gonna get without breaking ToS btw
            var resultPath = Path.Combine(Path.GetTempPath(), $"funkstrap_{assetId}.png");
            if (File.Exists(resultPath))
                return resultPath;

            retrieveCookie();
            if (!hasRetrievedCookie)
                throw new Exception("No cookie to use for request");

            var responseContent = await (await doRequest(GET_ASSET_URL + assetId, HttpMethod.Get)).Content.ReadFromJsonAsync<AssetDeliveryResponse>();
            
            // TODO: CHECK FOR ASSETTYPE
            
            var location = responseContent?.Locations?.First()?.Location ?? null;

            if (location == string.Empty || location == null)
                throw new Exception("No location found for asset");

            var imgBytes = await client.GetByteArrayAsync(location);
            string tempPath = resultPath;
            await File.WriteAllBytesAsync(tempPath, imgBytes);

            return tempPath;
        }
    }
}
