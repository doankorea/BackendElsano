using Makeup.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Makeup.Hubs
{
    public class MobileChatHub : Hub
    {
        private readonly MakeupContext _context;
        private readonly IHubContext<ChatHub> _chatHubContext;

        public MobileChatHub(MakeupContext context, IHubContext<ChatHub> chatHubContext)
        {
            _context = context;
            _chatHubContext = chatHubContext;
        }

        public async Task SendMessage(int senderId, int receiverId, string messageContent)
        {
            try
            {
                if (string.IsNullOrEmpty(messageContent))
                {
                    throw new HubException("Tin nhắn không được để trống");
                }

                // Kiểm tra người gửi và người nhận
                var senderExists = await _context.Users.AnyAsync(u => u.Id == senderId);
                if (!senderExists)
                {
                    throw new HubException("Người gửi không tồn tại");
                }

                var receiverExists = await _context.Users.AnyAsync(u => u.Id == receiverId);
                if (!receiverExists)
                {
                    throw new HubException("Người nhận không tồn tại");
                }

                // Lưu tin nhắn vào database
                var now = DateTime.Now;
                var message = new Message
                {
                    SenderId = senderId,
                    ReceiverId = receiverId,
                    MessageContent = messageContent,
                    MessageTimestamp = now,
                    IsRead = 0
                };

                await _context.Messages.AddAsync(message);
                await _context.SaveChangesAsync();

                // Format tin nhắn cho client
                var formattedDate = now.ToShortDateString();
                var formattedTime = now.ToShortTimeString();

                // Gửi tin nhắn tới người gửi (Mobile user)
                await Clients.Group(senderId.ToString()).SendAsync("ReceiveMessage", message.MessageId, senderId, receiverId, messageContent, formattedDate, formattedTime, true);
                
                // Gửi tin nhắn tới người nhận (Mobile user)
                await Clients.Group(receiverId.ToString()).SendAsync("ReceiveMessage", message.MessageId, senderId, receiverId, messageContent, formattedDate, formattedTime, false);

                // Gửi tin nhắn tới ChatHub (cho artist web app)
                // Sử dụng cả receiverId và senderId để đảm bảo artist nhận được tin nhắn
                await _chatHubContext.Clients.Users(new List<string> { receiverId.ToString(), senderId.ToString() })
                    .SendAsync("ReceiveMessage", messageContent, formattedDate, formattedTime, senderId);
            }
            catch (Exception ex)
            {
                throw new HubException($"Lỗi: {ex.Message}");
            }
        }

        public async Task MarkAsRead(int messageId, int userId)
        {
            try
            {
                var message = await _context.Messages.FindAsync(messageId);
                if (message == null)
                {
                    throw new HubException("Tin nhắn không tồn tại");
                }

                // Chỉ cho phép đánh dấu tin nhắn đã đọc nếu người dùng là người nhận
                if (message.ReceiverId != userId)
                {
                    throw new HubException("Bạn không có quyền đánh dấu tin nhắn này là đã đọc");
                }

                message.IsRead = 1;
                await _context.SaveChangesAsync();

                // Thông báo cho người gửi rằng tin nhắn đã được đọc (Mobile)
                await Clients.Group(message.SenderId.ToString()).SendAsync("MessageRead", messageId);
                
                // Thông báo cho người gửi rằng tin nhắn đã được đọc (Web)
                await _chatHubContext.Clients.User(message.SenderId.ToString()).SendAsync("MessageRead", messageId);
            }
            catch (Exception ex)
            {
                throw new HubException($"Lỗi: {ex.Message}");
            }
        }

        public override async Task OnConnectedAsync()
        {
            // Lấy userId từ query string
            var userId = Context.GetHttpContext()?.Request.Query["userId"].ToString();
            if (!string.IsNullOrEmpty(userId) && int.TryParse(userId, out var userIdInt))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, userId);
                await base.OnConnectedAsync();
                Console.WriteLine($"Mobile client connected: {Context.ConnectionId}, User: {userId}");
            }
            else
            {
                throw new HubException("UserId không hợp lệ");
            }
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var userId = Context.GetHttpContext()?.Request.Query["userId"].ToString();
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);
                Console.WriteLine($"Mobile client disconnected: {Context.ConnectionId}, User: {userId}");
            }
            await base.OnDisconnectedAsync(exception);
        }
    }
} 