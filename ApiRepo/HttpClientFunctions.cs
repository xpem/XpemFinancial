using Model.Resp.Api;
using System.Net;
using System.Text;

namespace ApiRepo
{
    public class HttpClientFunctions()
    {
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);

        public static async Task<ApiResp> Request(RequestsTypes requestsType, string url, string? userToken = null, string? jsonContent = null)
        {
            try
            {
                HttpClient httpClient = new()
                {
                    Timeout = RequestTimeout
                };

                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("XpemFinancial/1.0 (Android)");

                if (userToken is not null)
                    httpClient.DefaultRequestHeaders.Add("authorization", "bearer " + userToken);

                HttpResponseMessage httpResponse = new();

                switch (requestsType)
                {
                    case RequestsTypes.Get:
                        httpResponse = await httpClient.GetAsync(url);
                        break;
                    case RequestsTypes.Post:
                        if (jsonContent is not null)
                        {
                            StringContent bodyContent = new(jsonContent, Encoding.UTF8, "application/json");
                            httpResponse = await httpClient.PostAsync(url, bodyContent);
                        }
                        else return new ApiResp() { Success = false, Content = null, Error = ErrorTypes.BodyContentNull };
                        break;
                    case RequestsTypes.Put:
                        if (jsonContent is not null)
                        {
                            StringContent bodyContent = new(jsonContent, Encoding.UTF8, "application/json");
                            httpResponse = await httpClient.PutAsync(url, bodyContent);
                        }
                        else return new ApiResp() { Success = false, Content = null, Error = ErrorTypes.BodyContentNull };
                        break;
                    case RequestsTypes.Delete:
                        httpResponse = await httpClient.DeleteAsync(url);
                        break;
                }

                return new ApiResp()
                {
                    Success = httpResponse.IsSuccessStatusCode,
                    Error = httpResponse.StatusCode == HttpStatusCode.Unauthorized ? ErrorTypes.Unauthorized : null,
                    TryRefreshToken = httpResponse.StatusCode == HttpStatusCode.Unauthorized,
                    Content = await httpResponse.Content.ReadAsStringAsync()
                };
            }
            catch (HttpRequestException ex)
            {
                return new ApiResp() { Success = false, Error = ErrorTypes.ServerUnavaliable, Content = $"[HttpRequestException] {ex.Message} | Inner: {ex.InnerException?.Message}" };
            }
            catch (TaskCanceledException ex)
            {
                return new ApiResp() { Success = false, Error = ErrorTypes.ServerUnavaliable, Content = $"[TaskCanceledException] {ex.Message} | Inner: {ex.InnerException?.Message}" };
            }
        }
    }
}
