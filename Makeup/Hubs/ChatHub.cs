using Makeup.Models;
using Makeup.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Makeup.Hubs
{
    public class ChatHub : Hub
    {
        private readonly ICurrentUserService currentUserService;
        private readonly MakeupContext context;
        private readonly IHubContext<MobileChatHub> _mobileChatHubContext;
        
        public ChatHub(ICurrentUserService currentUserService, MakeupContext context, IHubContext<MobileChatHub> mobileChatHubContext)
        {
            this.currentUserService = currentUserService;
            this.context = context;
            _mobileChatHubContext = mobileChatHubContext;
        }
        
        public async Task SendMessage(int receiverId, string message)
        {
            try
            {
                Console.WriteLine($"[ChatHub] SendMessage called: receiverId={receiverId}, message={message}");

                if (string.IsNullOrEmpty(message))
                {
                    Console.WriteLine("[ChatHub] Tin nhắn rỗng");
                    throw new HubException("Tin nhắn không được để trống.");
                }

                var senderId = currentUserService.UserId;
                Console.WriteLine($"[ChatHub] SenderId: {senderId}, IsAuthenticated: {Context.User?.Identity?.IsAuthenticated}, Claims: {string.Join(", ", Context.User?.Claims.Select(c => $"{c.Type}:{c.Value}") ?? new List<string>())}");

                if (senderId == 0)
                {
                    Console.WriteLine("[ChatHub] Người dùng chưa được xác thực");
                    throw new HubException("Người dùng chưa được xác thực.");
                }

                var receiverExists = await context.Users.AnyAsync(u => u.Id == receiverId);
                if (!receiverExists)
                {
                    Console.WriteLine($"[ChatHub] ReceiverId {receiverId} không tồn tại");
                    throw new HubException("Người nhận không tồn tại.");
                }

                var now = DateTime.Now;
                var date = now.ToShortDateString();
                var time = now.ToShortTimeString();
                var messageToAdd = new Message()
                {
                    IsRead = 0,
                    MessageContent = message,
                    MessageTimestamp = now,
                    SenderId = senderId,
                    ReceiverId = receiverId
                };

                Console.WriteLine("[ChatHub] Thêm tin nhắn vào context");
                await context.AddAsync(messageToAdd);
                Console.WriteLine("[ChatHub] Lưu thay đổi");
                await context.SaveChangesAsync();
                Console.WriteLine("[ChatHub] Đã lưu thay đổi");

                // Danh sách người dùng web nhận tin nhắn
                List<string> users = new List<string>() { receiverId.ToString(), senderId.ToString() };

                // Gửi tin nhắn cho web app
                await Clients.Users(users).SendAsync("ReceiveMessage", message, date, time, senderId);
                Console.WriteLine($"[ChatHub] Đã gửi tin nhắn cho web users: {string.Join(", ", users)}");

                // Gửi tin nhắn cho mobile app
                await _mobileChatHubContext.Clients.Group(receiverId.ToString())
                    .SendAsync("ReceiveMessage", messageToAdd.MessageId, senderId, receiverId, message, date, time, false);
                Console.WriteLine($"[ChatHub] Đã gửi tin nhắn cho mobile user: {receiverId}");
                
                // Gửi tin nhắn cho người gửi mobile (nếu đang online)
                await _mobileChatHubContext.Clients.Group(senderId.ToString())
                    .SendAsync("ReceiveMessage", messageToAdd.MessageId, senderId, receiverId, message, date, time, true);
                Console.WriteLine($"[ChatHub] Đã gửi tin nhắn cho mobile sender: {senderId}");

                Console.WriteLine("[ChatHub] Đã gửi tin nhắn hoàn tất");
            }
            catch (DbUpdateException ex)
            {
                Console.WriteLine($"[ChatHub DB ERROR] {ex.Message}\nInner: {ex.InnerException?.Message}\nStackTrace: {ex.StackTrace}");
                throw new HubException("Lỗi khi lưu tin nhắn vào cơ sở dữ liệu.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SendMessage ERROR] {ex.Message}\nStackTrace: {ex.StackTrace}");
                throw new HubException($"Lỗi server nội bộ: {ex.Message}");
            }
        }

        public async Task MarkAsRead(int messageId)
        {
            try
            {
                var userId = currentUserService.UserId;
                if (userId == 0)
                {
                    throw new HubException("Người dùng chưa được xác thực.");
                }

                var message = await context.Messages.FindAsync(messageId);
                if (message == null)
                {
                    throw new HubException("Tin nhắn không tồn tại.");
                }

                if (message.ReceiverId != userId)
                {
                    throw new HubException("Bạn không có quyền đánh dấu tin nhắn này là đã đọc.");
                }

                message.IsRead = 1;
                await context.SaveChangesAsync();

                // Thông báo cho web app
                await Clients.User(message.SenderId.ToString()).SendAsync("MessageRead", messageId);
                Console.WriteLine($"[ChatHub] Đã gửi MessageRead cho web user: {message.SenderId}");
                
                // Thông báo cho mobile app
                await _mobileChatHubContext.Clients.Group(message.SenderId.ToString())
                    .SendAsync("MessageRead", messageId);
                Console.WriteLine($"[ChatHub] Đã gửi MessageRead cho mobile user: {message.SenderId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MarkAsRead ERROR] {ex.Message}\nStackTrace: {ex.StackTrace}");
                throw new HubException($"Lỗi server nội bộ: {ex.Message}");
            }
        }

        public override async Task OnConnectedAsync()
        {
            var userId = currentUserService.UserId;
            var connectionId = Context.ConnectionId;
            var isAuthenticated = Context.User?.Identity?.IsAuthenticated ?? false;

            Console.WriteLine($"[ChatHub] OnConnectedAsync - ConnectionId: {connectionId}, UserId: {userId}, IsAuthenticated: {isAuthenticated}");
            
            if (isAuthenticated && userId > 0)
            {
                Console.WriteLine($"[ChatHub] User {userId} connected with connectionId {connectionId}");
            }
            else
            {
                Console.WriteLine($"[ChatHub] Connection {connectionId} established but user is not authenticated. Claims: {string.Join(", ", Context.User?.Claims.Select(c => $"{c.Type}:{c.Value}") ?? new List<string>())}");
            }
            
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var userId = currentUserService.UserId;
            var connectionId = Context.ConnectionId;
            
            Console.WriteLine($"[ChatHub] OnDisconnectedAsync - ConnectionId: {connectionId}, UserId: {userId}");
            
            if (exception != null)
            {
                Console.WriteLine($"[ChatHub] Disconnected with error: {exception.Message}");
            }
            
            await base.OnDisconnectedAsync(exception);
        }
    }
}
