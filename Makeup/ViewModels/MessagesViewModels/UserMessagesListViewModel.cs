namespace Makeup.ViewModels.MessagesViewModels
{
    public class UserMessagesListViewModel
    {
        public int Id { get; set; }
        public string Text { get; set; }
        public DateTime? DateTime { get; set; }
        public bool IsCurrentUserSentMessage { get; set; }
    }
}
