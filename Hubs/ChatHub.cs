// Hubs/ChatHub.cs
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TripWise.Models;

namespace TripWise.Hubs
{
    public class ChatHub : Hub
    {
        private readonly TripWiseContext _context;
        private static readonly Dictionary<int, string> _userConnections = new(); // static readonly

        public ChatHub(TripWiseContext context)
        {
            _context = context;
        }

        // ========== ПОЛУЧЕНИЕ ID ПОЛЬЗОВАТЕЛЯ ИЗ CLAIMS ==========
        private int? GetUserId()
        {
            var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim != null && int.TryParse(userIdClaim, out var userId))
            {
                return userId;
            }

            // Fallback: пытаемся получить из сессии через заголовки (не самый надежный способ)
            var userIdHeader = Context.GetHttpContext()?.Request.Headers["X-UserId"].FirstOrDefault();
            if (userIdHeader != null && int.TryParse(userIdHeader, out userId))
            {
                return userId;
            }

            return null;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = GetUserId();
            if (userId.HasValue)
            {
                _userConnections[userId.Value] = Context.ConnectionId;
                Console.WriteLine($"[SignalR] Пользователь {userId} подключен. ConnectionId: {Context.ConnectionId}");
            }
            else
            {
                Console.WriteLine($"[SignalR] Подключение без userId. ConnectionId: {Context.ConnectionId}");
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var userId = _userConnections.FirstOrDefault(x => x.Value == Context.ConnectionId).Key;
            if (userId != 0)
            {
                _userConnections.Remove(userId);
                Console.WriteLine($"[SignalR] Пользователь {userId} отключен");
            }
            await base.OnDisconnectedAsync(exception);
        }

        // ========== ОТПРАВКА СООБЩЕНИЯ ВСЕМ УЧАСТНИКАМ ЧАТА ==========
        public async Task SendMessageToChat(int chatId, int messageId, string text, int senderId, string senderName, DateTime sentAt, List<object> attachments)
        {
            // Получаем всех участников чата
            var members = await _context.ChatMembers
                .Where(cm => cm.ChatId == chatId)
                .Select(cm => cm.UserId)
                .ToListAsync();

            foreach (var memberId in members)
            {
                if (_userConnections.TryGetValue(memberId, out var connectionId))
                {
                    await Clients.Client(connectionId).SendAsync("ReceiveMessage", new
                    {
                        id = messageId,
                        text = text,
                        senderId = senderId,
                        senderName = senderName,
                        sentAt = sentAt,
                        attachments = attachments,
                        chatId = chatId,
                        isOutgoing = memberId == senderId
                    });
                }
            }
        }

        // ========== ПРИСОЕДИНЕНИЕ К ГРУППЕ ЧАТА ==========
        public async Task JoinChat(int chatId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"chat_{chatId}");
            Console.WriteLine($"[SignalR] Пользователь присоединился к группе chat_{chatId}");
        }

        // ========== ПОКИДАНИЕ ГРУППЫ ЧАТА ==========
        public async Task LeaveChat(int chatId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chat_{chatId}");
            Console.WriteLine($"[SignalR] Пользователь покинул группу chat_{chatId}");
        }

        // ========== ОТПРАВКА СООБЩЕНИЯ В ГРУППУ (альтернативный способ) ==========
        public async Task SendMessageToGroup(int chatId, int messageId, string text, int senderId, string senderName, DateTime sentAt, List<object> attachments)
        {
            await Clients.Group($"chat_{chatId}").SendAsync("ReceiveMessage", new
            {
                id = messageId,
                text = text,
                senderId = senderId,
                senderName = senderName,
                sentAt = sentAt,
                attachments = attachments,
                chatId = chatId,
                isOutgoing = false // В группе каждый сам определяет isOutgoing на клиенте
            });
        }

        // ========== ПОЛУЧЕНИЕ СПИСКА ОНЛАЙН ПОЛЬЗОВАТЕЛЕЙ В ЧАТЕ ==========
        public async Task<List<int>> GetOnlineUsersInChat(int chatId)
        {
            var members = await _context.ChatMembers
                .Where(cm => cm.ChatId == chatId)
                .Select(cm => cm.UserId)
                .ToListAsync();

            var onlineUsers = members.Where(m => _userConnections.ContainsKey(m)).ToList();
            return onlineUsers;
        }
    }
}