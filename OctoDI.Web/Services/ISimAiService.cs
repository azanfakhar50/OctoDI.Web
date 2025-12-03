namespace OctoDI.Web.Services
{
    public interface ISimAiService
    {
        Task<string> GetChatResponseAsync(string message);
    }

}
