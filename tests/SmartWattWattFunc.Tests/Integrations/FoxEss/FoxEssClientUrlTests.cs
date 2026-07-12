using System.Net;
using System.Text;
using System.Text.Json;
using SmartWattWattFunc.Integrations.FoxEss;

namespace SmartWattWattFunc.Tests.Integrations.FoxEss;

public sealed class FoxEssClientUrlTests
{
    [Fact]
    public async Task GetForceChargeScheduleAsync_PostsDeviceSerialNumberToSchedulerGet()
    {
        const string serialNumber = "605H5020633D141";
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var handler = new CapturingHandler(
            request =>
            {
                capturedRequest = request;
                capturedBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "errno": 0,
                          "msg": "Operation successful",
                          "result": {
                            "enable": 1,
                            "groups": [
                              {
                                "startHour": 0,
                                "startMinute": 0,
                                "endHour": 5,
                                "endMinute": 30,
                                "workMode": "ForceCharge"
                              },
                              {
                                "startHour": 23,
                                "startMinute": 30,
                                "endHour": 23,
                                "endMinute": 59,
                                "workMode": "ForceCharge"
                              },
                              {
                                "startHour": 0,
                                "startMinute": 0,
                                "endHour": 23,
                                "endMinute": 59,
                                "workMode": "SelfUse"
                              }
                            ],
                            "maxGroupCount": 8
                          }
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
                };
            });

        var client = new HttpClient(handler);
        var options = new FoxEssOptions
        {
            ApiToken = "test-token",
            DeviceSerialNumber = serialNumber
        };
        var foxEssClient = new FoxEssClient(client, options, TimeProvider.System);

        var schedule = await foxEssClient.GetForceChargeScheduleAsync();

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal(
            $"https://www.foxesscloud.com{FoxEssClient.SchedulerGetPath}",
            capturedRequest.RequestUri!.ToString());

        var body = capturedBody!;
        using var document = JsonDocument.Parse(body);
        Assert.Equal(serialNumber, document.RootElement.GetProperty("deviceSN").GetString());

        Assert.True(schedule.Slot1.Enabled);
        Assert.Equal(23, schedule.Slot1.Start.Hour);
        Assert.Equal(59, schedule.Slot1.End.Minute);
        Assert.True(schedule.Slot2.Enabled);
        Assert.Equal(5, schedule.Slot2.End.Hour);
        Assert.Equal(30, schedule.Slot2.End.Minute);
    }

    private sealed class CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
