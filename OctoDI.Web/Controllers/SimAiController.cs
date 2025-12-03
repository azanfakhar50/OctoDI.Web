using Microsoft.AspNetCore.Mvc;
using OctoDI.Web.Services;

[Route("[controller]/[action]")]
public class SimAiController : Controller
{
    private readonly ISimAiService _simService;

    public SimAiController(ISimAiService simService)
    {
        _simService = simService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        var response = await _simService.GetChatResponseAsync(request.Message);
        return Json(new { reply = response });
    }
}

public class ChatRequest
{
    public string Message { get; set; }
}
