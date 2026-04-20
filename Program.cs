using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

class AppConfig
{
    public string ServerUrl { get; set; } = string.Empty;
    public string ForwardUrl { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
}

class Program
{
    private static readonly HttpClient httpClient = new HttpClient();

    static async Task Main(string[] args)
    {
        AppConfig config = LoadConfig();

        using HttpListener listener = new HttpListener();
        listener.Prefixes.Add(config.ServerUrl);

        try
        {
            listener.Start();
            Console.WriteLine($"[启动] 监听地址: {config.ServerUrl}");
            Console.WriteLine($"[启动] 转发地址: {config.ForwardUrl}");
            Console.WriteLine("[启动] 按 Ctrl + C 停止服务");

            while (true)
            {
                HttpListenerContext context = await listener.GetContextAsync();
                _ = Task.Run(() => HandleRequestAsync(context, config));
            }
        }
        catch (HttpListenerException ex)
        {
            Console.WriteLine($"[错误] HttpListener 启动失败: {ex.Message}");
            Console.WriteLine("[提示] 如有权限问题，可尝试管理员运行，或为该 URL 预留权限。");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[错误] 程序异常: {ex}");
        }
    }

    static AppConfig LoadConfig()
    {
        const string ConfigFileName = "appsettings.Local.json";

        if (!File.Exists(ConfigFileName))
            throw new Exception($"配置文件 {ConfigFileName} 不存在, 请确保与程序exe同目录下有该文件。如果没有, 可按README.md中的示例创建一个。");

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile(ConfigFileName, optional: false, reloadOnChange: true)
            .Build();

        AppConfig config = new AppConfig();
        configuration.GetSection("App").Bind(config);

        if (string.IsNullOrWhiteSpace(config.ServerUrl))
            throw new Exception("配置项 App:ServerUrl 不能为空");

        if (string.IsNullOrWhiteSpace(config.ForwardUrl))
            throw new Exception("配置项 App:ForwardUrl 不能为空");

        return config;
    }

    static async Task HandleRequestAsync(HttpListenerContext context, AppConfig config)
    {
        string method = context.Request.HttpMethod;
        string path = context.Request.Url?.AbsolutePath ?? "/";
        string remote = context.Request.RemoteEndPoint?.ToString() ?? "unknown";

        try
        {
            if (!string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
            {
                await WriteResponseAsync(context, 405, "Only GET is supported");
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 非 GET 请求: {method} {path} 来自 {remote}");
                return;
            }

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 收到 GET: {path} 来自 {remote}");

            const string BodyPath = "body.json";
            const string BodyRuntimePlaceholder = "RUNTIME_MSG";

            if (!File.Exists(BodyPath))
            {
                await WriteResponseAsync(context, 404, "Body file not found");
                Console.WriteLine($"配置文件 {BodyPath} 不存在, 将无法使用Http Post发送推送通知。可按README.md中的示例创建该配置。");
                return;
            }

            string body = await File.ReadAllTextAsync(BodyPath, Encoding.UTF8);

            if (body.Contains(BodyRuntimePlaceholder))
            {
                string queryMessage = context.Request.QueryString["msg"] ?? "No query message";

                string runtimeMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n{queryMessage}";

                body = body.Replace(BodyRuntimePlaceholder, runtimeMessage);

                Console.WriteLine($"已替换 RUNTIME_MSG -> {runtimeMessage}");
            }

            using var content = new StringContent(body, Encoding.UTF8, config.ContentType);

            HttpResponseMessage postResponse = await httpClient.PostAsync(config.ForwardUrl, content);

            string postResult = await postResponse.Content.ReadAsStringAsync();

            Console.WriteLine(
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 已转发 POST -> {config.ForwardUrl}, " +
                $"内容: {content.ReadAsStringAsync().Result}, " +
                $"状态码: {(int)postResponse.StatusCode}"
            );

            var result = new
            {
                ok = true,
                receivedMethod = method,
                forwardUrl = config.ForwardUrl,
                forwardStatus = (int)postResponse.StatusCode,
                forwardResponse = postResult
            };

            string json = JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await WriteResponseAsync(context, 200, json, "application/json; charset=utf-8");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 处理失败: {ex.Message}");
            await WriteResponseAsync(context, 500, $"Internal Server Error: {ex.Message}");
        }
    }

    static async Task WriteResponseAsync(
        HttpListenerContext context,
        int statusCode,
        string text,
        string contentType = "text/plain; charset=utf-8")
    {
        byte[] buffer = Encoding.UTF8.GetBytes(text);
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = contentType;
        context.Response.ContentLength64 = buffer.Length;

        await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        context.Response.OutputStream.Close();
    }
}
