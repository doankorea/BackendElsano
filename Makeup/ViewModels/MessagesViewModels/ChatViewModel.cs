namespace Makeup.ViewModels.MessagesViewModels
{
    public class ChatViewModel
    {
        public ChatViewModel()
        {
            Messages = new List<UserMessagesListViewModel>();
        }

        public int CurrentUserId { get; set; }
        public int ReceiverId { get; set; }
        public string ReceiverUserName { get; set; }
        public string ReceiverAvatar { get; set; }
        public IEnumerable<UserMessagesListViewModel> Messages { get; set; }
    }
}
 