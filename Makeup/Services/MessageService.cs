using Makeup.Models;
using Makeup.ViewModels.MessagesViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Makeup.Services
{
    public interface IMessageService
    {
        Task<IEnumerable<MessagesUsersListViewModel>> GetUsers();
        Task<ChatViewModel> GetMessages(int selectedUserId);
        Task<IEnumerable<MessagesUsersListViewModel>> FilterUsersByName(string searchQuery);
    }
    public class MessageService : IMessageService
    {
        private readonly MakeupContext _context;
        private readonly ICurrentUserService _currentUserService;
        public MessageService(MakeupContext context, ICurrentUserService currentUserService)
        {
            _currentUserService = currentUserService;
            _context = context;

        }
        public async Task<ChatViewModel> GetMessages(int selectedUserId)
        {
            var currentUserId = _currentUserService.UserId;
            if (currentUserId == 0)
                throw new Exception("Không lấy được ID người dùng hiện tại!");
            var selectedUser = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == selectedUserId);
            var selectedUserName = selectedUser?.UserName ?? "Unknown User";
            if (selectedUser == null)
            {
                throw new KeyNotFoundException($"User with ID {selectedUserId} not found.");
            }

            var chatViewModel = new ChatViewModel()
            {
                CurrentUserId = currentUserId,
                ReceiverId = selectedUserId,
                ReceiverUserName = selectedUserName,
                ReceiverAvatar = selectedUser.Avatar
            };

            var messages = await _context.Messages.Where(i => (i.SenderId == currentUserId && i.ReceiverId == selectedUserId) ||
        (i.SenderId == selectedUserId && i.ReceiverId == currentUserId))
                                                 .OrderBy(i => i.MessageTimestamp)
                                                 .Select(i => new UserMessagesListViewModel()
                                                 {
                                                     Id = i.MessageId,
                                                     Text = i.MessageContent,
                                                     DateTime = i.MessageTimestamp,
                                                     IsCurrentUserSentMessage = i.SenderId == currentUserId,
                                                 })
                                                 .ToListAsync();

            chatViewModel.Messages = messages;

            return chatViewModel;
        }

        public async Task<IEnumerable<MessagesUsersListViewModel>> GetUsers()
        {
            var currentUserId = _currentUserService.UserId;

            var users = await _context.Users
       .Where(u => u.Id != currentUserId &&
                   _context.Messages.Any(m =>
                       (m.SenderId == currentUserId && m.ReceiverId == u.Id) ||
                       (m.ReceiverId == currentUserId && m.SenderId == u.Id)))
       .Select(u => new MessagesUsersListViewModel()
       {
           Id = u.Id,
           UserName = u.UserName,
           UserAvatar = u.Avatar,
           LastMessage = _context.Messages
               .Where(m =>
                   (m.SenderId == currentUserId && m.ReceiverId == u.Id) ||
                   (m.SenderId == u.Id && m.ReceiverId == currentUserId))
               .OrderByDescending(a => a.MessageId)
               .Select(a => a.MessageContent)
               .FirstOrDefault()
       })
       .ToListAsync();
            Console.WriteLine("Current user ID: " + currentUserId);

            return users;
        }
        public async Task<IEnumerable<MessagesUsersListViewModel>> FilterUsersByName(string searchQuery)
        {
            if (string.IsNullOrEmpty(searchQuery))
            {
                return await GetUsers(); // Trả về tất cả người dùng nếu không có từ khóa tìm kiếm
            }

            var currentUserId = _currentUserService.UserId;

            var users = await _context.Users
                .Where(u => u.UserName.Contains(searchQuery) && u.Id != currentUserId &&
                            _context.Messages.Any(m =>
                                (m.SenderId == currentUserId && m.ReceiverId == u.Id) ||
                                (m.SenderId == u.Id && m.ReceiverId == currentUserId)))
                .Select(u => new MessagesUsersListViewModel()
                {
                    Id = u.Id,
                    UserName = u.UserName,
                    UserAvatar = u.Avatar,
                    LastMessage = _context.Messages
                        .Where(m =>
                            (m.SenderId == currentUserId && m.ReceiverId == u.Id) ||
                            (m.SenderId == u.Id && m.ReceiverId == currentUserId))
                        .OrderByDescending(a => a.MessageId)
                        .Select(a => a.MessageContent)
                        .FirstOrDefault()
                })
                .ToListAsync();

            return users;
        }
    }
}
