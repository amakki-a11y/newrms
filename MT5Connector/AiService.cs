using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MT5Connector
{
    public class AiService
    {
        private readonly AccountService _accountService;
        private readonly DbService _db;
        private readonly MT5Service _mt5;
        private readonly string? _apiKey;
        private readonly HttpClient _httpClient;
        private readonly List<JObject> _conversationHistory = new();
        private const int MaxHistoryMessages = 20;
        private const string ApiUrl = "https://api.anthropic.com/v1/messages";
        private const string Model = "claude-sonnet-4-20250514";

        private static readonly string SystemPrompt =
            "You are a risk management AI assistant for an MT5 trading platform. " +
            "You have access to live account data, positions, and market prices. " +
            "Provide concise, actionable risk analysis. When the user asks to perform trading actions, " +
            "use the available tools. Always confirm before executing trades.";

        public AiService(AccountService accountService, DbService db, MT5Service mt5)
        {
            _accountService = accountService;
            _db = db;
            _mt5 = mt5;
            _apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

            if (string.IsNullOrEmpty(_apiKey))
            {
                Console.WriteLine("[AI] WARNING: ANTHROPIC_API_KEY not set. AI chat is disabled.");
            }
            else
            {
                Console.WriteLine("[AI] API key configured. AI chat is enabled.");
            }

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey ?? "");
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            _httpClient.Timeout = TimeSpan.FromMinutes(3);
        }

        public async Task ProcessChatMessage(string message, Action<string> onChunk, Action<string, object?> onAction, Action onDone)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                onChunk("AI chat is not configured. Set the ANTHROPIC_API_KEY environment variable.");
                onDone();
                return;
            }

            try
            {
                // Build context from live data
                var context = await BuildContextString();

                // Build the user message with context prepended
                var userContent = $"[Current Platform State]\n{context}\n\n[User Message]\n{message}";

                // Add user message to conversation history
                _conversationHistory.Add(new JObject
                {
                    ["role"] = "user",
                    ["content"] = userContent
                });

                // Trim history to max
                TrimHistory();

                // Send to Claude and handle the response (potentially multiple rounds for tool use)
                await SendAndProcessResponse(onChunk, onAction, onDone);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AI] Error processing chat message: {ex.Message}");
                onChunk($"Sorry, an error occurred while processing your request: {ex.Message}");
                onDone();
            }
        }

        private async Task SendAndProcessResponse(Action<string> onChunk, Action<string, object?> onAction, Action onDone)
        {
            const int maxToolRounds = 10;

            for (int round = 0; round < maxToolRounds; round++)
            {
                var requestBody = BuildRequestBody();
                var content = new StringContent(
                    JsonConvert.SerializeObject(requestBody),
                    Encoding.UTF8,
                    "application/json"
                );

                var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
                {
                    Content = content
                };

                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AI] HTTP request failed: {ex.Message}");
                    onChunk("Sorry, failed to connect to the AI service. Please try again later.");
                    onDone();
                    return;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[AI] API error {response.StatusCode}: {errorBody}");
                    onChunk($"AI service error ({response.StatusCode}). Please try again later.");
                    onDone();
                    return;
                }

                // Parse the SSE stream
                var parseResult = await ParseSseStream(response, onChunk);

                // Build the assistant message from accumulated content blocks
                var assistantMessage = new JObject
                {
                    ["role"] = "assistant",
                    ["content"] = new JArray(parseResult.ContentBlocks)
                };
                _conversationHistory.Add(assistantMessage);
                TrimHistory();

                // If there are tool uses, execute them and continue
                if (parseResult.ToolUses.Count > 0)
                {
                    var toolResults = new List<JObject>();

                    foreach (var toolUse in parseResult.ToolUses)
                    {
                        var toolName = toolUse["name"]?.ToString() ?? "";
                        var toolId = toolUse["id"]?.ToString() ?? "";
                        var toolInput = toolUse["input"] as JObject ?? new JObject();

                        Console.WriteLine($"[AI] Executing tool: {toolName} with input: {toolInput}");
                        onAction(toolName, toolInput.ToObject<Dictionary<string, object?>>());

                        // Execute the tool
                        var result = await ExecuteTool(toolName, toolInput);

                        toolResults.Add(new JObject
                        {
                            ["type"] = "tool_result",
                            ["tool_use_id"] = toolId,
                            ["content"] = result
                        });
                    }

                    // Add tool results as a user message
                    _conversationHistory.Add(new JObject
                    {
                        ["role"] = "user",
                        ["content"] = new JArray(toolResults)
                    });
                    TrimHistory();

                    // Continue the loop to get Claude's response after tool execution
                    continue;
                }

                // No tool use - we're done
                onDone();
                return;
            }

            // If we hit max rounds, finish up
            Console.WriteLine("[AI] Max tool use rounds reached.");
            onDone();
        }

        private async Task<SseParseResult> ParseSseStream(HttpResponseMessage response, Action<string> onChunk)
        {
            var result = new SseParseResult();
            var currentToolUse = (JObject?)null;
            var toolInputJson = new StringBuilder();
            var currentTextBlock = (StringBuilder?)null;
            var isInTextBlock = false;

            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (line == null) break;

                // SSE format: "data: {...}" or "event: ..."
                if (!line.StartsWith("data: ")) continue;

                var jsonStr = line.Substring(6).Trim();
                if (jsonStr == "[DONE]") break;

                JObject? eventData;
                try
                {
                    eventData = JObject.Parse(jsonStr);
                }
                catch
                {
                    continue;
                }

                var eventType = eventData["type"]?.ToString() ?? "";

                switch (eventType)
                {
                    case "content_block_start":
                    {
                        var contentBlock = eventData["content_block"];
                        var blockType = contentBlock?["type"]?.ToString() ?? "";

                        if (blockType == "tool_use")
                        {
                            currentToolUse = new JObject
                            {
                                ["id"] = contentBlock?["id"]?.ToString() ?? "",
                                ["name"] = contentBlock?["name"]?.ToString() ?? "",
                                ["input"] = new JObject()
                            };
                            toolInputJson.Clear();
                            isInTextBlock = false;
                        }
                        else if (blockType == "text")
                        {
                            currentTextBlock = new StringBuilder();
                            isInTextBlock = true;
                        }
                        break;
                    }

                    case "content_block_delta":
                    {
                        var delta = eventData["delta"];
                        var deltaType = delta?["type"]?.ToString() ?? "";

                        if (deltaType == "text_delta")
                        {
                            var text = delta?["text"]?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(text))
                            {
                                onChunk(text);
                                currentTextBlock?.Append(text);
                            }
                        }
                        else if (deltaType == "input_json_delta")
                        {
                            var partialJson = delta?["partial_json"]?.ToString() ?? "";
                            toolInputJson.Append(partialJson);
                        }
                        break;
                    }

                    case "content_block_stop":
                    {
                        if (currentToolUse != null)
                        {
                            // Parse the accumulated JSON input
                            try
                            {
                                var inputStr = toolInputJson.ToString();
                                if (!string.IsNullOrEmpty(inputStr))
                                {
                                    currentToolUse["input"] = JObject.Parse(inputStr);
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[AI] Failed to parse tool input JSON: {ex.Message}");
                                currentToolUse["input"] = new JObject();
                            }

                            result.ToolUses.Add(currentToolUse);

                            // Add to content blocks for conversation history
                            result.ContentBlocks.Add(new JObject
                            {
                                ["type"] = "tool_use",
                                ["id"] = currentToolUse["id"],
                                ["name"] = currentToolUse["name"],
                                ["input"] = currentToolUse["input"]
                            });

                            currentToolUse = null;
                            toolInputJson.Clear();
                        }
                        else if (isInTextBlock && currentTextBlock != null)
                        {
                            // Finalize text block and add to content blocks for history
                            result.ContentBlocks.Add(new JObject
                            {
                                ["type"] = "text",
                                ["text"] = currentTextBlock.ToString()
                            });
                            currentTextBlock = null;
                            isInTextBlock = false;
                        }
                        break;
                    }

                    case "message_delta":
                    {
                        var stopReason = eventData["delta"]?["stop_reason"]?.ToString();
                        if (!string.IsNullOrEmpty(stopReason))
                        {
                            result.StopReason = stopReason;
                        }
                        break;
                    }

                    case "message_stop":
                    {
                        break;
                    }
                }
            }

            // Safety: if no content blocks were captured, add an empty text block
            if (result.ContentBlocks.Count == 0)
            {
                result.ContentBlocks.Add(new JObject
                {
                    ["type"] = "text",
                    ["text"] = ""
                });
            }

            return result;
        }

        private object BuildRequestBody()
        {
            return new
            {
                model = Model,
                max_tokens = 4096,
                system = SystemPrompt,
                messages = _conversationHistory.ToArray(),
                tools = GetToolDefinitions(),
                stream = true
            };
        }

        private async Task<string> BuildContextString()
        {
            var sb = new StringBuilder();

            try
            {
                // Account summary
                var summary = await _db.GetAccountSummary();
                sb.AppendLine($"Total Accounts: {summary.TotalAccounts}");
                sb.AppendLine($"Total Equity: ${summary.TotalEquity:N2}");
                sb.AppendLine($"Total Balance: ${summary.TotalBalance:N2}");
                sb.AppendLine($"Total Profit: ${summary.TotalProfit:N2}");
                sb.AppendLine($"Total Margin Used: ${summary.TotalMargin:N2}");
                sb.AppendLine($"Total Free Margin: ${summary.TotalMarginFree:N2}");

                if (summary.TotalEquity > 0 && summary.TotalMargin > 0)
                {
                    var marginUtilization = (summary.TotalMargin / summary.TotalEquity) * 100;
                    sb.AppendLine($"Margin Utilization: {marginUtilization:N2}%");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AI] Error fetching account summary: {ex.Message}");
                sb.AppendLine("Account summary: unavailable");
            }

            try
            {
                // Symbol exposure
                var exposures = await _db.GetSymbolExposure();
                if (exposures.Count > 0)
                {
                    sb.AppendLine("\nTop Symbol Exposures:");
                    var top = exposures.OrderByDescending(e => Math.Abs(e.NetVolume)).Take(10);
                    foreach (var exp in top)
                    {
                        sb.AppendLine($"  {exp.Symbol}: Net Volume={exp.NetVolume:N2}, " +
                                      $"Long={exp.LongVolume:N2}, Short={exp.ShortVolume:N2}, " +
                                      $"Net Profit=${exp.NetProfit:N2}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AI] Error fetching symbol exposure: {ex.Message}");
            }

            try
            {
                // Current ticks for actively traded symbols
                var ticks = _mt5.GetAllTicks();
                if (ticks.Count > 0)
                {
                    sb.AppendLine($"\nLive Market Prices ({ticks.Count} symbols tracked):");
                    var topTicks = ticks.Values.OrderBy(t => t.Symbol).Take(15);
                    foreach (var tick in topTicks)
                    {
                        sb.AppendLine($"  {tick.Symbol}: Bid={tick.Bid}, Ask={tick.Ask}, Spread={tick.Spread}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AI] Error fetching ticks: {ex.Message}");
            }

            return sb.ToString();
        }

        private async Task<string> ExecuteTool(string toolName, JObject input)
        {
            try
            {
                switch (toolName)
                {
                    case "close_position":
                    {
                        var login = input["login"]?.Value<long>() ?? 0;
                        var ticket = input["ticket"]?.Value<long>() ?? 0;
                        var symbol = input["symbol"]?.ToString() ?? "";
                        var action = input["action"]?.Value<int>() ?? 0;
                        var volume = input["volume"]?.Value<long>() ?? 0;

                        var result = await _accountService.ClosePosition(login, ticket, symbol, action, volume);
                        return JsonConvert.SerializeObject(result);
                    }

                    case "open_position":
                    {
                        var login = input["login"]?.Value<long>() ?? 0;
                        var symbol = input["symbol"]?.ToString() ?? "";
                        var action = input["action"]?.Value<int>() ?? 0;
                        var volume = input["volume"]?.Value<long>() ?? 0;

                        var result = await _accountService.OpenPosition(login, symbol, action, volume);
                        return JsonConvert.SerializeObject(result);
                    }

                    case "modify_position":
                    {
                        var login = input["login"]?.Value<long>() ?? 0;
                        var ticket = input["ticket"]?.Value<long>() ?? 0;
                        var symbol = input["symbol"]?.ToString() ?? "";
                        var sl = input["sl"]?.Value<double>() ?? 0;
                        var tp = input["tp"]?.Value<double>() ?? 0;

                        var result = await _accountService.ModifyPosition(login, ticket, symbol, sl, tp);
                        return JsonConvert.SerializeObject(result);
                    }

                    case "close_all_positions":
                    {
                        var login = input["login"]?.Value<long>() ?? 0;
                        var positions = await _accountService.GetPositions(login);
                        var results = new List<ClosePositionResult>();

                        foreach (var pos in positions)
                        {
                            // To close, reverse the action: close buy with sell (action 1), close sell with buy (action 0)
                            var closeAction = pos.Action == 0 ? 1 : 0;
                            var result = await _accountService.ClosePosition(login, pos.Ticket, pos.Symbol, closeAction, pos.Volume);
                            results.Add(result);
                        }

                        return JsonConvert.SerializeObject(new
                        {
                            total = positions.Count,
                            closed = results.Count(r => r.Success),
                            failed = results.Count(r => !r.Success),
                            details = results
                        });
                    }

                    default:
                        return JsonConvert.SerializeObject(new { error = $"Unknown tool: {toolName}" });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AI] Tool execution error ({toolName}): {ex.Message}");
                return JsonConvert.SerializeObject(new { error = ex.Message });
            }
        }

        private static JArray GetToolDefinitions()
        {
            return new JArray
            {
                new JObject
                {
                    ["name"] = "close_position",
                    ["description"] = "Close a specific trading position for an account. Requires the position ticket, symbol, action direction, and volume.",
                    ["input_schema"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["login"] = new JObject { ["type"] = "integer", ["description"] = "The MT5 account login number" },
                            ["ticket"] = new JObject { ["type"] = "integer", ["description"] = "The position ticket number to close" },
                            ["symbol"] = new JObject { ["type"] = "string", ["description"] = "The trading symbol (e.g., EURUSD)" },
                            ["action"] = new JObject { ["type"] = "integer", ["description"] = "Trade action: 0=Buy, 1=Sell. Use opposite of the open action to close." },
                            ["volume"] = new JObject { ["type"] = "integer", ["description"] = "Volume to close in units (lots * 10000, e.g., 10000 = 1.0 lot)" }
                        },
                        ["required"] = new JArray { "login", "ticket", "symbol", "action", "volume" }
                    }
                },
                new JObject
                {
                    ["name"] = "open_position",
                    ["description"] = "Open a new trading position for an account.",
                    ["input_schema"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["login"] = new JObject { ["type"] = "integer", ["description"] = "The MT5 account login number" },
                            ["symbol"] = new JObject { ["type"] = "string", ["description"] = "The trading symbol (e.g., EURUSD)" },
                            ["action"] = new JObject { ["type"] = "integer", ["description"] = "Trade action: 0=Buy, 1=Sell" },
                            ["volume"] = new JObject { ["type"] = "integer", ["description"] = "Volume in units (lots * 10000, e.g., 10000 = 1.0 lot)" }
                        },
                        ["required"] = new JArray { "login", "symbol", "action", "volume" }
                    }
                },
                new JObject
                {
                    ["name"] = "modify_position",
                    ["description"] = "Modify the stop loss and/or take profit of an existing position.",
                    ["input_schema"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["login"] = new JObject { ["type"] = "integer", ["description"] = "The MT5 account login number" },
                            ["ticket"] = new JObject { ["type"] = "integer", ["description"] = "The position ticket number to modify" },
                            ["symbol"] = new JObject { ["type"] = "string", ["description"] = "The trading symbol (e.g., EURUSD)" },
                            ["sl"] = new JObject { ["type"] = "number", ["description"] = "New stop loss price (0 to remove)" },
                            ["tp"] = new JObject { ["type"] = "number", ["description"] = "New take profit price (0 to remove)" }
                        },
                        ["required"] = new JArray { "login", "ticket", "symbol", "sl", "tp" }
                    }
                },
                new JObject
                {
                    ["name"] = "close_all_positions",
                    ["description"] = "Close ALL open positions for a specific account. Use with caution.",
                    ["input_schema"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["login"] = new JObject { ["type"] = "integer", ["description"] = "The MT5 account login number whose positions should all be closed" }
                        },
                        ["required"] = new JArray { "login" }
                    }
                }
            };
        }

        private void TrimHistory()
        {
            while (_conversationHistory.Count > MaxHistoryMessages)
            {
                _conversationHistory.RemoveAt(0);
            }
        }

        private class SseParseResult
        {
            public List<JObject> ContentBlocks { get; set; } = new();
            public List<JObject> ToolUses { get; set; } = new();
            public string StopReason { get; set; } = "";
        }
    }
}
