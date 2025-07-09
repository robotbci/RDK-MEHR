using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PshchAPI.Models
{
    /// <summary>
    /// 处理实际API通信
    /// </summary>
    public class DoubaoChatClient
    {
        private readonly ChatRequest _currentRequest;
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model;

        public DoubaoChatClient(string apiKey, string model = "doubao-1-5-pro-32k-250115")
        {
            _httpClient = new HttpClient();
            _currentRequest = new ChatRequest { Model = model };
            _apiKey = apiKey;
            _model = model;
        }

        // 设置系统身份
        public DoubaoChatClient SetSystemPrompt(string prompt)
        {
            _currentRequest.SetSystemPrompt(prompt);
            return this;
        }


        // 发送用户消息并获取回复
        public async Task<string> SendMessageAsync(string userMessage)
        {
            //保存记录
            _currentRequest.AddUserMessage(userMessage);
            //定义请求体
            var request = new HttpRequestMessage(HttpMethod.Post, "https://ark.cn-beijing.volces.com/api/v3/chat/completions")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { model = _model, messages = _currentRequest.Messages }),
                    Encoding.UTF8,
                    "application/json")
            };
            //设置请求头，Authorization认证信息
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            //发送请求
            var response = await _httpClient.SendAsync(request);
            //自动检查 HTTP 响应的状态码，如果状态码表示失败（即不在 200-299 范围内），该方法会 抛出异常
            response.EnsureSuccessStatusCode();
            //作用：异步读取HTTP响应体（Response Body）内容为字符串。

            //返回值：string 类型的JSON格式文本（如 { "name":"Alice","age":25}）。
            var responseBody = await response.Content.ReadAsStringAsync();
            //作用：将JSON字符串解析为 JsonDocument 对象，提供结构化访问能力。返回值：轻量级的 JsonDocument，可用于查询JSON数据。
            var jsonDoc = JsonDocument.Parse(responseBody);
            //获得返回内容
            var assistantReply = jsonDoc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            _currentRequest.AddAssistantMessage(assistantReply);
            return assistantReply;
        }

        // 获取完整对话历史
        public string GetConversationHistory()
        {
            return JsonSerializer.Serialize(
                _currentRequest.Messages,         // 要序列化的对象（List<ChatMessage>）
                new JsonSerializerOptions{       // 序列化配置
                    WriteIndented = true          // 启用缩进格式化
                }
            );
        }

        // 清空对话（保留系统消息）
        public void ClearConversation()
        {
            _currentRequest.ClearConversation();
        }

        // 添加对话持久化功能
        public void SaveConversation(string filePath)
        {
            //路径，内容
            File.WriteAllText(filePath, GetConversationHistory());
        }
        
        //修正后的对话加载方法
        public void LoadConversation(string filePath)
        {
            var json = File.ReadAllText(filePath);
            _currentRequest.Messages = JsonSerializer.Deserialize<List<ChatMessage>>(json);
        }
    }
}
