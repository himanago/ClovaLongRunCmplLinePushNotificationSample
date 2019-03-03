using CEK.CSharp;
using CEK.CSharp.Models;
using Line.Messaging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ClovaDurableSessionSample
{
    public static class LongTimeFunctions
    {
        [FunctionName(nameof(LongTimeFunction))]
        public static async Task<IActionResult> LongTimeFunction(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)]HttpRequest req,
            [OrchestrationClient] DurableOrchestrationClient client,
            Microsoft.Azure.WebJobs.ExecutionContext context,
            ILogger log)
        {
            var cekResponse = new CEKResponse();
            var clovaClient = new ClovaClient();
            var cekRequest = await clovaClient.GetRequest(req.Headers["SignatureCEK"], req.Body);
            switch (cekRequest.Request.Type)
            {
                case RequestType.LaunchRequest:
                    {
                        // UserId をインスタンス ID として新しい関数を実行
                        await client.StartNewAsync(nameof(LongTimeOrchestrationFunction), cekRequest.Session.User.UserId, context.FunctionAppDirectory);
                        cekResponse.AddText("時間のかかる処理を実行しました。結果はLINEでお知らせします。");
                        break;
                    }
                case RequestType.IntentRequest:
                default:
                    {
                        // インテントリクエスト他は特に用意しない
                        cekResponse.AddText("すみません。よくわかりませんでした。");
                        break;
                    }
            }

            return new OkObjectResult(cekResponse);
        }

        [FunctionName(nameof(LongTimeOrchestrationFunction))]
        public static async Task LongTimeOrchestrationFunction(
            [OrchestrationTrigger] DurableOrchestrationContext context)
        {
            // 時間のかかる処理を１つ呼び出す
            var result = await context.CallActivityAsync<string>(nameof(LongTimeActivityFunction), null);

            // 結果をLINEで通知
            var config = new ConfigurationBuilder()
                            .SetBasePath(context.GetInput<string>())    // FunctionAppDirectory
                            .AddJsonFile("local.settings.json", true)
                            .AddEnvironmentVariables()
                            .Build();

            await new LineMessagingClient(config.GetValue<string>("MessagingApiChannelAccessToken")).PushMessageAsync(
                to: context.InstanceId, // LINEのユーザーID
                messages: new List<ISendMessage> { new TextMessage($"終わりました。結果は{result}です。") });
        }

        [FunctionName(nameof(LongTimeActivityFunction))]
        public static async Task<string> LongTimeActivityFunction(
            [ActivityTrigger] DurableActivityContext context)
        {
            // 時間のかかる処理（60秒待つだけ）
            var time = 60000;
            await Task.Delay(time);
            return $"{(time / 1000).ToString()}秒待機成功";
        }
    }
}
