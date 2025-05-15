using Makeup.Hubs;
using Makeup.Models;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Makeup.Controllers
{
    [Route("api/mobile/messages")]
    [ApiController]
    [EnableCors("AllowAll")]
    public class MobileMessageController : ControllerBase
    {
        private readonly MakeupContext _context;
        private readonly ILogger<MobileMessageController> _logger;
        private readonly IHubContext<MobileChatHub> _mobileChatHubContext;
        private readonly IHubContext<ChatHub> _chatHubContext;

        public MobileMessageController(MakeupContext context, ILogger<MobileMessageController> logger,
            IHubContext<MobileChatHub> mobileChatHubContext, IHubContext<ChatHub> chatHubContext)
        {
            _context = context;
            _logger = logger;
            _mobileChatHubContext = mobileChatHubContext;
            _chatHubContext = chatHubContext;
        }

        // GET: api/mobile/messages/conversations/{userId}
        [HttpGet("conversations/{userId}")]
        public async Task<IActionResult> GetConversations(int userId)
        {
            try
            {
                // Lấy danh sách user đã từng chat với userId
                var users = await _context.Users
                    .Where(u => u.Id != userId &&
                        _context.Messages.Any(m =>
                            (m.SenderId == userId && m.ReceiverId == u.Id) ||
                            (m.ReceiverId == userId && m.SenderId == u.Id)))
                    .Select(u => new
                    {
                        id = u.Id,
                        username = u.UserName,
                        avatar = u.Avatar ?? "/assets/avatars/face-1.jpg",
                        // Lấy tin nhắn cuối cùng
                        lastMessage = _context.Messages
                            .Where(m =>
                                (m.SenderId == userId && m.ReceiverId == u.Id) ||
                                (m.SenderId == u.Id && m.ReceiverId == userId))
                            .OrderByDescending(m => m.MessageTimestamp)
                            .Select(m => m.MessageContent)
                            .FirstOrDefault(),
                        // Lấy thời gian tin nhắn cuối cùng
                        lastMessageTime = _context.Messages
                            .Where(m =>
                                (m.SenderId == userId && m.ReceiverId == u.Id) ||
                                (m.SenderId == u.Id && m.ReceiverId == userId))
                            .OrderByDescending(m => m.MessageTimestamp)
                            .Select(m => m.MessageTimestamp)
                            .FirstOrDefault(),
                        // Đếm số tin nhắn chưa đọc
                        unreadCount = _context.Messages
                            .Count(m => m.SenderId == u.Id && m.ReceiverId == userId && m.IsRead == 0),
                        // Kiểm tra xem đây có phải là artist không
                        isArtist = _context.MakeupArtists.Any(a => a.UserId == u.Id),
                        // Nếu là artist thì lấy thông tin artist
                        artist = _context.MakeupArtists
                            .Where(a => a.UserId == u.Id)
                            .Select(a => new
                            {
                                id = a.ArtistId,
                                fullName = a.FullName,
                                specialty = a.Specialty
                            })
                            .FirstOrDefault()
                    })
                    .OrderByDescending(u => u.lastMessageTime)
                    .ToListAsync();

                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách cuộc trò chuyện của user {UserId}", userId);
                return StatusCode(500, new { message = "Lỗi server" });
            }
        }

        // GET: api/mobile/messages/{userId}/{contactId}
        [HttpGet("{userId}/{contactId}")]
        public async Task<IActionResult> GetMessages(int userId, int contactId)
        {
            try
            {
                // Đánh dấu tin nhắn đã đọc
                var unreadMessages = await _context.Messages
                    .Where(m => m.SenderId == contactId && m.ReceiverId == userId && m.IsRead == 0)
                    .ToListAsync();

                foreach (var message in unreadMessages)
                {
                    message.IsRead = 1;
                }

                await _context.SaveChangesAsync();

                // Thông báo cho người gửi rằng tin nhắn đã được đọc (nếu có tin nhắn chưa đọc)
                if (unreadMessages.Any())
                {
                    foreach (var message in unreadMessages)
                    {
                        await _mobileChatHubContext.Clients.Group(message.SenderId.ToString())
                            .SendAsync("MessageRead", message.MessageId);
                        
                        await _chatHubContext.Clients.User(message.SenderId.ToString())
                            .SendAsync("MessageRead", message.MessageId);
                    }
                }

                // Lấy tất cả tin nhắn giữa 2 user
                var messages = await _context.Messages
                    .Where(m => (m.SenderId == userId && m.ReceiverId == contactId) ||
                                (m.SenderId == contactId && m.ReceiverId == userId))
                    .OrderBy(m => m.MessageTimestamp)
                    .Select(m => new
                    {
                        id = m.MessageId,
                        senderId = m.SenderId,
                        receiverId = m.ReceiverId,
                        content = m.MessageContent,
                        timestamp = m.MessageTimestamp,
                        isRead = m.IsRead == 1,
                        isSentByMe = m.SenderId == userId
                    })
                    .ToListAsync();

                // Lấy thông tin của người liên hệ
                var contact = await _context.Users
                    .Where(u => u.Id == contactId)
                    .Select(u => new
                    {
                        id = u.Id,
                        username = u.UserName,
                        avatar = u.Avatar ?? "/assets/avatars/face-1.jpg",
                        // Kiểm tra xem đây có phải là artist không
                        isArtist = _context.MakeupArtists.Any(a => a.UserId == u.Id),
                        // Nếu là artist thì lấy thông tin artist
                        artist = _context.MakeupArtists
                            .Where(a => a.UserId == u.Id)
                            .Select(a => new
                            {
                                id = a.ArtistId,
                                fullName = a.FullName,
                                specialty = a.Specialty
                            })
                            .FirstOrDefault()
                    })
                    .FirstOrDefaultAsync();

                if (contact == null)
                {
                    return NotFound(new { message = "Không tìm thấy người dùng" });
                }

                return Ok(new
                {
                    contact,
                    messages
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy tin nhắn giữa user {UserId} và {ContactId}", userId, contactId);
                return StatusCode(500, new { message = "Lỗi server" });
            }
        }

        // POST: api/mobile/messages/send
        [HttpPost("send")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                if (string.IsNullOrEmpty(request.Message))
                {
                    return BadRequest(new { message = "Tin nhắn không được để trống" });
                }

                // Kiểm tra người gửi có tồn tại không
                var sender = await _context.Users.FindAsync(request.SenderId);
                if (sender == null)
                {
                    return NotFound(new { message = "Không tìm thấy người gửi" });
                }

                // Kiểm tra người nhận có tồn tại không
                var receiver = await _context.Users.FindAsync(request.ReceiverId);
                if (receiver == null)
                {
                    return NotFound(new { message = "Không tìm thấy người nhận" });
                }

                var now = DateTime.Now;
                var message = new Message
                {
                    SenderId = request.SenderId,
                    ReceiverId = request.ReceiverId,
                    MessageContent = request.Message,
                    MessageTimestamp = now,
                    IsRead = 0
                };

                _context.Messages.Add(message);
                await _context.SaveChangesAsync();

                // Format tin nhắn cho client
                var formattedDate = now.ToShortDateString();
                var formattedTime = now.ToShortTimeString();

                // Gửi tin nhắn tới người gửi (Mobile user)
                await _mobileChatHubContext.Clients.Group(request.SenderId.ToString())
                    .SendAsync("ReceiveMessage", message.MessageId, request.SenderId, request.ReceiverId, request.Message, formattedDate, formattedTime, true);
                
                // Gửi tin nhắn tới người nhận (Mobile user)
                await _mobileChatHubContext.Clients.Group(request.ReceiverId.ToString())
                    .SendAsync("ReceiveMessage", message.MessageId, request.SenderId, request.ReceiverId, request.Message, formattedDate, formattedTime, false);

                // Gửi tin nhắn tới ChatHub (cho artist web app)
                await _chatHubContext.Clients.Users(new List<string> { request.ReceiverId.ToString(), request.SenderId.ToString() })
                    .SendAsync("ReceiveMessage", request.Message, formattedDate, formattedTime, request.SenderId);

                // Log để debug
                _logger.LogInformation("Tin nhắn đã được gửi từ {SenderId} đến {ReceiverId}: {Message}", 
                    request.SenderId, request.ReceiverId, request.Message);

                return Ok(new
                {
                    id = message.MessageId,
                    senderId = message.SenderId,
                    receiverId = message.ReceiverId,
                    content = message.MessageContent,
                    timestamp = message.MessageTimestamp,
                    isRead = false,
                    isSentByMe = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gửi tin nhắn từ {SenderId} đến {ReceiverId}: {Message}", 
                    request.SenderId, request.ReceiverId, ex.Message);
                return StatusCode(500, new { message = "Lỗi server: " + ex.Message });
            }
        }
    }

    public class SendMessageRequest
    {
        public int SenderId { get; set; }
        public int ReceiverId { get; set; }
        public string Message { get; set; } = string.Empty;
    }
} 