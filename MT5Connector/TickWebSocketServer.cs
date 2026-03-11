using System.Collections.Concurrent;
using Fleck;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace MT5Connector
{
    public class TickWebSocketServer
    {
        private readonly MT5Service _mt5;
        private readonly AccountService _accountService;
        private readonly DbService _db;
        private readonly TickHistoryQuery _tickHistory;
        private readonly AiService? _ai;
        private readonly SyncService _sync;
        private WebSocketServer? _server;
        private readonly ConcurrentDictionary<Guid, IWebSocketConnection> _clients = new();

        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore
        };

        public TickWebSocketServer(
            MT5Service mt5,
            AccountService accountService,
            DbService db,
            TickHistoryQuery tickHistory,
            SyncService sync,
            AiService? ai = null)
        {
            _mt5 = mt5;
            _accountService = accountService;
            _db = db;
            _tickHistory = tickHistory;
            _sync = sync;
            _ai = ai;
        }

        public void Start(int port)
        {
            _server = new WebSocketServer($"ws://0.0.0.0:{port}");

            _server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    var id = socket.ConnectionInfo.Id;
                    _clients[id] = socket;
                    Console.WriteLine($"[WS] Client connected: {id} ({_clients.Count} total)");

                    // Send full symbol list first
                    try
                    {
                        var symbols = _mt5.GetSymbols();
                        var symbolsJson = JsonConvert.SerializeObject(
                            new { type = "symbols", data = symbols.Values.ToList() }, JsonSettings);
                        socket.Send(symbolsJson);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[WS] Error sending symbols to {id}: {ex.Message}");
                    }

                    // Send snapshot of all current ticks
                    try
                    {
                        var ticks = _mt5.GetAllTicks();
                        var snapshot = new SnapshotMessage
                        {
                            Type = "snapshot",
                            Data = ticks.Values.ToList()
                        };
                        var json = JsonConvert.SerializeObject(snapshot, JsonSettings);
                        socket.Send(json);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[WS] Error sending snapshot to {id}: {ex.Message}");
                    }
                };

                socket.OnClose = () =>
                {
                    var id = socket.ConnectionInfo.Id;
                    _clients.TryRemove(id, out _);
                    Console.WriteLine($"[WS] Client disconnected: {id} ({_clients.Count} total)");
                };

                socket.OnMessage = message =>
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await HandleMessage(socket, message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[WS] Error handling message from {socket.ConnectionInfo.Id}: {ex.Message}");
                            try
                            {
                                SendJson(socket, new { type = "error", data = new { message = ex.Message } });
                            }
                            catch { /* client may have disconnected */ }
                        }
                    });
                };
            });

            Console.WriteLine($"[WS] WebSocket server started on port {port}");
        }

        public void Stop()
        {
            _server?.Dispose();
            _clients.Clear();
            Console.WriteLine("[WS] WebSocket server stopped");
        }

        public void BroadcastTick(TickData tick)
        {
            var json = JsonConvert.SerializeObject(new { type = "tick", data = tick }, JsonSettings);
            BroadcastRaw(json);
        }

        public void BroadcastDealEvent(DealEventData deal)
        {
            // Send the deal event
            var dealJson = JsonConvert.SerializeObject(new { type = "dealEvent", data = deal }, JsonSettings);
            BroadcastRaw(dealJson);

            // Also send updated account data and positions (fire-and-forget)
            _ = Task.Run(async () =>
            {
                try
                {
                    var account = await _db.GetAccount(deal.Login);
                    if (account != null)
                    {
                        var accountJson = JsonConvert.SerializeObject(
                            new { type = "accountUpdate", data = account }, JsonSettings);
                        BroadcastRaw(accountJson);
                    }

                    var positions = await _db.GetPositions(deal.Login);
                    var positionsJson = JsonConvert.SerializeObject(
                        new { type = "positions", data = new { login = deal.Login, positions } }, JsonSettings);
                    BroadcastRaw(positionsJson);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WS] Error broadcasting deal follow-up for login {deal.Login}: {ex.Message}");
                }
            });
        }

        // ── Private Helpers ──────────────────────────────────────────────

        private async Task HandleMessage(IWebSocketConnection socket, string raw)
        {
            var msg = JObject.Parse(raw);
            var type = msg["type"]?.ToString() ?? "";
            var data = msg["data"] as JObject;

            switch (type)
            {
                case "ping":
                    SendJson(socket, new { type = "pong" });
                    break;

                case "getAccounts":
                {
                    var page = data?["page"]?.Value<int>() ?? 1;
                    var pageSize = data?["pageSize"]?.Value<int>() ?? 50;
                    var search = data?["search"]?.ToString();
                    var result = await _accountService.GetAccounts(page, pageSize, search);
                    SendJson(socket, new { type = "accounts", data = result });
                    break;
                }

                case "getPositions":
                {
                    var login = data?["login"]?.Value<long>() ?? 0;
                    var positions = await _accountService.GetPositions(login);
                    SendJson(socket, new { type = "positions", data = positions });
                    break;
                }

                case "closePosition":
                {
                    var login = data?["login"]?.Value<long>() ?? 0;
                    var ticket = data?["ticket"]?.Value<long>() ?? 0;
                    var symbol = data?["symbol"]?.ToString() ?? "";
                    var action = data?["action"]?.Value<int>() ?? 0;
                    var volume = data?["volume"]?.Value<long>() ?? 0;
                    var result = await _accountService.ClosePosition(login, ticket, symbol, action, volume);
                    SendJson(socket, new { type = "closePositionResult", data = result });
                    break;
                }

                case "openPosition":
                {
                    var login = data?["login"]?.Value<long>() ?? 0;
                    var symbol = data?["symbol"]?.ToString() ?? "";
                    var action = data?["action"]?.Value<int>() ?? 0;
                    var volume = data?["volume"]?.Value<long>() ?? 0;
                    var result = await _accountService.OpenPosition(login, symbol, action, volume);
                    SendJson(socket, new { type = "openPositionResult", data = result });
                    break;
                }

                case "modifyPosition":
                {
                    var login = data?["login"]?.Value<long>() ?? 0;
                    var ticket = data?["ticket"]?.Value<long>() ?? 0;
                    var symbol = data?["symbol"]?.ToString() ?? "";
                    var sl = data?["sl"]?.Value<double>() ?? 0;
                    var tp = data?["tp"]?.Value<double>() ?? 0;
                    var result = await _accountService.ModifyPosition(login, ticket, symbol, sl, tp);
                    SendJson(socket, new { type = "modifyPositionResult", data = result });
                    break;
                }

                case "getTickHistory":
                {
                    var symbol = data?["symbol"]?.ToString() ?? "";
                    var interval = data?["interval"]?.ToString() ?? "1h";
                    var from = data?["from"]?.Value<DateTime>() ?? DateTime.UtcNow.AddHours(-24);
                    var to = data?["to"]?.Value<DateTime>() ?? DateTime.UtcNow;
                    var limit = data?["limit"]?.Value<int>() ?? 500;
                    var candles = await _tickHistory.GetCandles(symbol, interval, from, to, limit);
                    SendJson(socket, new { type = "tickHistory", data = candles });
                    break;
                }

                case "getDashboardData":
                {
                    var summary = await _db.GetAccountSummary();
                    var exposure = await _db.GetSymbolExposure();
                    var topMovers = await _db.GetTopMovers();
                    var from = DateTime.UtcNow.AddHours(-24);
                    var to = DateTime.UtcNow;
                    var dealHistory = await _db.GetDealHistoryBuckets(from, to);
                    SendJson(socket, new { type = "dashboardData", data = new { summary, exposure, topMovers, dealHistory } });
                    break;
                }

                case "getDealHistory":
                {
                    var from = data?["from"]?.Value<DateTime>() ?? DateTime.UtcNow.AddHours(-24);
                    var to = data?["to"]?.Value<DateTime>() ?? DateTime.UtcNow;
                    var interval = data?["interval"]?.ToString() ?? "1 hour";
                    var buckets = await _db.GetDealHistoryBuckets(from, to, interval);
                    SendJson(socket, new { type = "dealHistory", data = buckets });
                    break;
                }

                case "chat":
                {
                    if (_ai == null)
                    {
                        SendJson(socket, new { type = "chatChunk", data = new { text = "AI service is not available." } });
                        SendJson(socket, new { type = "chatDone", data = new { } });
                        break;
                    }

                    var chatMessage = data?["message"]?.ToString() ?? "";
                    await _ai.ProcessChatMessage(
                        chatMessage,
                        onChunk: text =>
                        {
                            try { SendJson(socket, new { type = "chatChunk", data = new { text } }); }
                            catch { /* client may have disconnected */ }
                        },
                        onAction: (action, parms) =>
                        {
                            try { SendJson(socket, new { type = "chatAction", data = new { action, @params = parms } }); }
                            catch { /* client may have disconnected */ }
                        },
                        onDone: () =>
                        {
                            try { SendJson(socket, new { type = "chatDone", data = new { } }); }
                            catch { /* client may have disconnected */ }
                        }
                    );
                    break;
                }

                case "executeAction":
                {
                    var actionName = data?["action"]?.ToString() ?? "";
                    var actionParams = data?["params"] as JObject;
                    var result = await ExecuteAction(actionName, actionParams);
                    SendJson(socket, new { type = "executeActionResult", data = result });
                    break;
                }

                default:
                    Console.WriteLine($"[WS] Unknown message type: {type}");
                    SendJson(socket, new { type = "error", data = new { message = $"Unknown message type: {type}" } });
                    break;
            }
        }

        private async Task<object> ExecuteAction(string actionName, JObject? parms)
        {
            switch (actionName)
            {
                case "closePosition":
                {
                    var login = parms?["login"]?.Value<long>() ?? 0;
                    var ticket = parms?["ticket"]?.Value<long>() ?? 0;
                    var symbol = parms?["symbol"]?.ToString() ?? "";
                    var action = parms?["action"]?.Value<int>() ?? 0;
                    var volume = parms?["volume"]?.Value<long>() ?? 0;
                    return await _accountService.ClosePosition(login, ticket, symbol, action, volume);
                }

                case "openPosition":
                {
                    var login = parms?["login"]?.Value<long>() ?? 0;
                    var symbol = parms?["symbol"]?.ToString() ?? "";
                    var action = parms?["action"]?.Value<int>() ?? 0;
                    var volume = parms?["volume"]?.Value<long>() ?? 0;
                    return await _accountService.OpenPosition(login, symbol, action, volume);
                }

                case "modifyPosition":
                {
                    var login = parms?["login"]?.Value<long>() ?? 0;
                    var ticket = parms?["ticket"]?.Value<long>() ?? 0;
                    var symbol = parms?["symbol"]?.ToString() ?? "";
                    var sl = parms?["sl"]?.Value<double>() ?? 0;
                    var tp = parms?["tp"]?.Value<double>() ?? 0;
                    return await _accountService.ModifyPosition(login, ticket, symbol, sl, tp);
                }

                default:
                    return new { success = false, message = $"Unknown action: {actionName}" };
            }
        }

        private void SendJson(IWebSocketConnection socket, object payload)
        {
            try
            {
                var json = JsonConvert.SerializeObject(payload, JsonSettings);
                socket.Send(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WS] Error sending to {socket.ConnectionInfo.Id}: {ex.Message}");
                // Remove disconnected client
                _clients.TryRemove(socket.ConnectionInfo.Id, out _);
            }
        }

        private void BroadcastRaw(string json)
        {
            foreach (var kvp in _clients)
            {
                try
                {
                    kvp.Value.Send(json);
                }
                catch (Exception)
                {
                    // Remove disconnected client
                    _clients.TryRemove(kvp.Key, out _);
                }
            }
        }
    }
}
