using JournalApp.Models;

namespace JournalApp.Services
{
    public class UserSessionService
    {
        public User CurrentUser { get; private set; }

        public void SetUser(User user)
        {
            CurrentUser = user;
        }

        public void Logout()
        {
            CurrentUser = null;
        }

        public bool IsLoggedIn() => CurrentUser != null;
    }
}
